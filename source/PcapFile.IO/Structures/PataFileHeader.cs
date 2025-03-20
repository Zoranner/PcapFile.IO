using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Configuration;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PATA数据文件头结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PataFileHeader
    {
        /// <summary>
        /// 文件头部大小(字节)
        /// </summary>
        public const int HEADER_SIZE = 16; // 4 + 4 + 4 + 2 + 2 + 4

        /// <summary>
        /// 默认时间戳精度(微秒)
        /// </summary>
        public const uint DEFAULT_TIMESTAMP_ACCURACY = 1000000;

        /// <summary>
        /// 魔术数，固定值 0x50415441 ("PATA")
        /// </summary>
        public uint MagicNumber { get; }

        /// <summary>
        /// 主版本号
        /// </summary>
        public ushort MajorVersion { get; }

        /// <summary>
        /// 次版本号
        /// </summary>
        public ushort MinorVersion { get; }

        /// <summary>
        /// 时区偏移量(GMT)
        /// </summary>
        public int Timezone { get; set; }

        /// <summary>
        /// 时间戳精度(毫秒)
        /// </summary>
        public uint TimestampAccuracy { get; set; }

        private PataFileHeader(int timezone)
        {
            MagicNumber = FileVersionConfig.PATA_MAGIC_NUMBER;
            MajorVersion = FileVersionConfig.MAJOR_VERSION;
            MinorVersion = FileVersionConfig.MINOR_VERSION;
            Timezone = timezone;
            TimestampAccuracy = FileVersionConfig.DEFAULT_TIMESTAMP_ACCURACY;
        }

        /// <summary>
        /// 创建一个新的 PataFileHeader 实例
        /// </summary>
        /// <param name="timezone">时区偏移量(GMT)</param>
        /// <returns>初始化后的 PataFileHeader 实例</returns>
        public static PataFileHeader Create(int timezone)
        {
            return new PataFileHeader(timezone);
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
