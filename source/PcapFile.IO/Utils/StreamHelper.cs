using System.IO;
using System.Runtime.InteropServices;

namespace KimoTech.PcapFile.IO.Utils
{
    /// <summary>
    /// 流处理工具类
    /// </summary>
    public static class StreamHelper
    {
        /// <summary>
        /// 创建文件流
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="mode">文件模式</param>
        /// <param name="access">文件访问权限</param>
        /// <param name="share">文件共享权限</param>
        /// <returns>文件流</returns>
        public static FileStream CreateFileStream(
            string path,
            FileMode mode,
            FileAccess access,
            FileShare share
        )
        {
            return new FileStream(path, mode, access, share);
        }

        /// <summary>
        /// 创建二进制写入器
        /// </summary>
        /// <param name="stream">基础流</param>
        /// <returns>二进制写入器</returns>
        public static BinaryWriter CreateBinaryWriter(Stream stream)
        {
            return new BinaryWriter(stream);
        }

        /// <summary>
        /// 从流中读取结构体
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="stream">流</param>
        /// <returns>结构体实例</returns>
        public static T ReadStructure<T>(Stream stream)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var buffer = new byte[size];
            stream.Read(buffer, 0, size);
            return ByteArrayToStructure<T>(buffer);
        }

        /// <summary>
        /// 将字节数组转换为结构体
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="bytes">字节数组</param>
        /// <returns>结构体实例</returns>
        private static T ByteArrayToStructure<T>(byte[] bytes)
            where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
