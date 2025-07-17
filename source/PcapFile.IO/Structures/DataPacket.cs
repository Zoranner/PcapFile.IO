using System;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 数据包类
    /// </summary>
    public class DataPacket
    {
        /// <summary>
        /// 数据包头
        /// </summary>
        public DataPacketHeader Header { get; }

        /// <summary>
        /// 数据包捕获时间
        /// </summary>
        public DateTime CaptureTime =>
            DateTimeExtensions.FromUnixTimeWithNanoseconds(
                Header.TimestampSeconds,
                Header.TimestampNanoseconds
            );

        /// <summary>
        /// 数据包内容
        /// </summary>
        public ArraySegment<byte> Data { get; }

        /// <summary>
        /// 数据包长度
        /// </summary>
        public uint PacketLength => Header.PacketLength;

        /// <summary>
        /// 获取数据包总大小(字节)
        /// </summary>
        public int TotalSize => DataPacketHeader.HEADER_SIZE + (int)PacketLength;

        /// <summary>
        /// 数据包校验和
        /// </summary>
        public uint Checksum => Header.Checksum;

        /// <summary>
        /// 检查数据大小是否在限制范围内
        /// </summary>
        /// <param name="dataSize">要检查的数据大小（字节）</param>
        /// <returns>如果数据大小在限制范围内返回true，否则返回false</returns>
        public static bool IsValidSize(long dataSize)
        {
            return dataSize > 0 && dataSize <= PcapConstants.MAX_PACKET_SIZE;
        }

        /// <summary>
        /// 检查数据大小是否在限制范围内
        /// </summary>
        /// <param name="data">要检查的数据</param>
        /// <returns>如果数据大小在限制范围内返回true，否则返回false</returns>
        public static bool IsValidSize(byte[] data)
        {
            return data != null && IsValidSize(data.Length);
        }

        /// <summary>
        /// 主构造函数 - 使用预构建的头部信息
        /// </summary>
        /// <param name="header">数据包头</param>
        /// <param name="data">数据内容</param>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        public DataPacket(DataPacketHeader header, ArraySegment<byte> data)
        {
            ValidateData(data);

            Data = data;
            Header = header;
        }

        /// <summary>
        /// 构造函数 - 使用捕获时间自动生成头部
        /// </summary>
        /// <param name="captureTime">捕获时间</param>
        /// <param name="data">数据内容</param>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        public DataPacket(in DateTime captureTime, ArraySegment<byte> data)
            : this(DataPacketHeader.CreateFromPacket(captureTime, ValidateAndGetSpan(data)), data)
        { }

        /// <summary>
        /// 构造函数 - 使用捕获时间和byte[]数组
        /// </summary>
        /// <param name="captureTime">捕获时间</param>
        /// <param name="data">数据内容</param>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        public DataPacket(in DateTime captureTime, byte[] data)
            : this(
                captureTime,
                new ArraySegment<byte>(data ?? throw new ArgumentNullException(nameof(data)))
            ) { }

        /// <summary>
        /// 构造函数 - 使用时间戳
        /// </summary>
        /// <param name="timestampSeconds">捕获时间戳(秒)</param>
        /// <param name="timestampNanoseconds">捕获时间戳(纳秒)</param>
        /// <param name="data">数据内容</param>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        public DataPacket(uint timestampSeconds, uint timestampNanoseconds, byte[] data)
            : this(
                CreateHeaderFromTimestamp(timestampSeconds, timestampNanoseconds, data),
                new ArraySegment<byte>(data ?? throw new ArgumentNullException(nameof(data)))
            ) { }

        /// <summary>
        /// 构造函数 - 使用时间戳和ArraySegment
        /// </summary>
        /// <param name="timestampSeconds">捕获时间戳(秒)</param>
        /// <param name="timestampNanoseconds">捕获时间戳(纳秒)</param>
        /// <param name="data">数据内容</param>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        public DataPacket(uint timestampSeconds, uint timestampNanoseconds, ArraySegment<byte> data)
            : this(CreateHeaderFromTimestamp(timestampSeconds, timestampNanoseconds, data), data)
        { }

        /// <summary>
        /// 验证数据有效性
        /// </summary>
        /// <param name="data">要验证的数据</param>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        private static void ValidateData(ArraySegment<byte> data)
        {
            if (data.Array == null)
            {
                throw new ArgumentNullException(nameof(data), "数据不能为空");
            }

            if (data.Count > PcapConstants.MAX_PACKET_SIZE)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    $"数据包大小({data.Count}字节)超过了限制({PcapConstants.MAX_PACKET_SIZE}字节)"
                );
            }
        }

        /// <summary>
        /// 验证数据并获取Span - 用于构造函数链调用
        /// </summary>
        /// <param name="data">要验证的数据</param>
        /// <returns>数据的Span表示</returns>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        private static Span<byte> ValidateAndGetSpan(ArraySegment<byte> data)
        {
            ValidateData(data);
            return data.AsSpan();
        }

        /// <summary>
        /// 从时间戳创建数据包头
        /// </summary>
        /// <param name="timestampSeconds">时间戳秒部分</param>
        /// <param name="timestampNanoseconds">时间戳纳秒部分</param>
        /// <param name="data">数据内容</param>
        /// <returns>创建的数据包头</returns>
        private static DataPacketHeader CreateHeaderFromTimestamp(
            uint timestampSeconds,
            uint timestampNanoseconds,
            ArraySegment<byte> data
        )
        {
            ValidateData(data);
            var checksum = ChecksumCalculator.CalculateCrc32(data.AsSpan());
            return DataPacketHeader.Create(
                timestampSeconds,
                timestampNanoseconds,
                (uint)data.Count,
                checksum
            );
        }

        /// <summary>
        /// 从时间戳创建数据包头 - byte[]重载
        /// </summary>
        /// <param name="timestampSeconds">时间戳秒部分</param>
        /// <param name="timestampNanoseconds">时间戳纳秒部分</param>
        /// <param name="data">数据内容</param>
        /// <returns>创建的数据包头</returns>
        private static DataPacketHeader CreateHeaderFromTimestamp(
            uint timestampSeconds,
            uint timestampNanoseconds,
            byte[] data
        )
        {
            return data == null
                ? throw new ArgumentNullException(nameof(data))
                : CreateHeaderFromTimestamp(
                    timestampSeconds,
                    timestampNanoseconds,
                    new ArraySegment<byte>(data)
                );
        }
    }
}
