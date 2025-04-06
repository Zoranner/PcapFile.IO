using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Configuration;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Readers
{
    /// <summary>
    /// PCAP文件读取器，负责管理PCAP数据文件的读取
    /// </summary>
    internal class PcapFileReader : IDisposable
    {
        #region 属性

        /// <summary>
        /// 获取文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 获取文件头
        /// </summary>
        public PcapFileHeader Header { get; private set; }

        /// <summary>
        /// 获取文件是否已打开
        /// </summary>
        public bool IsOpen => _FileStream != null;

        /// <summary>
        /// 文件流
        /// </summary>
        private FileStream _FileStream;

        /// <summary>
        /// 二进制读取器
        /// </summary>
        private BinaryReader _BinaryReader;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _IsDisposed;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public PcapFileReader()
        {
            _IsDisposed = false;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 打开PCAP文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功打开</returns>
        /// <exception cref="ArgumentNullException">文件路径为空时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">打开文件时发生IO错误时抛出</exception>
        public bool Open(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            // 先关闭已打开的文件
            Close();

            try
            {
                _FileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _BinaryReader = new BinaryReader(_FileStream);
                FilePath = filePath;

                // 读取文件头
                if (_FileStream.Length >= PcapFileHeader.HEADER_SIZE)
                {
                    _FileStream.Position = 0;
                    byte[] headerBytes = new byte[PcapFileHeader.HEADER_SIZE];
                    _FileStream.Read(headerBytes, 0, PcapFileHeader.HEADER_SIZE);
                    Header = PcapFileHeader.FromBytes(headerBytes);

                    // 验证魔术数
                    if (Header.MagicNumber != Configuration.FileVersionConfig.PCAP_MAGIC_NUMBER)
                    {
                        Close();
                        return false;
                    }

                    // 定位到第一个数据包
                    _FileStream.Position = PcapFileHeader.HEADER_SIZE;
                    return true;
                }
            }
            catch (IOException)
            {
                Close();
                throw;
            }
            catch
            {
                Close();
            }

            return false;
        }

        /// <summary>
        /// 异步打开PCAP文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功打开</returns>
        /// <exception cref="ArgumentNullException">文件路径为空时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">打开文件时发生IO错误时抛出</exception>
        public async Task<bool> OpenAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            // 先关闭已打开的文件
            await CloseAsync(cancellationToken);

            try
            {
                _FileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    true
                );
                _BinaryReader = new BinaryReader(_FileStream);
                FilePath = filePath;

                // 读取文件头
                if (_FileStream.Length >= PcapFileHeader.HEADER_SIZE)
                {
                    _FileStream.Position = 0;
                    byte[] headerBytes = new byte[PcapFileHeader.HEADER_SIZE];
                    await _FileStream.ReadAsync(
                        headerBytes,
                        0,
                        PcapFileHeader.HEADER_SIZE,
                        cancellationToken
                    );
                    Header = PcapFileHeader.FromBytes(headerBytes);

                    // 验证魔术数
                    if (Header.MagicNumber != Configuration.FileVersionConfig.PCAP_MAGIC_NUMBER)
                    {
                        await CloseAsync(cancellationToken);
                        return false;
                    }

                    // 定位到第一个数据包
                    _FileStream.Position = PcapFileHeader.HEADER_SIZE;
                    return true;
                }
            }
            catch (IOException)
            {
                await CloseAsync(cancellationToken);
                throw;
            }
            catch
            {
                await CloseAsync(cancellationToken);
            }

            return false;
        }

        /// <summary>
        /// 关闭文件
        /// </summary>
        public void Close()
        {
            if (_BinaryReader != null)
            {
                _BinaryReader.Close();
                _BinaryReader.Dispose();
                _BinaryReader = null;
            }

            if (_FileStream != null)
            {
                _FileStream.Close();
                _FileStream.Dispose();
                _FileStream = null;
            }

            FilePath = null;
        }

        /// <summary>
        /// 异步关闭文件
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_BinaryReader != null)
            {
                _BinaryReader.Close();
                _BinaryReader.Dispose();
                _BinaryReader = null;
            }

            if (_FileStream != null)
            {
                await _FileStream.FlushAsync(cancellationToken);
                _FileStream.Close();
                _FileStream.Dispose();
                _FileStream = null;
            }

            FilePath = null;
        }

        /// <summary>
        /// 读取下一个数据包
        /// </summary>
        /// <returns>数据包对象，如果读取失败则返回null</returns>
        /// <exception cref="InvalidOperationException">文件未打开时抛出</exception>
        public DataPacket ReadPacket()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (_FileStream.Position >= _FileStream.Length)
            {
                return null; // 已到文件末尾
            }

            try
            {
                // 读取数据包头
                if (_FileStream.Position + DataPacketHeader.HEADER_SIZE > _FileStream.Length)
                {
                    return null; // 剩余数据不足以构成一个完整的数据包头
                }

                byte[] headerBytes = new byte[DataPacketHeader.HEADER_SIZE];
                _FileStream.Read(headerBytes, 0, DataPacketHeader.HEADER_SIZE);
                var header = DataPacketHeader.FromBytes(headerBytes);

                // 验证数据包长度
                if (header.PacketLength > FileVersionConfig.MAX_PACKET_SIZE ||
                    _FileStream.Position + header.PacketLength > _FileStream.Length)
                {
                    return null; // 数据包长度无效或超出文件范围
                }

                // 读取数据包内容
                byte[] data = new byte[header.PacketLength];
                if (header.PacketLength > 0)
                {
                    _FileStream.Read(data, 0, (int)header.PacketLength);
                }

                // 检查校验和
                uint calculatedChecksum = ChecksumCalculator.CalculateCrc32(data);
                if (calculatedChecksum != header.Checksum)
                {
                    return null; // 校验和不匹配
                }

                return new DataPacket(header, data);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 异步读取下一个数据包
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据包对象，如果读取失败则返回null</returns>
        /// <exception cref="InvalidOperationException">文件未打开时抛出</exception>
        public async Task<DataPacket> ReadPacketAsync(CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (_FileStream.Position >= _FileStream.Length)
            {
                return null; // 已到文件末尾
            }

            try
            {
                // 读取数据包头
                if (_FileStream.Position + DataPacketHeader.HEADER_SIZE > _FileStream.Length)
                {
                    return null; // 剩余数据不足以构成一个完整的数据包头
                }

                byte[] headerBytes = new byte[DataPacketHeader.HEADER_SIZE];
                await _FileStream.ReadAsync(
                    headerBytes,
                    0,
                    DataPacketHeader.HEADER_SIZE,
                    cancellationToken
                );
                var header = DataPacketHeader.FromBytes(headerBytes);

                // 验证数据包长度
                if (header.PacketLength > FileVersionConfig.MAX_PACKET_SIZE ||
                    _FileStream.Position + header.PacketLength > _FileStream.Length)
                {
                    return null; // 数据包长度无效或超出文件范围
                }

                // 读取数据包内容
                byte[] data = new byte[header.PacketLength];
                if (header.PacketLength > 0)
                {
                    await _FileStream.ReadAsync(data, 0, (int)header.PacketLength, cancellationToken);
                }

                // 检查校验和
                uint calculatedChecksum = ChecksumCalculator.CalculateCrc32(data);
                if (calculatedChecksum != header.Checksum)
                {
                    return null; // 校验和不匹配
                }

                return new DataPacket(header, data);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 定位到指定偏移量
        /// </summary>
        /// <param name="offset">偏移量</param>
        /// <returns>是否成功定位</returns>
        /// <exception cref="InvalidOperationException">文件未打开时抛出</exception>
        public bool Seek(long offset)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (offset < PcapFileHeader.HEADER_SIZE || offset >= _FileStream.Length)
            {
                return false;
            }

            try
            {
                _FileStream.Position = offset;
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region IDisposable实现

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
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                Close();
            }

            _IsDisposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~PcapFileReader()
        {
            Dispose(false);
        }

        #endregion
    }
}
