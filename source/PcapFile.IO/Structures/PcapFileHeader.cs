using System;
using System.Runtime.InteropServices;

namespace KimoTech.PcapFile.IO
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
        public const int HEADER_SIZE = 16; // 4 + 2 + 2 + 4 + 4

        /// <summary>
        /// 默认时间戳精度(纳秒)
        /// </summary>
        public const uint DEFAULT_TIMESTAMP_ACCURACY = 1;

        /// <summary>
        /// 魔术数，固定值 0xD4C3B2A1
        /// </summary>
        public uint MagicNumber { get; set; }

        /// <summary>
        /// 主版本号，固定值 0x0002
        /// </summary>
        public ushort MajorVersion { get; set; }

        /// <summary>
        /// 次版本号，固定值 0x0004，表示支持纳秒级时间量
        /// </summary>
        public ushort MinorVersion { get; set; }

        /// <summary>
        /// 时区偏移量(GMT)，固定为0x00
        /// </summary>
        public int TimezoneOffset { get; set; }

        /// <summary>
        /// 时间戳精度，固定为0x00
        /// </summary>
        public uint TimestampAccuracy { get; set; }

        private PcapFileHeader(int timezoneOffset)
        {
            MagicNumber = PcapConstants.PCAP_MAGIC_NUMBER;
            MajorVersion = PcapConstants.MAJOR_VERSION;
            MinorVersion = PcapConstants.MINOR_VERSION;
            TimezoneOffset = timezoneOffset;
            TimestampAccuracy = 0; // 固定为0，与图片要求一致
        }

        /// <summary>
        /// 创建一个新的 PcapFileHeader 实例
        /// </summary>
        /// <param name="timezoneOffset">时区偏移量(GMT)，通常使用0</param>
        /// <returns>初始化后的 PcapFileHeader 实例</returns>
        public static PcapFileHeader Create(int timezoneOffset = 0)
        {
            return new PcapFileHeader(timezoneOffset);
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
