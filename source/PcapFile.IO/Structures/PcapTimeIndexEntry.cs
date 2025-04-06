using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
{
    /// <summary>
    /// 时间索引条目结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PcapTimeIndexEntry
    {
        /// <summary>
        /// 条目大小(字节)
        /// </summary>
        public const int ENTRY_SIZE = 12; // 4 + 8

        /// <summary>
        /// 文件ID
        /// </summary>
        public uint FileId { get; set; }

        /// <summary>
        /// 时间戳(毫秒)
        /// </summary>
        public long Timestamp { get; set; }

        private PcapTimeIndexEntry(uint fileId, long timestamp)
        {
            FileId = fileId;
            Timestamp = timestamp;
        }

        /// <summary>
        /// 创建一个新的 PcapTimeIndexEntry 实例
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <param name="timestamp">时间戳(毫秒)</param>
        /// <returns>初始化后的 PcapTimeIndexEntry 实例</returns>
        public static PcapTimeIndexEntry Create(uint fileId, long timestamp)
        {
            return new PcapTimeIndexEntry(fileId, timestamp);
        }

        /// <summary>
        /// 将索引条目转换为字节数组
        /// </summary>
        /// <returns>字节数组</returns>
        public readonly byte[] ToBytes()
        {
            return BinaryConverter.ToBytes(this);
        }
    }
}
