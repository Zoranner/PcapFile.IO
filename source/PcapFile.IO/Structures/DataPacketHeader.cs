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
        public const int HEADER_SIZE = 16; // 8 + 4 + 4

        /// <summary>
        /// 数据包捕获时间戳(毫秒)
        /// </summary>
        public long Timestamp { get; }

        /// <summary>
        /// 数据包长度
        /// </summary>
        public uint PacketLength { get; }

        /// <summary>
        /// 数据包校验和
        /// </summary>
        public uint Checksum { get; }

        private DataPacketHeader(long timestamp, uint packetLength, uint checksum)
        {
            Timestamp = timestamp;
            PacketLength = packetLength;
            Checksum = checksum;
        }

        /// <summary>
        /// 创建一个新的 DataPacketHeader 实例
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        /// <param name="packetLength">数据包长度</param>
        /// <param name="checksum">校验和</param>
        /// <returns>初始化后的 DataPacketHeader 实例</returns>
        public static DataPacketHeader Create(DateTime timestamp, uint packetLength, uint checksum)
        {
            return new DataPacketHeader(timestamp.ToUnixTimeMilliseconds(), packetLength, checksum);
        }

        /// <summary>
        /// 根据时间戳和数据包内容创建一个新的 DataPacketHeader 实例
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        /// <param name="packetData">数据包内容</param>
        /// <returns>初始化后的 DataPacketHeader 实例</returns>
        public static DataPacketHeader CreateFromPacket(DateTime timestamp, byte[] packetData)
        {
            if (packetData == null)
            {
                throw new ArgumentNullException(nameof(packetData));
            }

            var packetLength = (uint)packetData.Length;
            var checksum = ChecksumCalculator.CalculateCrc32(packetData);
            return Create(timestamp, packetLength, checksum);
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
