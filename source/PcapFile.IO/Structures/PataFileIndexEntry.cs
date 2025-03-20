using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
{
    /// <summary>
    /// 文件内索引结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PataFileIndexEntry
    {
        /// <summary>
        /// 索引项大小(字节)
        /// </summary>
        public const int ENTRY_SIZE = 16; // 8字节时间戳 + 8字节文件偏移量

        /// <summary>
        /// 时间戳
        /// </summary>
        public long Timestamp { get; }

        /// <summary>
        /// 数据包在文件中的偏移量
        /// </summary>
        public long FileOffset { get; }

        private PataFileIndexEntry(long timestamp, long fileOffset)
        {
            Timestamp = timestamp;
            FileOffset = fileOffset;
        }

        /// <summary>
        /// 创建一个新的 PataFileIndexEntry 实例
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        /// <param name="fileOffset">文件偏移量</param>
        /// <returns>初始化后的 PataFileIndexEntry 实例</returns>
        public static PataFileIndexEntry Create(long timestamp, long fileOffset)
        {
            return new PataFileIndexEntry(timestamp, fileOffset);
        }

        /// <summary>
        /// 将索引项转换为字节数组
        /// </summary>
        /// <returns>字节数组</returns>
        public readonly byte[] ToBytes()
        {
            return BinaryConverter.ToBytes(this);
        }
    }
}
