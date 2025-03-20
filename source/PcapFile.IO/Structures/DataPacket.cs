using System;
using KimoTech.PcapFile.IO.Extensions;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
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
        /// 数据包捕获时间戳
        /// </summary>
        public DateTime Timestamp => DateTimeExtensions.FromUnixTimeMilliseconds(Header.Timestamp);

        /// <summary>
        /// 数据包内容
        /// </summary>
        public byte[] Data { get; }

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
        /// 构造函数
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        /// <param name="data">数据内容</param>
        public DataPacket(DateTime timestamp, byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));

            Header = DataPacketHeader.Create(
                timestamp,
                (uint)data.Length,
                ChecksumCalculator.CalculateCrc32(data)
            );
        }
    }
}
