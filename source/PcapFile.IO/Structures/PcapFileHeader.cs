using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Configuration;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件头结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PcapFileHeader
    {
        /// <summary>
        /// 文件头部大小(字节)
        /// </summary>
        public const int HEADER_SIZE = 28;

        /// <summary>
        /// 魔术数，固定值 0xA1B2C3D4
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
        /// 数据文件数量
        /// </summary>
        public ushort FileCount { get; set; }

        /// <summary>
        /// 总索引项数
        /// </summary>
        public uint TotalIndexCount { get; set; }

        /// <summary>
        /// 索引间隔(毫秒)
        /// </summary>
        public ushort IndexInterval { get; set; }

        /// <summary>
        /// 文件条目表偏移量
        /// </summary>
        public uint FileEntryOffset { get; set; }

        /// <summary>
        /// 时间索引表偏移量
        /// </summary>
        public uint TimeIndexOffset { get; set; }

        /// <summary>
        /// 保留字段
        /// </summary>
        public ushort Reserved { get; set; }

        private PcapFileHeader(ushort fileCount, uint totalIndexCount)
        {
            MagicNumber = FileVersionConfig.PCAP_MAGIC_NUMBER;
            MajorVersion = FileVersionConfig.MAJOR_VERSION;
            MinorVersion = FileVersionConfig.MINOR_VERSION;
            FileCount = fileCount;
            TotalIndexCount = totalIndexCount;
            IndexInterval = FileVersionConfig.DEFAULT_INDEX_INTERVAL;
            FileEntryOffset = 0;
            TimeIndexOffset = 0;
            Reserved = 0;
        }

        /// <summary>
        /// 创建一个新的 PcapFileHeader 实例
        /// </summary>
        /// <param name="fileCount">数据文件数量</param>
        /// <param name="totalIndexCount">总索引项数</param>
        /// <returns>初始化后的 PcapFileHeader 实例</returns>
        public static PcapFileHeader Create(ushort fileCount, uint totalIndexCount)
        {
            return new PcapFileHeader(fileCount, totalIndexCount);
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
