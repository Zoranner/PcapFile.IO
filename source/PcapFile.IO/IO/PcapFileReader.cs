using System;
using System.IO;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件读取器，负责管理PCAP数据文件的打开、读取和关闭操作
    /// </summary>
    /// <remarks>
    /// 注意：此类设计为单线程使用，不支持多线程并发读取。
    /// </remarks>
    internal class PcapFileReader : IDisposable
    {
        #region 字段

        private FileStream _FileStream;
        private BinaryReader _BinaryReader;
        private bool _IsDisposed;
        private readonly byte[] _ReadBuffer;
        private long _FileHeaderPosition;

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
        /// 获取当前文件中已读取的数据包数量
        /// </summary>
        public long PacketCount { get; private set; }

        /// <summary>
        /// 获取文件头信息
        /// </summary>
        public PcapFileHeader Header { get; private set; }

        /// <summary>
        /// 获取是否已到达文件末尾
        /// </summary>
        public bool EndOfFile => _FileStream == null || _FileStream.Position >= _FileStream.Length;

        #endregion

        #region 构造函数

        public PcapFileReader(int bufferSize = PcapConstants.MAX_BUFFER_SIZE)
        {
            _ReadBuffer = new byte[bufferSize];
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化PCAP文件读取器
        /// </summary>
        public void Initialize()
        {
            ThrowIfDisposed();

            // 重置状态
            PacketCount = 0;
        }

        /// <summary>
        /// 打开现有的PCAP文件
        /// </summary>
        /// <param name="filePath">PCAP文件路径</param>
        public void Open(string filePath)
        {
            ThrowIfDisposed();

            // 关闭现有文件
            Close();

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("文件路径不能为空", nameof(filePath));
                }

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"找不到文件: {filePath}");
                }

                FilePath = filePath;

                // 打开文件流
                _FileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                _BinaryReader = new BinaryReader(_FileStream);

                // 读取并验证文件头
                ReadAndValidateHeader();

                // 重置状态
                Initialize();
            }
            catch (Exception ex)
            {
                Close();
                throw new IOException($"打开PCAP文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 关闭PCAP文件
        /// </summary>
        public void Close()
        {
            _BinaryReader?.Close();
            _BinaryReader = null;

            _FileStream?.Close();
            _FileStream = null;

            FilePath = null;
            PacketCount = 0;
        }

        /// <summary>
        /// 读取下一个数据包
        /// </summary>
        /// <returns>读取到的数据包，如果到达文件末尾则返回null</returns>
        public DataPacket ReadPacket()
        {
            ThrowIfDisposed();

            if (!IsOpen || EndOfFile)
            {
                return null;
            }

            try
            {
                // 读取数据包头部
                var header = ReadPacketHeader();
                if (header == null)
                {
                    return null; // 到达文件末尾
                }

                // 读取数据包内容
                var data = ReadPacketData((int)header.Value.PacketLength);
                if (data == null || data.Length != header.Value.PacketLength)
                {
                    throw new InvalidDataException("数据包数据读取不完整");
                }

                // 验证校验和
                var calculatedChecksum = ChecksumCalculator.CalculateCrc32(data);
                if (calculatedChecksum != header.Value.Checksum)
                {
                    throw new InvalidDataException(
                        $"数据包校验和验证失败。期望: {header.Value.Checksum:X8}, 实际: {calculatedChecksum:X8}"
                    );
                }

                PacketCount++;
                return new DataPacket(header.Value, new ArraySegment<byte>(data));
            }
            catch (EndOfStreamException)
            {
                return null; // 到达文件末尾
            }
            catch (Exception ex)
            {
                throw new IOException($"读取数据包失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 重置读取位置到数据区开始位置
        /// </summary>
        public void Reset()
        {
            ThrowIfDisposed();

            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            // 定位到数据区开始位置（跳过文件头）
            _FileStream.Seek(_FileHeaderPosition + PcapFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            PacketCount = 0;
        }

        /// <summary>
        /// 移动到指定的字节位置
        /// </summary>
        /// <param name="position">字节位置</param>
        public void Seek(long position)
        {
            ThrowIfDisposed();

            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (position < _FileHeaderPosition + PcapFileHeader.HEADER_SIZE)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    "位置不能小于数据区开始位置"
                );
            }

            _FileStream.Seek(position, SeekOrigin.Begin);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 读取并验证文件头
        /// </summary>
        private void ReadAndValidateHeader()
        {
            _FileHeaderPosition = _FileStream.Position;

            if (_FileStream.Length < PcapFileHeader.HEADER_SIZE)
            {
                throw new InvalidDataException("文件太小，不是有效的PCAP文件");
            }

            // 读取文件头
            var magicNumber = _BinaryReader.ReadUInt32();
            var majorVersion = _BinaryReader.ReadUInt16();
            var minorVersion = _BinaryReader.ReadUInt16();
            var timezoneOffset = _BinaryReader.ReadInt32();
            var timestampAccuracy = _BinaryReader.ReadUInt32();

            Header = new PcapFileHeader
            {
                MagicNumber = magicNumber,
                MajorVersion = majorVersion,
                MinorVersion = minorVersion,
                TimezoneOffset = timezoneOffset,
                TimestampAccuracy = timestampAccuracy,
            };

            // 验证魔术数
            if (Header.MagicNumber != 0xD4C3B2A1)
            {
                throw new InvalidDataException($"无效的PCAP文件魔术数: 0x{Header.MagicNumber:X8}");
            }

            // 验证版本号
            if (Header.MajorVersion != 0x0002)
            {
                throw new NotSupportedException($"不支持的PCAP文件主版本号: {Header.MajorVersion}");
            }

            if (Header.MinorVersion != 0x0004)
            {
                throw new NotSupportedException($"不支持的PCAP文件次版本号: {Header.MinorVersion}");
            }
        }

        /// <summary>
        /// 读取数据包头部
        /// </summary>
        /// <returns>数据包头部，如果到达文件末尾则返回null</returns>
        private DataPacketHeader? ReadPacketHeader()
        {
            if (_FileStream.Position + DataPacketHeader.HEADER_SIZE > _FileStream.Length)
            {
                return null; // 没有足够的字节读取完整的头部
            }

            return ReadPacketHeaderInternal();
        }

        /// <summary>
        /// 内部读取数据包头部方法
        /// </summary>
        /// <returns>数据包头部</returns>
        private DataPacketHeader ReadPacketHeaderInternal()
        {
            var timestampSeconds = _BinaryReader.ReadUInt32();
            var timestampNanoseconds = _BinaryReader.ReadUInt32();
            var packetLength = _BinaryReader.ReadUInt32();
            var checksum = _BinaryReader.ReadUInt32();

            // 验证数据包长度
            return !DataPacket.IsValidSize(packetLength)
                ? throw new InvalidDataException($"无效的数据包长度: {packetLength}")
                : new DataPacketHeader
                {
                    TimestampSeconds = timestampSeconds,
                    TimestampNanoseconds = timestampNanoseconds,
                    PacketLength = packetLength,
                    Checksum = checksum,
                };
        }

        /// <summary>
        /// 读取数据包内容
        /// </summary>
        /// <param name="length">数据长度</param>
        /// <returns>数据包内容</returns>
        private byte[] ReadPacketData(int length)
        {
            if (length <= 0)
            {
                return Array.Empty<byte>();
            }

            if (_FileStream.Position + length > _FileStream.Length)
            {
                return null; // 没有足够的字节读取完整的数据
            }

            var data = new byte[length];
            var bytesRead = _BinaryReader.Read(data, 0, length);

            return bytesRead != length
                ? throw new EndOfStreamException("数据包数据读取不完整")
                : data;
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileReader));
            }
        }

        #endregion

        #region IDisposable

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
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    Close();
                }

                _IsDisposed = true;
            }
        }

        #endregion
    }
}
