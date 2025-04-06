using System;
using System.Runtime.InteropServices;
using System.Text;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO.Structures
{
    /// <summary>
    /// 数据文件条目结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PcapFileEntry
    {
        /// <summary>
        /// 条目大小(字节)
        /// </summary>
        public const int ENTRY_SIZE = 286; // 4 + 2 + 256 + 8 + 8 + 4 + 4

        /// <summary>
        /// 文件ID
        /// </summary>
        public uint FileId { get; set; }

        /// <summary>
        /// 相对路径长度
        /// </summary>
        public ushort PathLength { get; set; }

        /// <summary>
        /// 相对路径
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] RelativePathBytes;

        /// <summary>
        /// 相对路径
        /// </summary>
        public string RelativePath
        {
            get
            {
                if (RelativePathBytes == null)
                {
                    return string.Empty;
                }

                return Encoding.UTF8.GetString(RelativePathBytes, 0, PathLength);
            }
        }

        /// <summary>
        /// 起始时间戳(毫秒)
        /// </summary>
        public long StartTimestamp { get; set; }

        /// <summary>
        /// 结束时间戳(毫秒)
        /// </summary>
        public long EndTimestamp { get; set; }

        /// <summary>
        /// 索引数量
        /// </summary>
        public uint IndexCount { get; set; }

        /// <summary>
        /// 保留字段
        /// </summary>
        public uint Reserved { get; set; }

        private PcapFileEntry(
            uint fileId,
            string relativePath,
            long startTimestamp,
            long endTimestamp,
            uint indexCount
        )
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                throw new ArgumentNullException(nameof(relativePath));
            }

            FileId = fileId;
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            PathLength = (ushort)Math.Min(pathBytes.Length, 255);
            RelativePathBytes = new byte[256];
            Array.Copy(pathBytes, RelativePathBytes, PathLength);
            StartTimestamp = startTimestamp;
            EndTimestamp = endTimestamp;
            IndexCount = indexCount;
            Reserved = 0;
        }

        /// <summary>
        /// 创建一个新的 PcapFileEntry 实例
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <param name="relativePath">相对路径</param>
        /// <param name="startTimestamp">起始时间戳(毫秒)</param>
        /// <param name="endTimestamp">结束时间戳(毫秒)</param>
        /// <param name="indexCount">索引数量</param>
        /// <returns>初始化后的 PcapFileEntry 实例</returns>
        public static PcapFileEntry Create(
            uint fileId,
            string relativePath,
            long startTimestamp,
            long endTimestamp,
            uint indexCount
        )
        {
            return new PcapFileEntry(
                fileId,
                relativePath,
                startTimestamp,
                endTimestamp,
                indexCount
            );
        }

        /// <summary>
        /// 将结构转换为字节数组
        /// </summary>
        /// <returns>字节数组</returns>
        public readonly byte[] ToBytes()
        {
            return BinaryConverter.ToBytes(this);
        }
    }
}
