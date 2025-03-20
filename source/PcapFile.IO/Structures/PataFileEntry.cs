using System;
using System.Runtime.InteropServices;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 文件条目结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PataFileEntry
    {
        /// <summary>
        /// 条目大小(字节)
        /// </summary>
        public const int ENTRY_SIZE = 4 + 2 + 256 + 8 + 8 + 4 + 4;

        /// <summary>
        /// 文件标识符
        /// </summary>
        public uint FileId { get; }

        /// <summary>
        /// 相对路径长度
        /// </summary>
        public ushort PathLength { get; }

        /// <summary>
        /// 相对路径
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string RelativePath;

        /// <summary>
        /// 起始时间戳
        /// </summary>
        public long StartTimestamp { get; }

        /// <summary>
        /// 结束时间戳
        /// </summary>
        public long EndTimestamp { get; }

        /// <summary>
        /// 索引项数量
        /// </summary>
        public uint IndexCount { get; }

        /// <summary>
        /// 保留字段
        /// </summary>
        public uint Reserved { get; }

        private PataFileEntry(
            uint fileId,
            string relativePath,
            long startTimestamp,
            long endTimestamp,
            uint indexCount
        )
        {
            FileId = fileId;
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            PathLength = (ushort)relativePath.Length;
            StartTimestamp = startTimestamp;
            EndTimestamp = endTimestamp;
            IndexCount = indexCount;
            Reserved = 0;
        }

        /// <summary>
        /// 创建一个新的 PataFileEntry 实例
        /// </summary>
        /// <param name="fileId">文件标识符</param>
        /// <param name="relativePath">相对路径</param>
        /// <param name="startTimestamp">起始时间戳</param>
        /// <param name="endTimestamp">结束时间戳</param>
        /// <param name="indexCount">索引项数量</param>
        /// <returns>初始化后的 PataFileEntry 实例</returns>
        public static PataFileEntry Create(
            uint fileId,
            string relativePath,
            long startTimestamp,
            long endTimestamp,
            uint indexCount
        )
        {
            return new PataFileEntry(
                fileId,
                relativePath,
                startTimestamp,
                endTimestamp,
                indexCount
            );
        }

        /// <summary>
        /// 将文件条目转换为字节数组
        /// </summary>
        /// <returns>字节数组</returns>
        public readonly byte[] ToBytes()
        {
            return BinaryConverter.ToBytes(this);
        }
    }
}
