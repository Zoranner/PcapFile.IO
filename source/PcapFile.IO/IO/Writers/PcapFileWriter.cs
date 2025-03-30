using System;
using System.Collections.Generic;
using System.IO;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件写入器，负责管理PCAP文件的创建、打开、写入和关闭操作
    /// </summary>
    /// <remarks>
    /// 注意：此类设计为单线程使用，不支持多线程并发写入。
    /// </remarks>
    internal class PcapFileWriter : IDisposable
    {
        #region 字段

        private FileStream _FileStream;
        private BinaryWriter _BinaryWriter;
        private bool _IsDisposed;

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前PCAP文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 获取当前文件大小
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
        /// 初始化PCAP文件写入器
        /// </summary>
        public PcapFileWriter()
        {
            // 默认构造函数
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 创建新的PCAP文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <exception cref="ArgumentException">文件路径为空或无效</exception>
        /// <exception cref="IOException">创建文件时发生错误</exception>
        public void Create(string filePath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            try
            {
                FilePath = filePath;
                _FileStream = StreamHelper.CreateFileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);
            }
            catch (Exception ex)
            {
                throw new IOException($"创建PCAP文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 打开现有的PCAP文件
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
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);
            }
            catch (Exception ex)
            {
                throw new IOException($"打开PCAP文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 写入PCAP文件头
        /// </summary>
        /// <param name="header">文件头信息</param>
        /// <exception cref="ArgumentNullException">header为null</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteHeader(PcapFileHeader header)
        {
            ThrowIfDisposed();

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(header.ToBytes());
            Header = header;
        }

        /// <summary>
        /// 读取PCAP文件头
        /// </summary>
        /// <returns>文件头信息</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件流未初始化</exception>
        public PcapFileHeader ReadHeader()
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            var originalPosition = _FileStream.Position;
            _FileStream.Position = 0;
            Header = StreamHelper.ReadStructure<PcapFileHeader>(_FileStream);
            _FileStream.Position = originalPosition;
            return Header;
        }

        /// <summary>
        /// 计算和更新校验和
        /// </summary>
        /// <param name="fileEntries">文件条目列表</param>
        /// <param name="timeIndices">时间索引列表</param>
        /// <param name="fileIndices">文件索引字典</param>
        /// <returns>更新后的文件头</returns>
        public PcapFileHeader CalculateAndUpdateChecksum(
            List<PataFileEntry> fileEntries,
            List<PataTimeIndexEntry> timeIndices,
            Dictionary<string, List<PataFileIndexEntry>> fileIndices
        )
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            // 准备计算校验和
            using var memoryStream = new MemoryStream();

            // 创建临时头部，校验和字段为0
            var updatedHeader = Header;
            updatedHeader.FileCount = (ushort)fileEntries.Count;
            updatedHeader.TotalIndexCount = 0;

            // 计算数据包总数
            foreach (var entry in fileEntries)
            {
                updatedHeader.TotalIndexCount += entry.IndexCount;
            }

            // 计算偏移量
            updatedHeader.FileEntryOffset = PcapFileHeader.HEADER_SIZE;
            updatedHeader.TimeIndexOffset =
                updatedHeader.FileEntryOffset + (uint)fileEntries.Count * PataFileEntry.ENTRY_SIZE;

            // 将校验和设置为0进行计算
            var headerBytes = updatedHeader.ToBytes();
            Array.Copy(BitConverter.GetBytes(0), 0, headerBytes, 24, 4);
            memoryStream.Write(headerBytes, 0, headerBytes.Length);

            // 写入文件条目
            foreach (var entry in fileEntries)
            {
                memoryStream.Write(entry.ToBytes(), 0, PataFileEntry.ENTRY_SIZE);
            }

            // 写入时间索引
            foreach (var entry in timeIndices)
            {
                memoryStream.Write(entry.ToBytes(), 0, PataTimeIndexEntry.ENTRY_SIZE);
            }

            // 写入文件索引
            foreach (var entry in fileEntries)
            {
                if (fileIndices.TryGetValue(entry.RelativePath, out var indexList))
                {
                    foreach (var indexEntry in indexList)
                    {
                        memoryStream.Write(indexEntry.ToBytes(), 0, PataFileIndexEntry.ENTRY_SIZE);
                    }
                }
            }

            // 计算CRC32校验和
            var bytes = memoryStream.ToArray();
            updatedHeader.Checksum = ChecksumCalculator.CalculateCrc32(bytes);

            // 更新头部
            Header = updatedHeader;

            return Header;
        }

        /// <summary>
        /// 一次性写入所有索引数据
        /// </summary>
        /// <param name="fileEntries">文件条目列表</param>
        /// <param name="timeIndices">时间索引列表</param>
        /// <param name="fileIndices">文件索引字典</param>
        public void WriteAllIndices(
            List<PataFileEntry> fileEntries,
            List<PataTimeIndexEntry> timeIndices,
            Dictionary<string, List<PataFileIndexEntry>> fileIndices
        )
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            try
            {
                // 首先计算和更新校验和
                var updatedHeader = CalculateAndUpdateChecksum(
                    fileEntries,
                    timeIndices,
                    fileIndices
                );

                // 定位到文件开头
                Seek(0);

                // 写入更新后的文件头
                WriteHeader(updatedHeader);

                // 写入文件条目表
                foreach (var entry in fileEntries)
                {
                    WriteFileEntry(entry);
                }

                // 写入时间范围索引表
                foreach (var entry in timeIndices)
                {
                    WriteTimeIndexEntry(entry);
                }

                // 写入文件索引表
                foreach (var entry in fileEntries)
                {
                    if (fileIndices.TryGetValue(entry.RelativePath, out var indexList))
                    {
                        foreach (var indexEntry in indexList)
                        {
                            WriteFileIndexEntry(indexEntry);
                        }
                    }
                }

                // 确保所有数据都写入磁盘
                Flush();
            }
            catch (Exception ex)
            {
                throw new IOException($"写入索引数据失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 写入索引条目
        /// </summary>
        /// <param name="entry">索引条目</param>
        /// <exception cref="ArgumentNullException">entry为null</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteIndexEntry(PataFileIndexEntry entry)
        {
            ThrowIfDisposed();

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(entry.ToBytes());
        }

        /// <summary>
        /// 刷新文件缓冲区
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void Flush()
        {
            ThrowIfDisposed();
            _BinaryWriter?.Flush();
        }

        /// <summary>
        /// 关闭文件管理器
        /// </summary>
        public void Close()
        {
            DisposeStreams();
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

        /// <summary>
        /// 写入文件条目
        /// </summary>
        /// <param name="entry">文件条目</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteFileEntry(PataFileEntry entry)
        {
            ThrowIfDisposed();

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(entry.ToBytes());
        }

        /// <summary>
        /// 写入时间索引条目
        /// </summary>
        /// <param name="entry">时间索引条目</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteTimeIndexEntry(PataTimeIndexEntry entry)
        {
            ThrowIfDisposed();

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(entry.ToBytes());
        }

        /// <summary>
        /// 写入文件索引条目
        /// </summary>
        /// <param name="entry">文件索引条目</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteFileIndexEntry(PataFileIndexEntry entry)
        {
            ThrowIfDisposed();

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(entry.ToBytes());
        }

        /// <summary>
        /// 设置文件位置
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void Seek(long position)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _FileStream.Position = position;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 检查对象是否已释放，如果已释放则抛出异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileWriter));
            }
        }

        /// <summary>
        /// 释放文件流和二进制写入器资源
        /// </summary>
        private void DisposeStreams()
        {
            if (_BinaryWriter != null)
            {
                _BinaryWriter.Dispose();
                _BinaryWriter = null;
            }

            if (_FileStream != null)
            {
                _FileStream.Dispose();
                _FileStream = null;
            }
        }

        #endregion
    }
}
