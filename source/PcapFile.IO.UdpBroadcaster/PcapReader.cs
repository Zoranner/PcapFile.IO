using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO;

namespace KimoTech.PcapFile.IO.UdpBroadcaster
{
    /// <summary>
    /// PCAP文件读取器
    /// </summary>
    public class PcapReader : IDisposable
    {
        private readonly FileStream _fileStream;
        private readonly BinaryReader _binaryReader;
        private bool _isDisposed;
        private bool _headerRead;
        private PcapFileHeader _fileHeader;
        private long _maxPosition;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">PCAP文件路径</param>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        public PcapReader(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PCAP文件不存在", filePath);
            }

            _fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileVersionConfig.MAX_BUFFER_SIZE
            );

            _binaryReader = new BinaryReader(_fileStream);
            _maxPosition = _fileStream.Length;
        }

        /// <summary>
        /// 文件长度
        /// </summary>
        public long FileLength => _fileStream.Length;

        /// <summary>
        /// 当前位置
        /// </summary>
        public long Position => _fileStream.Position;

        /// <summary>
        /// 读取PCAP文件头
        /// </summary>
        /// <returns>PCAP文件头信息</returns>
        /// <exception cref="InvalidDataException">无效的PCAP文件格式</exception>
        public PcapFileHeader ReadHeader()
        {
            if (_headerRead)
            {
                // 如果已经读取过头，重置文件位置到开头
                _fileStream.Seek(0, SeekOrigin.Begin);
            }

            try
            {
                // 读取文件头字节
                var headerBytes = _binaryReader.ReadBytes(PcapFileHeader.HEADER_SIZE);

                if (headerBytes.Length < PcapFileHeader.HEADER_SIZE)
                {
                    throw new InvalidDataException("PCAP文件头不完整");
                }

                // 使用库中的方法解析文件头
                _fileHeader = PcapFileHeader.FromBytes(headerBytes);

                // 验证魔数
                if (
                    _fileHeader.MagicNumber != FileVersionConfig.PCAP_MAGIC_NUMBER
                    && _fileHeader.MagicNumber != FileVersionConfig.PROJ_MAGIC_NUMBER
                )
                {
                    throw new InvalidDataException("无效的PCAP文件格式");
                }

                _headerRead = true;
                return _fileHeader;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException)
            {
                throw new InvalidDataException("PCAP文件头不完整", ex);
            }
        }

        /// <summary>
        /// 异步读取一批数据包
        /// </summary>
        /// <param name="batchSize">批处理大小</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>数据包列表</returns>
        public async Task<List<DataPacket>> ReadPacketBatchAsync(
            int batchSize,
            CancellationToken cancellationToken = default
        )
        {
            if (!_headerRead)
            {
                ReadHeader();
            }

            var packets = new List<DataPacket>();
            var fileSize = _fileStream.Length;
            var position = _fileStream.Position;

            try
            {
                // 检查是否已经到达文件末尾
                if (position >= fileSize)
                {
                    return packets; // 返回空列表表示文件结束
                }

                for (
                    int i = 0;
                    i < batchSize
                        && position < fileSize
                        && !cancellationToken.IsCancellationRequested;
                    i++
                )
                {
                    try
                    {
                        // 读取数据包头
                        var headerBytes = _binaryReader.ReadBytes(DataPacketHeader.HEADER_SIZE);

                        if (headerBytes.Length < DataPacketHeader.HEADER_SIZE)
                        {
                            break; // 数据包头不完整，停止读取
                        }

                        var header = BinaryConverter.FromBytes<DataPacketHeader>(headerBytes);

                        // 验证包长度
                        if (
                            header.PacketLength > FileVersionConfig.MAX_PACKET_SIZE
                            || header.PacketLength == 0
                        )
                        {
                            throw new InvalidDataException(
                                $"数据包大小无效: {header.PacketLength} 字节"
                            );
                        }

                        // 读取实际数据
                        var packetData = _binaryReader.ReadBytes((int)header.PacketLength);

                        if (packetData.Length < header.PacketLength)
                        {
                            break; // 数据包数据不完整，停止读取
                        }

                        // 计算校验和并验证
                        var calculatedChecksum = ChecksumCalculator.CalculateCrc32(packetData);
                        var checksumValid = calculatedChecksum == header.Checksum;

                        if (!checksumValid)
                        {
                            // 校验和不匹配，记录警告但继续处理
                            await Console.Out.WriteLineAsync(
                                $"警告: 校验和不匹配 - 预期: 0x{header.Checksum:X8}, 计算得: 0x{calculatedChecksum:X8}"
                            );
                        }

                        // 创建数据包并添加到列表
                        var dataPacket = new DataPacket(header, packetData);
                        packets.Add(dataPacket);

                        // 更新当前位置
                        position = _fileStream.Position;
                    }
                    catch (EndOfStreamException)
                    {
                        break; // 文件结束
                    }
                    catch (Exception ex)
                    {
                        await Console.Out.WriteLineAsync($"读取数据包时出错: {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"批量读取数据包时出错: {ex.Message}");
            }

            return packets;
        }

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
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _binaryReader?.Dispose();
                    _fileStream?.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}
