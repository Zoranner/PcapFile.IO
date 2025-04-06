using System;
using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Configuration;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
{
    /// <summary>
    /// PCAP数据文件头结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PcapFileHeader
    {
        /// <summary>
        /// 文件头部大小(字节)
        /// </summary>
        public const int HEADER_SIZE = 16; // 4 + 4 + 4 + 2 + 2 + 4

        /// <summary>
        /// 默认时间戳精度(毫秒)
        /// </summary>
        public const uint DEFAULT_TIMESTAMP_ACCURACY = 1000;

        /// <summary>
        /// 魔术数，固定值 0x50415441 ("PCAP")
        /// </summary>
        public uint MagicNumber { get; set; }

        /// <summary>
        /// 主版本号
        /// </summary>
        public ushort MajorVersion { get; set; }

        /// <summary>
        /// 次版本号
        /// </summary>
        public ushort MinorVersion { get; set; }

        /// <summary>
        /// 时区偏移量(GMT)
        /// </summary>
        public int Timezone { get; set; }

        /// <summary>
        /// 时间戳精度(毫秒)
        /// </summary>
        public uint TimestampAccuracy { get; set; }

        private PcapFileHeader(int timezone)
        {
            MagicNumber = FileVersionConfig.PCAP_MAGIC_NUMBER;
            MajorVersion = FileVersionConfig.MAJOR_VERSION;
            MinorVersion = FileVersionConfig.MINOR_VERSION;
            Timezone = timezone;
            TimestampAccuracy = FileVersionConfig.DEFAULT_TIMESTAMP_ACCURACY;
        }

        /// <summary>
        /// 创建一个新的 PcapFileHeader 实例
        /// </summary>
        /// <param name="timezone">时区偏移量(GMT)</param>
        /// <returns>初始化后的 PcapFileHeader 实例</returns>
        public static PcapFileHeader Create(int timezone)
        {
            return new PcapFileHeader(timezone);
        }

        /// <summary>
        /// 从字节数组创建 PcapFileHeader 实例
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>PcapFileHeader 实例</returns>
        /// <exception cref="ArgumentException">当字节数组无效时抛出</exception>
        public static PcapFileHeader FromBytes(byte[] bytes)
        {
            return bytes == null || bytes.Length < HEADER_SIZE
                ? throw new ArgumentException("Invalid header data", nameof(bytes))
                : BinaryConverter.FromBytes<PcapFileHeader>(bytes);
        }

        /// <summary>
        /// 将文件头转换为字节数组
        /// </summary>
        /// <returns>字节数组</returns>
        public readonly byte[] ToBytes()
        {
            return BinaryConverter.ToBytes(this);
        }
    }
}
