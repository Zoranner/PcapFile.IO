using System;
using KimoTech.PcapFile.IO.Configuration;
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
        /// 检查数据大小是否在限制范围内
        /// </summary>
        /// <param name="dataSize">要检查的数据大小（字节）</param>
        /// <returns>如果数据大小在限制范围内返回true，否则返回false</returns>
        public static bool IsValidSize(long dataSize)
        {
            return dataSize > 0 && dataSize <= FileVersionConfig.MAX_PACKET_SIZE;
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
        /// 构造函数
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        /// <param name="data">数据内容</param>
        /// <exception cref="ArgumentNullException">数据为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">数据大小超过限制时抛出</exception>
        public DataPacket(DateTime timestamp, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length > FileVersionConfig.MAX_PACKET_SIZE)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    $"数据包大小({data.Length}字节)超过了限制({FileVersionConfig.MAX_PACKET_SIZE}字节)"
                );
            }

            Data = data;

            Header = DataPacketHeader.Create(
                timestamp,
                (uint)data.Length,
                ChecksumCalculator.CalculateCrc32(data)
            );
        }
    }
}
