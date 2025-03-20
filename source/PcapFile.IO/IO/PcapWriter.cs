using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP数据写入器
    /// </summary>
    public class PcapWriter : IPcapWriter
    {
        private readonly PcapFileManager _PcapManager;
        private readonly PataFileManager _PataManager;

        public PcapWriter()
        {
            _PcapManager = new PcapFileManager();
            _PataManager = new PataFileManager();
        }

        #region 属性

        /// <inheritdoc />
        public long PacketCount { get; private set; }

        /// <inheritdoc />
        public long FileSize => _PcapManager.FileSize + _PataManager.FileSize;

        /// <inheritdoc />
        public string FilePath => _PcapManager.FilePath;

        /// <inheritdoc />
        public bool AutoFlush { get; set; }

        /// <inheritdoc />
        public bool IsOpen => _PcapManager.IsOpen && _PataManager.IsOpen;

        #endregion

        #region 公共方法

        /// <inheritdoc />
        public bool Create(string filePath, PcapFileHeader header = default)
        {
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

                // 创建文件
                _PcapManager.Create(filePath);
                _PataManager.Create(filePath);

                // 写入文件头
                if (header.MagicNumber == 0)
                {
                    header = PcapFileHeader.Create(1, 0);
                }

                _PcapManager.WriteHeader(header);

                var pataHeader = PataFileHeader.Create(0);
                _PataManager.WriteHeader(pataHeader);

                PacketCount = 0;
                AutoFlush = true;

                return true;
            }
            catch
            {
                Close();
                return false;
            }
        }

        /// <inheritdoc />
        public bool Open(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            try
            {
                // 打开文件
                _PcapManager.Open(filePath);
                _PataManager.Open(filePath);

                // 读取文件头
                var header = _PcapManager.ReadHeader();
                PacketCount = header.TotalIndexCount;
                AutoFlush = true;

                return true;
            }
            catch
            {
                Close();
                return false;
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            _PataManager.Close();
            _PcapManager.Close();
            PacketCount = 0;
        }

        /// <inheritdoc />
        public bool WritePacket(DataPacket packet)
        {
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
                // 写入数据包
                _PataManager.WritePacket(packet);

                // 更新索引
                var indexEntry = PataFileIndexEntry.Create(
                    packet.Header.Timestamp,
                    _PataManager.Position - packet.TotalSize
                );
                _PcapManager.WriteIndexEntry(indexEntry);

                PacketCount++;

                if (AutoFlush)
                {
                    Flush();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool WritePackets(IEnumerable<DataPacket> packets)
        {
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
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public void Flush()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            _PataManager.Flush();
            _PcapManager.Flush();
        }

        #endregion

        #region 异步方法

        /// <inheritdoc />
        public async Task<bool> WritePacketAsync(
            DataPacket packet,
            CancellationToken cancellationToken = default
        )
        {
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
                // 写入数据包
                await _PataManager.WritePacketAsync(packet, cancellationToken);

                // 更新索引
                var indexEntry = PataFileIndexEntry.Create(
                    packet.Header.Timestamp,
                    _PataManager.Position - packet.TotalSize
                );
                var indexBytes = indexEntry.ToBytes();
                await _PcapManager.WriteIndexEntryAsync(indexBytes, cancellationToken);

                PacketCount++;

                if (AutoFlush)
                {
                    await FlushAsync(cancellationToken);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> WritePacketsAsync(
            IEnumerable<DataPacket> packets,
            CancellationToken cancellationToken = default
        )
        {
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
                    if (!await WritePacketAsync(packet, cancellationToken))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            await _PataManager.FlushAsync(cancellationToken);
            await _PcapManager.FlushAsync(cancellationToken);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _PataManager.Dispose();
            _PcapManager.Dispose();
        }

        #endregion
    }
}
