using System.Runtime.InteropServices;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
{
    /// <summary>
    /// 时间索引结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PataTimeIndexEntry
    {
        /// <summary>
        /// 索引项大小(字节)
        /// </summary>
        public const int ENTRY_SIZE = 12; // 8字节时间戳 + 4字节文件标识符

        /// <summary>
        /// 文件标识符
        /// </summary>
        public uint FileId { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public long Timestamp { get; }

        private PataTimeIndexEntry(uint fileId, long timestamp)
        {
            FileId = fileId;
            Timestamp = timestamp;
        }

        /// <summary>
        /// 创建一个新的 PataTimeIndexEntry 实例
        /// </summary>
        /// <param name="fileId">文件标识符</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>初始化后的 PataTimeIndexEntry 实例</returns>
        public static PataTimeIndexEntry Create(uint fileId, long timestamp)
        {
            return new PataTimeIndexEntry(fileId, timestamp);
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
