using System;
using System.Runtime.InteropServices;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 数据包结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DataPacketHeader
    {
        /// <summary>
        /// 数据包头大小(字节)
        /// </summary>
        public const int HEADER_SIZE = 16; // 4 + 4 + 4 + 4

        /// <summary>
        /// 数据包捕获时间戳(秒)
        /// </summary>
        public uint TimestampSeconds { get; set; }

        /// <summary>
        /// 数据包捕获时间戳(纳秒)
        /// </summary>
        public uint TimestampNanoseconds { get; set; }

        /// <summary>
        /// 数据包长度
        /// </summary>
        public uint PacketLength { get; set; }

        /// <summary>
        /// 数据包校验和
        /// </summary>
        public uint Checksum { get; set; }

        private DataPacketHeader(
            uint timestampSeconds,
            uint timestampNanoseconds,
            uint packetLength,
            uint checksum
        )
        {
            TimestampSeconds = timestampSeconds;
            TimestampNanoseconds = timestampNanoseconds;
            PacketLength = packetLength;
            Checksum = checksum;
        }

        /// <summary>
        /// 创建一个新的 DataPacketHeader 实例
        /// </summary>
        /// <param name="timestampSeconds">捕获时间戳(秒)</param>
        /// <param name="timestampNanoseconds">捕获时间戳(纳秒)</param>
        /// <param name="packetLength">数据包长度</param>
        /// <param name="checksum">校验和</param>
        /// <returns>初始化后的 DataPacketHeader 实例</returns>
        public static DataPacketHeader Create(
            uint timestampSeconds,
            uint timestampNanoseconds,
            uint packetLength,
            uint checksum
        )
        {
            return new DataPacketHeader(
                timestampSeconds,
                timestampNanoseconds,
                packetLength,
                checksum
            );
        }

        /// <summary>
        /// 创建一个新的 DataPacketHeader 实例（使用DateTime）
        /// </summary>
        /// <param name="captureTime">捕获时间</param>
        /// <param name="packetLength">数据包长度</param>
        /// <param name="checksum">校验和</param>
        /// <returns>初始化后的 DataPacketHeader 实例</returns>
        public static DataPacketHeader CreateFromDateTime(
            DateTime captureTime,
            uint packetLength,
            uint checksum
        )
        {
            var seconds = captureTime.ToUnixTimeSeconds();
            var nanoseconds = captureTime.GetNanoseconds();
            return new DataPacketHeader(seconds, nanoseconds, packetLength, checksum);
        }

        /// <summary>
        /// 根据捕获时间和数据包内容创建一个新的 DataPacketHeader 实例
        /// </summary>
        /// <param name="captureTime">捕获时间</param>
        /// <param name="packetData">数据包内容</param>
        /// <returns>初始化后的 DataPacketHeader 实例</returns>
        public static DataPacketHeader CreateFromPacket(DateTime captureTime, byte[] packetData)
        {
            if (packetData == null)
            {
                throw new ArgumentNullException(nameof(packetData));
            }

            var packetLength = (uint)packetData.Length;
            var checksum = ChecksumCalculator.CalculateCrc32(packetData);
            return CreateFromDateTime(captureTime, packetLength, checksum);
        }

        /// <summary>
        /// 从字节数组创建 DataPacketHeader 实例
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>DataPacketHeader 实例</returns>
        /// <exception cref="ArgumentException">当字节数组无效时抛出</exception>
        public static DataPacketHeader FromBytes(byte[] bytes)
        {
            return bytes == null || bytes.Length < HEADER_SIZE
                ? throw new ArgumentException("Invalid header data", nameof(bytes))
                : BinaryConverter.FromBytes<DataPacketHeader>(bytes);
        }

        /// <summary>
        /// 将数据包头转换为字节数组
        /// </summary>
        /// <returns>字节数组</returns>
        public readonly byte[] ToBytes()
        {
            return BinaryConverter.ToBytes(this);
        }
    }
}
