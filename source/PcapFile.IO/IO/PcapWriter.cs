using System;
using System.Collections.Generic;
using System.IO;
using KimoTech.PcapFile.IO.Configuration;
using KimoTech.PcapFile.IO.Extensions;
using KimoTech.PcapFile.IO.Interfaces;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;
using KimoTech.PcapFile.IO.Writers;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件写入器，提供创建和写入PCAP文件的功能
    /// </summary>
    /// <remarks>
    /// 该类负责管理工程文件和数据文件的写入操作
    /// </remarks>
    public class PcapWriter : IPcapWriter
    {
        #region 私有字段

        /// <summary>
        /// 工程文件写入器，负责管理PROJ工程文件的创建、打开、写入和关闭操作
        /// </summary>
        private readonly ProjFileWriter _ProjFileWriter;

        /// <summary>
        /// PCAP文件写入器，负责管理PCAP数据文件的创建、打开、写入和关闭操作
        /// </summary>
        private readonly PcapFileWriter _PcapFileWriter;

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
        /// 当前文件ID，从1开始计数
        /// </summary>
        private uint _CurrentFileId;

        /// <summary>
        /// 最后一个数据包的时间戳
        /// </summary>
        private long _LastPacketTimestamp;

        /// <summary>
        /// 最后索引的时间戳
        /// </summary>
        private long _LastIndexedTimestamp;

        /// <summary>
        /// PCAP文件条目列表
        /// </summary>
        private List<PcapFileEntry> _FileEntries;

        /// <summary>
        /// 时间索引列表
        /// </summary>
        private List<PcapTimeIndexEntry> _TimeIndices;

        /// <summary>
        /// 文件索引列表
        /// </summary>
        private Dictionary<string, List<PcapFileIndexEntry>> _FileIndices;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PCAP文件写入器
        /// </summary>
        /// <remarks>
        /// 构造函数会创建PROJ和PCAP文件写入器的实例。
        /// 成功调用Create或Open方法后，才能进行文件写入操作。
        /// </remarks>
        public PcapWriter()
        {
            _ProjFileWriter = new ProjFileWriter();
            _PcapFileWriter = new PcapFileWriter();
            _IsDisposed = false;
            _CurrentFileId = 0;
        }

        #endregion

        #region 属性

        /// <inheritdoc />
        public long PacketCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 计算总文件大小时会包含：
        /// 1. PROJ索引文件的大小
        /// 2. PCAP数据目录下所有数据文件的大小总和
        /// </remarks>
        public long FileSize
        {
            get
            {
                ThrowIfDisposed();
                return _ProjFileWriter.FileSize + _TotalSize;
            }
        }

        /// <inheritdoc />
        public string FilePath => _ProjFileWriter.FilePath;

        /// <inheritdoc />
        /// <remarks>
        /// 设置为true时，每次写入数据包后会自动刷新文件缓冲区。
        /// 设置为false时，需要手动调用Flush方法来刷新缓冲区。
        /// </remarks>
        public bool AutoFlush { get; set; }

        /// <inheritdoc />
        public bool IsOpen => _ProjFileWriter.IsOpen;

        #endregion

        #region 核心方法

        /// <summary>
        /// 创建新的工程文件
        /// </summary>
        /// <param name="filePath">工程文件路径</param>
        /// <param name="header">工程文件头</param>
        /// <returns>是否成功创建</returns>
        /// <exception cref="ArgumentNullException">文件路径为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public bool Create(string filePath, ProjFileHeader header = default)
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
                    var pcapDirectory = PathHelper.GetPcapDirectoryPath(filePath);
                    Directory.CreateDirectory(pcapDirectory);
                }

                // 创建PROJ索引文件
                _ProjFileWriter.Create(filePath);

                // 初始化PCAP数据文件写入器
                _PcapFileWriter.Initialize(filePath);

                // 写入文件头
                if (header.MagicNumber == 0)
                {
                    header = ProjFileHeader.Create(0, 0); // FileCount=0, TotalIndexCount=0
                    header.MagicNumber = FileVersionConfig.PROJ_MAGIC_NUMBER;
                    header.MajorVersion = FileVersionConfig.MAJOR_VERSION;
                    header.MinorVersion = FileVersionConfig.MINOR_VERSION;
                    header.FileEntryOffset = ProjFileHeader.HEADER_SIZE;
                    header.IndexInterval = FileVersionConfig.DEFAULT_INDEX_INTERVAL;
                    header.TimeIndexOffset = 0; // 初始为0，后续更新
                    header.Checksum = 0; // 初始为0，关闭时计算
                }

                _ProjFileWriter.WriteHeader(header);

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

        /// <summary>
        /// 打开现有工程文件
        /// </summary>
        /// <param name="filePath">工程文件路径</param>
        /// <returns>是否成功打开</returns>
        /// <exception cref="ArgumentNullException">文件路径为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
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
                // 打开PROJ索引文件
                _ProjFileWriter.Open(filePath);

                // 初始化PCAP数据文件写入器
                _PcapFileWriter.Initialize(filePath);

                // 读取文件头
                var header = _ProjFileWriter.ReadHeader();
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

        /// <summary>
        /// 关闭工程文件
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void Close()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                return;
            }

            try
            {
                // 确保当前PCAP文件操作完成
                _PcapFileWriter.Flush();
                _PcapFileWriter.Close();

                // 如果没有写入任何数据，直接关闭
                if (!_FirstPacketWritten || _FileEntries.Count == 0)
                {
                    _ProjFileWriter.Close();
                    PacketCount = 0;
                    return;
                }

                // 更新所有文件条目的索引计数
                UpdateFileEntryIndexCounts();

                // 一次性写入所有索引数据
                _ProjFileWriter.WriteAllIndices(_FileEntries, _TimeIndices, _FileIndices);

                // 关闭PROJ索引文件
                _ProjFileWriter.Close();
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

        /// <summary>
        /// 写入数据包
        /// </summary>
        /// <param name="packet">数据包</param>
        /// <returns>是否成功写入</returns>
        /// <exception cref="ArgumentNullException">数据包为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        /// <exception cref="InvalidOperationException">文件未打开时抛出</exception>
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

                // 检查是否需要创建新PCAP文件
                CheckAndCreateNewFile(packet);

                // 写入数据包并获取偏移量
                var fileOffset = _PcapFileWriter.WritePacket(packet);

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

        /// <summary>
        /// 刷新缓冲区
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        /// <exception cref="InvalidOperationException">文件未打开时抛出</exception>
        public void Flush()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            _PcapFileWriter.Flush();
            _ProjFileWriter.Flush();
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
                _ProjFileWriter.Seek(offset);
                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"设置文件位置失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 重置内部状态
        /// </summary>
        private void ResetState()
        {
            PacketCount = 0;
            _TotalSize = 0;
            _FileEntries = new List<PcapFileEntry>();
            _TimeIndices = new List<PcapTimeIndexEntry>();
            _FileIndices = new Dictionary<string, List<PcapFileIndexEntry>>();
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

                _FileEntries[i] = PcapFileEntry.Create(
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
            // 创建第一个PCAP文件，使用第一个数据包的时间戳命名
            var pcapFilePath = _PcapFileWriter.CreateDataFile(packet.CaptureTime);

            // 创建新的文件条目
            _CurrentFileId = 1;
            var fileEntry = PcapFileEntry.Create(
                _CurrentFileId,
                Path.GetFileName(pcapFilePath),
                packet.Header.Timestamp,
                packet.Header.Timestamp,
                0
            );

            // 添加到内存中的文件条目列表
            _FileEntries.Add(fileEntry);
            _FirstPacketWritten = true;

            // 创建索引字典
            _FileIndices[fileEntry.RelativePath] = new List<PcapFileIndexEntry>();
        }

        /// <summary>
        /// 检查是否需要创建新PCAP文件
        /// </summary>
        private bool ShouldCreateNewFile()
        {
            return _PcapFileWriter.CurrentPacketCount >= _PcapFileWriter.MaxPacketsPerFile;
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
        /// 创建新的PCAP数据文件
        /// </summary>
        private void CreateNewFile(DataPacket packet)
        {
            // 获取当前文件条目
            var currentEntry = _FileEntries[_CurrentFileId - 1];

            // 更新当前文件的结束时间戳
            _FileEntries[_CurrentFileId - 1] = PcapFileEntry.Create(
                currentEntry.FileId,
                currentEntry.RelativePath,
                currentEntry.StartTimestamp,
                _LastPacketTimestamp,
                (uint)_FileIndices[currentEntry.RelativePath].Count
            );

            // 先关闭当前PCAP文件，确保小周期闭环
            _PcapFileWriter.Flush();
            _PcapFileWriter.Close();

            // 创建新文件，使用当前数据包的时间戳命名
            var newPcapFilePath = _PcapFileWriter.CreateDataFile(packet.CaptureTime);

            // 创建新的文件条目
            _CurrentFileId++;
            var newFileEntry = PcapFileEntry.Create(
                _CurrentFileId,
                Path.GetFileName(newPcapFilePath),
                packet.Header.Timestamp,
                packet.Header.Timestamp,
                0
            );

            // 添加到内存中的文件条目列表
            _FileEntries.Add(newFileEntry);

            // 创建新文件的索引列表
            _FileIndices[newFileEntry.RelativePath] = new List<PcapFileIndexEntry>();
        }

        /// <summary>
        /// 更新索引和统计信息
        /// </summary>
        private void UpdateIndices(DataPacket packet, long fileOffset)
        {
            // 创建并保存文件索引条目
            var currentFileEntry = _FileEntries[_CurrentFileId - 1];
            var indexEntry = PcapFileIndexEntry.Create(packet.Header.Timestamp, fileOffset);

            // 添加到当前文件的索引列表
            var relativePath = currentFileEntry.RelativePath;
            _FileIndices[relativePath].Add(indexEntry);

            // 更新时间范围索引
            var indexInterval = _ProjFileWriter.Header.IndexInterval;
            var packetTimestamp = packet.Header.Timestamp;

            // 如果时间差超过索引间隔，或者是第一个数据包，则创建时间索引
            if (
                _LastIndexedTimestamp == 0
                || packetTimestamp - _LastIndexedTimestamp >= indexInterval
            )
            {
                var timeIndexEntry = PcapTimeIndexEntry.Create(
                    _CurrentFileId,
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
            _FileEntries[_CurrentFileId - 1] = PcapFileEntry.Create(
                entryToUpdate.FileId,
                entryToUpdate.RelativePath,
                entryToUpdate.StartTimestamp,
                packet.Header.Timestamp,
                (uint)_FileIndices[entryToUpdate.RelativePath].Count
            );
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                // 1. 关闭PROJ文件写入器
                // 2. 释放PCAP文件写入器的资源
                
                // ... existing code ...

                _PcapFileWriter?.Dispose();
                
                // ... existing code ...
            }

            _IsDisposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~PcapWriter()
        {
            Dispose(false);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 释放流资源
        /// </summary>
        private void DisposeStreams()
        {
            _PcapFileWriter?.Dispose();
            _ProjFileWriter?.Dispose();
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
