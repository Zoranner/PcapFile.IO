using System;
using System.Collections.Generic;
using System.IO;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PROJ文件写入器，负责PROJ工程文件的创建、打开、写入和关闭操作
    /// </summary>
    /// <remarks>
    /// 该类封装了PROJ文件的底层操作，提供了简单易用的接口。
    /// 注意：此类设计为单线程使用，不支持多线程并发写入。
    /// </remarks>
    internal class ProjFileWriter : IDisposable
    {
        #region 字段

        /// <summary>
        /// 文件流
        /// </summary>
        private FileStream _FileStream;

        /// <summary>
        /// 标记对象是否已被释放
        /// </summary>
        private bool _IsDisposed;

        #endregion

        #region 属性

        /// <summary>
        /// 获取文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 获取文件大小
        /// </summary>
        public long FileSize => _FileStream?.Length ?? 0;

        /// <summary>
        /// 获取当前文件头
        /// </summary>
        public ProjFileHeader Header { get; private set; }

        /// <summary>
        /// 获取文件是否已打开
        /// </summary>
        public bool IsOpen => _FileStream != null;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PROJ文件写入器的新实例
        /// </summary>
        public ProjFileWriter()
        {
            _FileStream = null;
            FilePath = string.Empty;
            _IsDisposed = false;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 创建PROJ文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功创建</returns>
        /// <exception cref="ArgumentException">文件路径为空</exception>
        /// <exception cref="IOException">创建文件时发生错误</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public bool Create(string filePath)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            try
            {
                // 创建目录
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 创建文件流
                _FileStream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.Read
                );

                // 保存文件路径
                FilePath = filePath;

                return true;
            }
            catch (Exception ex)
            {
                DisposeFileStream();
                throw new IOException($"创建文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 打开PROJ文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功打开</returns>
        /// <exception cref="ArgumentException">文件路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">打开文件时发生错误</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public bool Open(string filePath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PROJ文件不存在", filePath);
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
                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"打开PROJ文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 写入文件头
        /// </summary>
        /// <param name="header">文件头</param>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">写入文件时发生错误</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteHeader(ProjFileHeader header)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _FileStream.Seek(0, SeekOrigin.Begin);
            _FileStream.Write(header.ToBytes(), 0, ProjFileHeader.HEADER_SIZE);
            Header = header;
        }

        /// <summary>
        /// 读取文件头
        /// </summary>
        /// <returns>文件头</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">读取文件时发生错误</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public ProjFileHeader ReadHeader()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                // 定位到文件头
                _FileStream.Seek(0, SeekOrigin.Begin);

                // 读取文件头
                Header = StreamHelper.ReadStructure<ProjFileHeader>(_FileStream);

                return Header;
            }
            catch (Exception ex)
            {
                throw new IOException($"读取文件头失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 计算并更新文件头的校验和
        /// </summary>
        /// <param name="fileEntries">文件条目集合</param>
        /// <param name="timeIndices">时间索引集合</param>
        /// <param name="totalPacketCount">总数据包数量</param>
        /// <returns>更新后的文件头</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">更新文件时发生错误</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public ProjFileHeader CalculateAndUpdateChecksum(
            List<PataFileEntry> fileEntries,
            List<PataTimeIndexEntry> timeIndices,
            uint totalPacketCount
        )
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            // 更新文件头
            var updatedHeader = Header;
            updatedHeader.FileCount = (ushort)fileEntries.Count;
            updatedHeader.TotalIndexCount = totalPacketCount;
            updatedHeader.FileEntryOffset = ProjFileHeader.HEADER_SIZE;

            // 计算偏移量
            updatedHeader.TimeIndexOffset =
                updatedHeader.FileEntryOffset + (uint)fileEntries.Count * PataFileEntry.ENTRY_SIZE;

            // 将校验和设置为0进行计算
            var headerBytes = updatedHeader.ToBytes();
            Array.Copy(BitConverter.GetBytes(0), 0, headerBytes, 24, 4);
            _FileStream.Seek(0, SeekOrigin.Begin);
            _FileStream.Write(headerBytes, 0, headerBytes.Length);

            // 写入文件条目
            foreach (var entry in fileEntries)
            {
                _FileStream.Write(entry.ToBytes(), 0, PataFileEntry.ENTRY_SIZE);
            }

            // 写入时间索引
            foreach (var entry in timeIndices)
            {
                _FileStream.Write(entry.ToBytes(), 0, PataTimeIndexEntry.ENTRY_SIZE);
            }

            // 计算CRC32校验和
            var bytes = new byte[
                updatedHeader.FileEntryOffset
                    + updatedHeader.TotalIndexCount * PataFileEntry.ENTRY_SIZE
            ];
            _FileStream.Seek(0, SeekOrigin.Begin);
            _FileStream.Read(bytes, 0, bytes.Length);
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
                    (uint)fileEntries.Count
                );

                // 定位到文件开头
                _FileStream.Seek(0, SeekOrigin.Begin);

                // 写入更新后的文件头
                WriteHeader(updatedHeader);

                // 写入文件条目表
                foreach (var entry in fileEntries)
                {
                    _FileStream.Write(entry.ToBytes(), 0, PataFileEntry.ENTRY_SIZE);
                }

                // 写入时间范围索引表
                foreach (var entry in timeIndices)
                {
                    _FileStream.Write(entry.ToBytes(), 0, PataTimeIndexEntry.ENTRY_SIZE);
                }

                // 写入文件索引表
                foreach (var entry in fileEntries)
                {
                    if (fileIndices.TryGetValue(entry.RelativePath, out var indexList))
                    {
                        foreach (var indexEntry in indexList)
                        {
                            _FileStream.Write(
                                indexEntry.ToBytes(),
                                0,
                                PataFileIndexEntry.ENTRY_SIZE
                            );
                        }
                    }
                }

                // 确保所有数据都写入磁盘
                _FileStream.Flush();
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

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _FileStream.Write(entry.ToBytes(), 0, PataFileIndexEntry.ENTRY_SIZE);
        }

        /// <summary>
        /// 刷新文件缓冲区
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void Flush()
        {
            ThrowIfDisposed();
            _FileStream?.Flush();
        }

        /// <summary>
        /// 关闭PROJ文件
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void Close()
        {
            DisposeFileStream();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                DisposeFileStream();
                _IsDisposed = true;
            }
            GC.SuppressFinalize(this);
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
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ProjFileWriter));
            }
        }

        /// <summary>
        /// 释放文件流
        /// </summary>
        private void DisposeFileStream()
        {
            if (_FileStream != null)
            {
                _FileStream.Dispose();
                _FileStream = null;
            }
        }

        #endregion
    }
}
