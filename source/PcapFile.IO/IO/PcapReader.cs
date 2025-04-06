using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Interfaces;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;
using KimoTech.PcapFile.IO.Extensions;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP数据读取器，提供PCAP文件的打开、读取和关闭操作
    /// </summary>
    /// <remarks>
    /// 该类封装了PCAP文件的底层操作，提供了简单易用的接口。
    /// 支持顺序读取、随机访问和时间范围查询等功能。
    /// </remarks>
    public sealed class PcapReader : IPcapReader
    {
        #region 字段

        /// <summary>
        /// PCAP文件读取器，负责管理PCAP索引文件的读取
        /// </summary>
        private readonly PcapFileReader _PcapFileReader;

        /// <summary>
        /// PATA文件读取器，负责管理PATA数据文件的读取
        /// </summary>
        private readonly PataFileReader _PataFileReader;

        /// <summary>
        /// 标记对象是否已被释放
        /// </summary>
        private bool _IsDisposed;

        /// <summary>
        /// 标记是否已读取第一个数据包
        /// </summary>
        private bool _FirstPacketRead;

        /// <summary>
        /// 当前文件ID
        /// </summary>
        private int _CurrentFileId;

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
        private readonly Dictionary<string, List<PataFileIndexEntry>> _FileIndices;

        /// <summary>
        /// 索引时间间隔（毫秒）
        /// </summary>
        private ushort _IndexInterval;

        /// <summary>
        /// 文件头
        /// </summary>
        private PcapFileHeader _Header;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PCAP读取器的新实例
        /// </summary>
        public PcapReader()
        {
            _PcapFileReader = new PcapFileReader();
            _PataFileReader = new PataFileReader();
            _FileEntries = new List<PataFileEntry>();
            _TimeIndices = new List<PataTimeIndexEntry>();
            _FileIndices = new Dictionary<string, List<PataFileIndexEntry>>();
            _IsDisposed = false;
            _CurrentFileId = 0;
        }

        #endregion

        #region 属性

        /// <inheritdoc />
        public long PacketCount { get; private set; }

        /// <inheritdoc />
        public long CurrentPosition { get; private set; }

        /// <inheritdoc />
        public DateTime StartTime { get; private set; }

        /// <inheritdoc />
        public DateTime EndTime { get; private set; }

        /// <inheritdoc />
        public string FilePath => _PcapFileReader.FilePath;

        /// <inheritdoc />
        public bool IsOpen => _PcapFileReader.IsOpen;

        #endregion

        #region 公共方法

        /// <inheritdoc />
        public bool Open(string filePath)
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
                // 打开PCAP工程文件
                _PcapFileReader.Open(filePath);

                // 读取PCAP文件头
                var header = _PcapFileReader.ReadHeader();
                _Header = header;
                _IndexInterval = header.IndexInterval;
                PacketCount = header.TotalIndexCount;

                // 加载文件条目表
                _FileEntries = _PcapFileReader.ReadAllFileEntries(header.FileCount);

                // 加载时间索引表
                _TimeIndices = _PcapFileReader.ReadAllTimeIndices(header.TimeIndexOffset);

                // 初始化时间范围
                if (_FileEntries.Count > 0)
                {
                    var firstEntry = _FileEntries[0];
                    var lastEntry = _FileEntries[^1];
                    StartTime = DateTimeExtensions.FromUnixTimeMilliseconds(firstEntry.StartTimestamp);
                    EndTime = DateTimeExtensions.FromUnixTimeMilliseconds(lastEntry.EndTimestamp);
                }

                // 初始化状态
                _FirstPacketRead = false;
                CurrentPosition = 0;

                return true;
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"打开文件失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            ThrowIfDisposed();

            try
            {
                // 关闭PATA数据文件
                _PataFileReader?.Close();

                // 关闭PCAP工程文件
                _PcapFileReader?.Close();

                // 清空状态
                ResetState();
            }
            catch (Exception ex)
            {
                throw new IOException($"关闭文件失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public DataPacket ReadNextPacket()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            // 首次读取检查
            if (!_FirstPacketRead)
            {
                // 初始化第一个PATA文件
                if (_FileEntries.Count > 0)
                {
                    OpenCurrentFile(0);
                    _FirstPacketRead = true;
                }
                else
                {
                    return null; // 没有数据文件
                }
            }

            // 从当前PATA文件读取数据包
            var packet = _PataFileReader.ReadPacket();
            if (packet != null)
            {
                CurrentPosition++;
                return packet;
            }

            // 如果当前文件读取完毕，尝试切换到下一个文件
            if (_CurrentFileId < _FileEntries.Count - 1)
            {
                // 关闭当前文件
                _PataFileReader.Close();

                // 打开下一个文件
                OpenCurrentFile(_CurrentFileId + 1);

                // 继续读取
                return ReadNextPacket();
            }

            // 如果已经是最后一个文件，返回null表示读取结束
            return null;
        }

        /// <inheritdoc />
        public async Task<DataPacket> ReadNextPacketAsync(
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            // 首次读取检查
            if (!_FirstPacketRead)
            {
                // 初始化第一个PATA文件
                if (_FileEntries.Count > 0)
                {
                    await OpenCurrentFileAsync(0, cancellationToken);
                    _FirstPacketRead = true;
                }
                else
                {
                    return null; // 没有数据文件
                }
            }

            // 从当前PATA文件读取数据包
            var packet = await _PataFileReader.ReadPacketAsync(cancellationToken);
            if (packet != null)
            {
                CurrentPosition++;
                return packet;
            }

            // 如果当前文件读取完毕，尝试切换到下一个文件
            if (_CurrentFileId < _FileEntries.Count - 1)
            {
                // 关闭当前文件
                await _PataFileReader.CloseAsync(cancellationToken);

                // 打开下一个文件
                await OpenCurrentFileAsync(_CurrentFileId + 1, cancellationToken);

                // 继续读取
                return await ReadNextPacketAsync(cancellationToken);
            }

            // 如果已经是最后一个文件，返回null表示读取结束
            return null;
        }

        /// <inheritdoc />
        public List<DataPacket> ReadPackets(int count)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "数量必须大于0");
            }

            var packets = new List<DataPacket>();
            for (var i = 0; i < count; i++)
            {
                var packet = ReadNextPacket();
                if (packet == null)
                {
                    break;
                }
                
                packets.Add(packet);
            }

            return packets;
        }

        /// <inheritdoc />
        public async Task<List<DataPacket>> ReadPacketsAsync(
            int count,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "数量必须大于0");
            }

            var packets = new List<DataPacket>();
            for (var i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var packet = await ReadNextPacketAsync(cancellationToken);
                if (packet == null)
                {
                    break;
                }

                packets.Add(packet);
            }

            return packets;
        }

        /// <inheritdoc />
        public bool SeekToTime(DateTime targetTime)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            // 检查时间是否在文件范围内
            if (targetTime < StartTime || targetTime > EndTime)
            {
                return false;
            }

            // 转换为Unix时间戳格式
            var targetTimestamp = targetTime.ToUnixTimeMilliseconds();

            // 通过时间索引查找目标文件ID
            var fileId = FindFileIdByTime(targetTimestamp);
            if (fileId < 0)
            {
                return false;
            }

            // 切换到目标文件
            if (_CurrentFileId != fileId)
            {
                _PataFileReader.Close();
                OpenCurrentFile(fileId);
            }

            // 在目标文件中查找并定位到接近时间戳的索引位置
            var indexOffset = FindIndexInCurrentFile(targetTimestamp);
            if (indexOffset >= 0)
            {
                _PataFileReader.Seek(indexOffset);
                // 计算并更新全局位置
                CurrentPosition = CalculateGlobalPosition(fileId, indexOffset);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public bool SeekToPosition(long position)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            // 检查位置是否在有效范围内
            if (position < 0 || position >= PacketCount)
            {
                return false;
            }

            // 计算目标文件ID和文件内偏移
            var (fileId, localIndex) = CalculateFileAndIndex(position);

            // 切换到目标文件
            if (_CurrentFileId != fileId)
            {
                _PataFileReader.Close();
                OpenCurrentFile(fileId);
            }

            // 获取文件的索引列表
            var indices = GetFileIndices((uint)fileId + 1);
            if (indices == null || localIndex >= indices.Count)
            {
                return false;
            }

            // 在目标文件中定位到指定索引位置
            _PataFileReader.Seek(indices[localIndex].FileOffset);
            CurrentPosition = position;
            return true;
        }

        /// <inheritdoc />
        public DataPacket ReadPacketAt(long position)
        {
            return SeekToPosition(position) ? ReadNextPacket() : null;
        }

        /// <inheritdoc />
        public async Task<DataPacket> ReadPacketAtAsync(
            long position,
            CancellationToken cancellationToken = default
        )
        {
            return SeekToPosition(position) ? await ReadNextPacketAsync(cancellationToken) : null;
        }

        /// <inheritdoc />
        public void Reset()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            // 关闭当前PATA文件
            _PataFileReader.Close();

            // 重置状态
            _FirstPacketRead = false;
            CurrentPosition = 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                try
                {
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

        #endregion

        #region 私有方法

        /// <summary>
        /// 重置内部状态
        /// </summary>
        private void ResetState()
        {
            _FileEntries.Clear();
            _TimeIndices.Clear();
            _FileIndices.Clear();

            _CurrentFileId = -1;
            CurrentPosition = 0;
            _FirstPacketRead = false;
        }

        /// <summary>
        /// 打开指定的PATA数据文件
        /// </summary>
        private void OpenCurrentFile(int fileId)
        {
            if (fileId < 0 || fileId >= _FileEntries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(fileId));
            }

            _CurrentFileId = fileId;
            var entry = _FileEntries[fileId];
            var pataFilePath = PathHelper.GetFullPataFilePath(
                _PcapFileReader.FilePath,
                entry.RelativePath
            );

            _PataFileReader.Open(pataFilePath);

            // 预加载当前文件的索引
            GetFileIndices(entry.FileId);
        }

        /// <summary>
        /// 异步打开指定的PATA数据文件
        /// </summary>
        private async Task OpenCurrentFileAsync(int fileId, CancellationToken cancellationToken)
        {
            if (fileId < 0 || fileId >= _FileEntries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(fileId));
            }

            _CurrentFileId = fileId;
            var entry = _FileEntries[fileId];
            var pataFilePath = PathHelper.GetFullPataFilePath(
                _PcapFileReader.FilePath,
                entry.RelativePath
            );

            await _PataFileReader.OpenAsync(pataFilePath, cancellationToken);

            // 预加载当前文件的索引
            GetFileIndices(entry.FileId);
        }

        /// <summary>
        /// 通过时间查找文件ID
        /// </summary>
        private int FindFileIdByTime(long timestamp)
        {
            // 使用二分查找找到最接近时间戳的索引
            var index = FindNearestTimeIndex(timestamp);
            if (index < 0 || index >= _TimeIndices.Count)
            {
                return -1;
            }

            // 获取文件ID（注意索引从1开始，数组索引从0开始）
            return (int)_TimeIndices[index].FileId - 1;
        }

        /// <summary>
        /// 查找最接近时间戳的索引
        /// </summary>
        private int FindNearestTimeIndex(long timestamp)
        {
            if (_TimeIndices.Count == 0)
            {
                return -1;
            }

            int low = 0,
                high = _TimeIndices.Count - 1;

            while (low <= high)
            {
                var mid = (low + high) / 2;
                var midTs = _TimeIndices[mid].Timestamp;

                if (midTs == timestamp)
                {
                    return mid;
                }

                if (midTs < timestamp)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            // 返回最接近但不超过目标时间的索引
            return high >= 0 ? high : 0;
        }

        /// <summary>
        /// 在当前文件中查找指定时间戳的文件偏移量
        /// </summary>
        private long FindIndexInCurrentFile(long timestamp)
        {
            var entry = _FileEntries[_CurrentFileId];
            var indices = GetFileIndices(entry.FileId);

            if (indices == null || indices.Count == 0)
            {
                return -1;
            }

            // 二分查找最接近时间戳的索引
            int low = 0,
                high = indices.Count - 1;

            while (low <= high)
            {
                var mid = (low + high) / 2;
                var midTs = indices[mid].Timestamp;

                if (midTs == timestamp)
                {
                    return indices[mid].FileOffset;
                }

                if (midTs < timestamp)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            // 返回最接近但不超过目标时间的索引的偏移量
            if (high >= 0)
            {
                return indices[high].FileOffset;
            }

            // 如果找不到合适的索引，返回第一个索引的偏移量
            return indices[0].FileOffset;
        }

        /// <summary>
        /// 计算全局位置
        /// </summary>
        private long CalculateGlobalPosition(int fileId, long fileOffset)
        {
            long position = 0;

            // 累加之前文件的索引数量
            for (var i = 0; i < fileId; i++)
            {
                position += _FileEntries[i].IndexCount;
            }

            // 计算当前文件中的位置
            var indices = GetFileIndices(_FileEntries[fileId].FileId);
            if (indices != null && indices.Count > 0)
            {
                // 二分查找找到最接近偏移量的索引
                int low = 0,
                    high = indices.Count - 1;
                var idx = 0;

                while (low <= high)
                {
                    var mid = (low + high) / 2;
                    var midOffset = indices[mid].FileOffset;

                    if (midOffset == fileOffset)
                    {
                        idx = mid;
                        break;
                    }

                    if (midOffset < fileOffset)
                    {
                        low = mid + 1;
                        idx = mid;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }

                position += idx;
            }

            return position;
        }

        /// <summary>
        /// 计算全局位置对应的文件ID和本地索引
        /// </summary>
        private (int fileId, int localIndex) CalculateFileAndIndex(long position)
        {
            long count = 0;

            for (var i = 0; i < _FileEntries.Count; i++)
            {
                var entry = _FileEntries[i];
                var indexCount = entry.IndexCount;

                if (count + indexCount > position)
                {
                    // 找到对应的文件
                    return (i, (int)(position - count));
                }

                count += indexCount;
            }

            // 如果到达这里，说明位置无效，返回最后一个文件的最后一个索引
            return (
                _FileEntries.Count - 1,
                (int)_FileEntries[_FileEntries.Count - 1].IndexCount - 1
            );
        }

        /// <summary>
        /// 获取文件索引列表（懒加载）
        /// </summary>
        private List<PataFileIndexEntry> GetFileIndices(uint fileId)
        {
            var entry = _FileEntries[(int)fileId - 1];

            // 检查索引是否已加载
            if (_FileIndices.TryGetValue(entry.RelativePath, out var indices))
            {
                return indices;
            }

            // 按需加载索引
            indices = _PcapFileReader.ReadFileIndices(
                CalculateFileIndexOffset(fileId),
                entry.IndexCount
            );

            // 缓存索引
            _FileIndices[entry.RelativePath] = indices;

            return indices;
        }

        /// <summary>
        /// 计算文件索引在PCAP文件中的偏移量
        /// </summary>
        private long CalculateFileIndexOffset(uint fileId)
        {
            var offset =
                _PcapFileReader.Header.TimeIndexOffset
                + _TimeIndices.Count * PataTimeIndexEntry.ENTRY_SIZE;

            // 累加之前文件的索引大小
            for (var i = 0; i < fileId - 1; i++)
            {
                offset += _FileEntries[i].IndexCount * PataFileIndexEntry.ENTRY_SIZE;
            }

            return offset;
        }

        /// <summary>
        /// 释放流资源
        /// </summary>
        private void DisposeStreams()
        {
            _PataFileReader?.Dispose();
            _PcapFileReader?.Dispose();
        }

        /// <summary>
        /// 检查对象是否已释放，如果已释放则抛出异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapReader));
            }
        }

        #endregion
    }
}
