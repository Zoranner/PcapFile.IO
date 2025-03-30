using System;
using System.Collections.Generic;
using System.IO;
using KimoTech.PcapFile.IO.Configuration;
using KimoTech.PcapFile.IO.Interfaces;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP数据写入器，提供PCAP文件的创建、打开、写入和关闭操作
    /// </summary>
    /// <remarks>
    /// 该类封装了PCAP文件的底层操作，提供了简单易用的接口。
    /// 注意：此类设计为单线程使用，不支持多线程并发写入。
    /// 如需在异步环境中使用，请考虑在后台线程中调用此类的方法。
    /// </remarks>
    public sealed class PcapWriter : IPcapWriter
    {
        #region 字段

        /// <summary>
        /// PCAP文件写入器，负责管理PCAP索引文件的创建、打开、写入和关闭操作
        /// </summary>
        private readonly PcapFileWriter _PcapFileWriter;

        /// <summary>
        /// PATA文件写入器，负责管理PATA数据文件的创建、打开、写入和关闭操作
        /// </summary>
        private readonly PataFileWriter _PataFileWriter;

        /// <summary>
        /// 标记对象是否已被释放
        /// </summary>
        private bool _IsDisposed;

        /// <summary>
        /// 当前写入的数据包总大小
        /// </summary>
        private long _TotalSize;

        /// <summary>
        /// 标记是否已写入第一个数据包
        /// </summary>
        private bool _FirstPacketWritten;

        /// <summary>
        /// 当前文件ID
        /// </summary>
        private int _CurrentFileId;

        /// <summary>
        /// 最后一个数据包的时间戳
        /// </summary>
        private long _LastPacketTimestamp;

        /// <summary>
        /// 最后索引的时间戳
        /// </summary>
        private long _LastIndexedTimestamp;

        /// <summary>
        /// PATA文件条目列表
        /// </summary>
        private List<PataFileEntry> _FileEntries;

        /// <summary>
        /// 时间索引列表
        /// </summary>
        private List<PataTimeIndexEntry> _TimeIndices;

        /// <summary>
        /// 文件索引字典，键为相对路径，值为索引列表
        /// </summary>
        private Dictionary<string, List<PataFileIndexEntry>> _FileIndices;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PCAP写入器的新实例
        /// </summary>
        /// <remarks>
        /// 构造函数会创建PCAP和PATA文件写入器的实例。
        /// 这些实例会在对象释放时自动释放。
        /// </remarks>
        public PcapWriter()
        {
            _PcapFileWriter = new PcapFileWriter();
            _PataFileWriter = new PataFileWriter();
        }

        #endregion

        #region 属性

        /// <inheritdoc />
        public long PacketCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 计算总文件大小时会包含：
        /// 1. PCAP索引文件的大小
        /// 2. PATA数据目录下所有数据文件的大小总和
        /// </remarks>
        public long FileSize
        {
            get
            {
                ThrowIfDisposed();
                return _PcapFileWriter.FileSize + _TotalSize;
            }
        }

        /// <inheritdoc />
        public string FilePath => _PcapFileWriter.FilePath;

        /// <inheritdoc />
        /// <remarks>
        /// 设置为true时，每次写入数据包后会自动刷新文件缓冲区。
        /// 设置为false时，需要手动调用Flush方法来刷新缓冲区。
        /// </remarks>
        public bool AutoFlush { get; set; }

        /// <inheritdoc />
        public bool IsOpen => _PcapFileWriter.IsOpen;

        #endregion

        #region 公共方法

        /// <inheritdoc />
        /// <remarks>
        /// 创建新文件时会：
        /// 1. 创建必要的目录结构
        /// 2. 创建PCAP索引文件
        /// 3. 初始化PATA数据文件写入器
        /// 4. 写入默认的文件头信息
        /// </remarks>
        public bool Create(string filePath, PcapFileHeader header = default)
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
                    var pataDirectory = PathHelper.GetPataDirectoryPath(filePath);
                    Directory.CreateDirectory(pataDirectory);
                }

                // 创建PCAP索引文件
                _PcapFileWriter.Create(filePath);

                // 初始化PATA数据文件写入器
                _PataFileWriter.Initialize(filePath);

                // 写入文件头
                if (header.MagicNumber == 0)
                {
                    header = PcapFileHeader.Create(0, 0); // FileCount=0, TotalIndexCount=0
                    header.MagicNumber = FileVersionConfig.PCAP_MAGIC_NUMBER;
                    header.MajorVersion = FileVersionConfig.MAJOR_VERSION;
                    header.MinorVersion = FileVersionConfig.MINOR_VERSION;
                    header.FileEntryOffset = PcapFileHeader.HEADER_SIZE;
                    header.IndexInterval = FileVersionConfig.DEFAULT_INDEX_INTERVAL;
                    header.TimeIndexOffset = 0; // 初始为0，后续更新
                    header.Checksum = 0; // 初始为0，关闭时计算
                }

                _PcapFileWriter.WriteHeader(header);

                // 初始化内存数据结构
                ResetState();
                AutoFlush = true;

                return true;
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"创建文件失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 打开现有文件时会：
        /// 1. 验证文件是否存在
        /// 2. 打开PCAP索引文件
        /// 3. 初始化PATA数据文件写入器
        /// 4. 读取文件头信息以获取数据包计数
        /// </remarks>
        public bool Open(string filePath)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            try
            {
                // 打开PCAP索引文件
                _PcapFileWriter.Open(filePath);

                // 初始化PATA数据文件写入器
                _PataFileWriter.Initialize(filePath);

                // 读取文件头
                var header = _PcapFileWriter.ReadHeader();
                PacketCount = header.TotalIndexCount;

                // 初始化内存数据结构
                ResetState();
                AutoFlush = true;

                return true;
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"打开文件失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 关闭文件时会：
        /// 1. 关闭PATA数据文件
        /// 2. 更新PCAP索引文件的头信息和索引
        /// 3. 关闭PCAP索引文件
        /// 4. 重置数据包计数
        /// </remarks>
        public void Close()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                return;
            }

            try
            {
                // 确保当前PATA文件操作完成
                _PataFileWriter.Flush();
                _PataFileWriter.Close();

                // 如果没有写入任何数据，直接关闭
                if (!_FirstPacketWritten || _FileEntries.Count == 0)
                {
                    _PcapFileWriter.Close();
                    PacketCount = 0;
                    return;
                }

                // 更新所有文件条目的索引计数
                UpdateFileEntryIndexCounts();

                // 一次性写入所有索引数据
                _PcapFileWriter.WriteAllIndices(_FileEntries, _TimeIndices, _FileIndices);

                // 关闭PCAP索引文件
                _PcapFileWriter.Close();
            }
            catch (Exception ex)
            {
                try
                {
                    DisposeStreams();
                }
                catch
                {
                    /* 忽略释放过程中的异常 */
                }

                throw new IOException($"关闭文件失败: {ex.Message}", ex);
            }
            finally
            {
                PacketCount = 0;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 写入数据包时会：
        /// 1. 根据需要创建并切换PATA数据文件
        /// 2. 将数据包写入PATA文件
        /// 3. 更新索引和统计信息
        /// 4. 如果启用了自动刷新，则刷新文件缓冲区
        /// 注意：此类不支持并发写入，所有写入操作应在单一线程中进行
        /// </remarks>
        public bool WritePacket(DataPacket packet)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            try
            {
                // 首次写入数据包检查
                if (!_FirstPacketWritten)
                {
                    InitializeFirstPacket(packet);
                }

                // 检查是否需要创建新PATA文件
                CheckAndCreateNewFile(packet);

                // 写入数据包并获取偏移量
                var fileOffset = _PataFileWriter.WritePacket(packet);

                // 更新索引和统计信息
                UpdateIndices(packet, fileOffset);

                if (AutoFlush)
                {
                    Flush();
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"写入数据包失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 批量写入数据包时会：
        /// 1. 遍历数据包集合
        /// 2. 逐个写入数据包
        /// 3. 如果任何一个数据包写入失败，则立即返回false
        /// </remarks>
        public bool WritePackets(IEnumerable<DataPacket> packets)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (packets == null)
            {
                throw new ArgumentNullException(nameof(packets));
            }

            try
            {
                foreach (var packet in packets)
                {
                    if (!WritePacket(packet))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"批量写入数据包失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 刷新文件缓冲区时会：
        /// 1. 刷新PATA文件的缓冲区
        /// 2. 刷新PCAP文件的缓冲区
        /// </remarks>
        public void Flush()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            _PataFileWriter.Flush();
            _PcapFileWriter.Flush();
        }

        /// <inheritdoc />
        /// <remarks>
        /// 将当前位置设置到指定偏移量
        /// </remarks>
        public bool Seek(long offset)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                _PcapFileWriter.Seek(offset);
                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"设置文件位置失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 重置内部状态
        /// </summary>
        private void ResetState()
        {
            PacketCount = 0;
            _TotalSize = 0;
            _FileEntries = new List<PataFileEntry>();
            _TimeIndices = new List<PataTimeIndexEntry>();
            _FileIndices = new Dictionary<string, List<PataFileIndexEntry>>();
            _FirstPacketWritten = false;
            _LastPacketTimestamp = 0;
            _LastIndexedTimestamp = 0;
            _CurrentFileId = 0;
        }

        /// <summary>
        /// 更新文件条目的索引计数
        /// </summary>
        private void UpdateFileEntryIndexCounts()
        {
            for (var i = 0; i < _FileEntries.Count; i++)
            {
                var entry = _FileEntries[i];
                var indexCount = _FileIndices.TryGetValue(entry.RelativePath, out var indices)
                    ? (uint)indices.Count
                    : 0;

                _FileEntries[i] = PataFileEntry.Create(
                    entry.FileId,
                    entry.RelativePath,
                    entry.StartTimestamp,
                    entry.EndTimestamp,
                    indexCount
                );
            }
        }

        /// <summary>
        /// 初始化首个数据包处理
        /// </summary>
        private void InitializeFirstPacket(DataPacket packet)
        {
            // 创建第一个PATA文件，使用第一个数据包的时间戳命名
            var pataFilePath = _PataFileWriter.CreateDataFile(packet.Timestamp);

            // 创建新的文件条目
            _CurrentFileId = 1;
            var fileEntry = PataFileEntry.Create(
                (uint)_CurrentFileId,
                Path.GetFileName(pataFilePath),
                packet.Header.Timestamp,
                packet.Header.Timestamp,
                0
            );

            // 添加到内存中的文件条目列表
            _FileEntries.Add(fileEntry);
            _FirstPacketWritten = true;

            // 创建索引字典
            _FileIndices[fileEntry.RelativePath] = new List<PataFileIndexEntry>();
        }

        /// <summary>
        /// 检查是否需要创建新PATA文件
        /// </summary>
        private bool ShouldCreateNewFile()
        {
            return _PataFileWriter.CurrentPacketCount >= _PataFileWriter.MaxPacketsPerFile;
        }

        /// <summary>
        /// 检查并创建新文件（如果需要）
        /// </summary>
        private void CheckAndCreateNewFile(DataPacket packet)
        {
            if (ShouldCreateNewFile())
            {
                CreateNewFile(packet);
            }
        }

        /// <summary>
        /// 创建新的PATA数据文件
        /// </summary>
        private void CreateNewFile(DataPacket packet)
        {
            // 获取当前文件条目
            var currentEntry = _FileEntries[_CurrentFileId - 1];

            // 更新当前文件的结束时间戳
            _FileEntries[_CurrentFileId - 1] = PataFileEntry.Create(
                currentEntry.FileId,
                currentEntry.RelativePath,
                currentEntry.StartTimestamp,
                _LastPacketTimestamp,
                (uint)_FileIndices[currentEntry.RelativePath].Count
            );

            // 先关闭当前PATA文件，确保小周期闭环
            _PataFileWriter.Flush();
            _PataFileWriter.Close();

            // 创建新文件，使用当前数据包的时间戳命名
            var newPataFilePath = _PataFileWriter.CreateDataFile(packet.Timestamp);

            // 创建新的文件条目
            _CurrentFileId++;
            var newFileEntry = PataFileEntry.Create(
                (uint)_CurrentFileId,
                Path.GetFileName(newPataFilePath),
                packet.Header.Timestamp,
                packet.Header.Timestamp,
                0
            );

            // 添加到内存中的文件条目列表
            _FileEntries.Add(newFileEntry);

            // 创建新文件的索引列表
            _FileIndices[newFileEntry.RelativePath] = new List<PataFileIndexEntry>();
        }

        /// <summary>
        /// 更新索引和统计信息
        /// </summary>
        private void UpdateIndices(DataPacket packet, long fileOffset)
        {
            // 创建并保存文件索引条目
            var currentFileEntry = _FileEntries[_CurrentFileId - 1];
            var indexEntry = PataFileIndexEntry.Create(packet.Header.Timestamp, fileOffset);

            // 添加到当前文件的索引列表
            var relativePath = currentFileEntry.RelativePath;
            _FileIndices[relativePath].Add(indexEntry);

            // 更新时间范围索引
            var indexInterval = _PcapFileWriter.Header.IndexInterval;
            var packetTimestamp = packet.Header.Timestamp;

            // 如果时间差超过索引间隔，或者是第一个数据包，则创建时间索引
            if (
                _LastIndexedTimestamp == 0
                || packetTimestamp - _LastIndexedTimestamp >= indexInterval
            )
            {
                var timeIndexEntry = PataTimeIndexEntry.Create(
                    (uint)_CurrentFileId,
                    packetTimestamp
                );
                _TimeIndices.Add(timeIndexEntry);
                _LastIndexedTimestamp = packetTimestamp;
            }

            // 更新统计信息
            _TotalSize += packet.TotalSize;
            PacketCount++;
            _LastPacketTimestamp = packet.Header.Timestamp;

            // 更新文件条目的结束时间戳
            var entryToUpdate = _FileEntries[_CurrentFileId - 1];
            _FileEntries[_CurrentFileId - 1] = PataFileEntry.Create(
                entryToUpdate.FileId,
                entryToUpdate.RelativePath,
                entryToUpdate.StartTimestamp,
                packet.Header.Timestamp,
                (uint)_FileIndices[entryToUpdate.RelativePath].Count
            );
        }

        /// <inheritdoc />
        /// <remarks>
        /// 释放资源时会：
        /// 1. 尝试关闭文件
        /// 2. 释放PATA文件写入器的资源
        /// 3. 释放PCAP文件写入器的资源
        /// 4. 标记对象为已释放状态
        /// </remarks>
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                try
                {
                    // 如果文件仍然打开，先关闭
                    if (IsOpen)
                    {
                        Close();
                    }
                }
                finally
                {
                    DisposeStreams();
                    _IsDisposed = true;
                }
            }
        }

        /// <summary>
        /// 释放流资源
        /// </summary>
        private void DisposeStreams()
        {
            _PataFileWriter?.Dispose();
            _PcapFileWriter?.Dispose();
        }

        /// <summary>
        /// 检查对象是否已释放，如果已释放则抛出异常
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <remarks>
        /// 此方法用于确保在对象被释放后不会继续使用。
        /// 所有公共方法都应该在开始时调用此方法。
        /// </remarks>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapWriter));
            }
        }

        #endregion
    }
}
