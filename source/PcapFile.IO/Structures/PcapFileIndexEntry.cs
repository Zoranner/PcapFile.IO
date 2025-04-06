using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
{
    /// <summary>
    /// 文件索引条目结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PcapFileIndexEntry
    {
        /// <summary>
        /// 条目大小(字节)
        /// </summary>
        public const int ENTRY_SIZE = 16; // 8 + 8

        /// <summary>
        /// 时间戳(毫秒)
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 文件偏移量
        /// </summary>
        public long FileOffset { get; set; }

        private PcapFileIndexEntry(long timestamp, long fileOffset)
        {
            Timestamp = timestamp;
            FileOffset = fileOffset;
        }

        /// <summary>
        /// 创建一个新的 PcapFileIndexEntry 实例
        /// </summary>
        /// <param name="timestamp">时间戳(毫秒)</param>
        /// <param name="fileOffset">文件偏移量</param>
        /// <returns>初始化后的 PcapFileIndexEntry 实例</returns>
        public static PcapFileIndexEntry Create(long timestamp, long fileOffset)
        {
            return new PcapFileIndexEntry(timestamp, fileOffset);
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
