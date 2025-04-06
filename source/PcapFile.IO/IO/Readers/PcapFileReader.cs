using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件读取器，负责管理PCAP文件的打开、读取和关闭操作
    /// </summary>
    /// <remarks>
    /// 该类封装了PCAP工程文件的底层操作，提供读取文件头、文件条目表和索引表的功能。
    /// </remarks>
    internal class PcapFileReader : IDisposable
    {
        #region 字段

        private FileStream _FileStream;
        private BinaryReader _BinaryReader;
        private bool _IsDisposed;

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前PCAP文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 获取当前文件流的位置
        /// </summary>
        public long Position => _FileStream?.Position ?? 0;

        /// <summary>
        /// 获取文件大小
        /// </summary>
        public long FileSize => _FileStream?.Length ?? 0;

        /// <summary>
        /// 获取文件是否已打开且未释放
        /// </summary>
        public bool IsOpen => _FileStream != null && !_IsDisposed;

        /// <summary>
        /// 获取文件头
        /// </summary>
        public PcapFileHeader Header { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PCAP文件读取器
        /// </summary>
        public PcapFileReader()
        {
            // 默认构造函数
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 打开PCAP文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <exception cref="ArgumentException">文件路径为空或无效</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">打开文件时发生错误</exception>
        public void Open(string filePath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PCAP文件不存在", filePath);
            }

            try
            {
                FilePath = filePath;
                _FileStream = StreamHelper.CreateFileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                _BinaryReader = StreamHelper.CreateBinaryReader(_FileStream);

                // 读取文件头
                ReadHeader();
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"打开PCAP文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取PCAP文件头
        /// </summary>
        /// <returns>文件头</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="FormatException">文件格式无效</exception>
        public PcapFileHeader ReadHeader()
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                // 保存当前位置
                var originalPosition = _FileStream.Position;

                // 跳转到文件开头
                _FileStream.Position = 0;

                // 读取文件头
                Header = StreamHelper.ReadStructure<PcapFileHeader>(_FileStream);

                // 验证魔术数
                if (Header.MagicNumber != Configuration.FileVersionConfig.PCAP_MAGIC_NUMBER)
                {
                    throw new FormatException("无效的PCAP文件格式，魔术数不匹配");
                }

                // 恢复原始位置
                _FileStream.Position = originalPosition;

                return Header;
            }
            catch (EndOfStreamException)
            {
                throw new FormatException("无效的PCAP文件格式，文件过小");
            }
            catch (IOException ex)
            {
                throw new IOException($"读取文件头失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取所有文件条目
        /// </summary>
        /// <param name="fileCount">文件数量</param>
        /// <returns>文件条目列表</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">读取文件条目失败</exception>
        public List<PataFileEntry> ReadAllFileEntries(ushort fileCount)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (fileCount == 0)
            {
                return new List<PataFileEntry>();
            }

            try
            {
                var entries = new List<PataFileEntry>(fileCount);
                _FileStream.Position = Header.FileEntryOffset;

                for (int i = 0; i < fileCount; i++)
                {
                    var entry = StreamHelper.ReadStructure<PataFileEntry>(_FileStream);
                    entries.Add(entry);
                }

                return entries;
            }
            catch (Exception ex)
            {
                throw new IOException($"读取文件条目失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取所有时间索引
        /// </summary>
        /// <param name="timeIndexOffset">时间索引偏移量</param>
        /// <returns>时间索引列表</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">读取时间索引失败</exception>
        public List<PataTimeIndexEntry> ReadAllTimeIndices(uint timeIndexOffset)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (timeIndexOffset == 0)
            {
                return new List<PataTimeIndexEntry>();
            }

            try
            {
                // 跳转到时间索引表开始位置
                _FileStream.Position = timeIndexOffset;

                // 计算时间索引表大小
                var fileEntriesSize = Header.FileCount * PataFileEntry.ENTRY_SIZE;

                // 安全计算时间索引表条目数量
                long availableBytes = Math.Max(0, FileSize - timeIndexOffset - fileEntriesSize);
                long estimatedCount = availableBytes / PataTimeIndexEntry.ENTRY_SIZE;

                // 限制初始容量，防止整数溢出
                const int maxInitialCapacity = 1000000; // 设置一个合理的初始容量上限
                int initialCapacity = (int)Math.Min(estimatedCount, maxInitialCapacity);

                // 读取所有时间索引
                var indices = new List<PataTimeIndexEntry>(initialCapacity);

                // 使用可用字节数来控制读取，而不是预计算的数量
                long bytesRead = 0;
                while (bytesRead < availableBytes)
                {
                    // 确保不会读取超出文件末尾
                    if (_FileStream.Position >= FileSize)
                    {
                        break;
                    }

                    var entry = StreamHelper.ReadStructure<PataTimeIndexEntry>(_FileStream);
                    indices.Add(entry);
                    bytesRead += PataTimeIndexEntry.ENTRY_SIZE;
                }

                return indices;
            }
            catch (Exception ex)
            {
                throw new IOException($"读取时间索引失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取文件索引
        /// </summary>
        /// <param name="fileIndexOffset">文件索引偏移量</param>
        /// <param name="indexCount">索引数量</param>
        /// <returns>文件索引列表</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">读取文件索引失败</exception>
        public List<PataFileIndexEntry> ReadFileIndices(long fileIndexOffset, uint indexCount)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (indexCount == 0)
            {
                return new List<PataFileIndexEntry>();
            }

            try
            {
                // 跳转到文件索引表开始位置
                _FileStream.Position = fileIndexOffset;

                // 限制初始容量，防止整数溢出
                const int maxInitialCapacity = 1000000; // 设置一个合理的初始容量上限
                int initialCapacity = (int)Math.Min(indexCount, maxInitialCapacity);

                // 读取所有文件索引
                var indices = new List<PataFileIndexEntry>(initialCapacity);

                // 使用安全的读取方式
                long remainingBytes = Math.Min(indexCount * PataFileIndexEntry.ENTRY_SIZE, FileSize - fileIndexOffset);
                long bytesRead = 0;
                int count = 0;

                while (bytesRead < remainingBytes && count < indexCount)
                {
                    // 确保不会读取超出文件末尾
                    if (_FileStream.Position >= FileSize)
                    {
                        break;
                    }

                    var entry = StreamHelper.ReadStructure<PataFileIndexEntry>(_FileStream);
                    indices.Add(entry);
                    bytesRead += PataFileIndexEntry.ENTRY_SIZE;
                    count++;
                }

                return indices;
            }
            catch (Exception ex)
            {
                throw new IOException($"读取文件索引失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 设置文件位置
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">设置文件位置失败</exception>
        public void Seek(long position)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                _FileStream.Position = position;
            }
            catch (Exception ex)
            {
                throw new IOException($"设置文件位置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 关闭文件
        /// </summary>
        public void Close()
        {
            DisposeStreams();
            FilePath = null;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                DisposeStreams();
                _IsDisposed = true;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 释放流资源
        /// </summary>
        private void DisposeStreams()
        {
            if (_BinaryReader != null)
            {
                _BinaryReader.Dispose();
                _BinaryReader = null;
            }

            if (_FileStream != null)
            {
                _FileStream.Dispose();
                _FileStream = null;
            }
        }

        /// <summary>
        /// 检查对象是否已释放，如果已释放则抛出异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileReader));
            }
        }

        #endregion
    }
}
