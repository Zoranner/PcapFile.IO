using System;
using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Configuration;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
{
    /// <summary>
    /// PROJ文件头结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ProjFileHeader
    {
        /// <summary>
        /// 文件头部大小(字节)
        /// </summary>
        public const int HEADER_SIZE = 32;

        /// <summary>
        /// 魔术数，固定值 0xA1B2C3D4
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
        /// 文件条目表偏移量
        /// </summary>
        public uint FileEntryOffset { get; set; }

        /// <summary>
        /// 数据文件数量
        /// </summary>
        public ushort FileCount { get; set; }

        /// <summary>
        /// 时间索引表偏移量
        /// </summary>
        public uint TimeIndexOffset { get; set; }

        /// <summary>
        /// 索引间隔(毫秒)
        /// </summary>
        public ushort IndexInterval { get; set; }

        /// <summary>
        /// 总索引项数
        /// </summary>
        public uint TotalIndexCount { get; set; }

        /// <summary>
        /// CRC32 值
        /// </summary>
        public uint Checksum { get; set; }

        /// <summary>
        /// 保留字段
        /// </summary>
        public ushort Reserved { get; set; }

        private ProjFileHeader(ushort fileCount, uint totalIndexCount)
        {
            MagicNumber = FileVersionConfig.PROJ_MAGIC_NUMBER;
            MajorVersion = FileVersionConfig.MAJOR_VERSION;
            MinorVersion = FileVersionConfig.MINOR_VERSION;
            FileEntryOffset = HEADER_SIZE;
            FileCount = fileCount;
            TimeIndexOffset = 0;
            IndexInterval = FileVersionConfig.DEFAULT_INDEX_INTERVAL;
            TotalIndexCount = totalIndexCount;
            Checksum = 0;
            Reserved = 0;
        }

        /// <summary>
        /// 创建一个新的 ProjFileHeader 实例
        /// </summary>
        /// <param name="fileCount">数据文件数量</param>
        /// <param name="totalIndexCount">总索引项数</param>
        /// <returns>初始化后的 ProjFileHeader 实例</returns>
        public static ProjFileHeader Create(ushort fileCount, uint totalIndexCount)
        {
            return new ProjFileHeader(fileCount, totalIndexCount);
        }

        /// <summary>
        /// 从字节数组创建 ProjFileHeader 实例
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>ProjFileHeader 实例</returns>
        /// <exception cref="ArgumentException">当字节数组无效时抛出</exception>
        public static ProjFileHeader FromBytes(byte[] bytes)
        {
            return bytes == null || bytes.Length < HEADER_SIZE
                ? throw new ArgumentException("Invalid header data", nameof(bytes))
                : BinaryConverter.FromBytes<ProjFileHeader>(bytes);
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
