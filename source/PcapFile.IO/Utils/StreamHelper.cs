using System.IO;
using System.Runtime.InteropServices;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 流操作帮助类
    /// </summary>
    public static class StreamHelper
    {
        /// <summary>
        /// 从流中读取结构体
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="stream">流</param>
        /// <returns>读取的结构体</returns>
        public static T ReadStructure<T>(Stream stream)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var buffer = new byte[size];
            var totalBytesRead = 0;

            while (totalBytesRead < size)
            {
                var bytesRead = stream.Read(buffer, totalBytesRead, size - totalBytesRead);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("读取结构体时达到流末尾");
                }

                totalBytesRead += bytesRead;
            }

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// 将结构体写入流
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="stream">流</param>
        /// <param name="structure">结构体</param>
        public static void WriteStructure<T>(Stream stream, T structure)
            where T : struct
        {
            var size = Marshal.SizeOf(structure);
            var buffer = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(structure, ptr, true);
                Marshal.Copy(ptr, buffer, 0, size);
                stream.Write(buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 创建文件流
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="mode">文件模式</param>
        /// <param name="access">文件访问权限</param>
        /// <param name="share">文件共享权限</param>
        /// <returns>文件流</returns>
        public static FileStream CreateFileStream(
            string filePath,
            FileMode mode,
            FileAccess access,
            FileShare share
        )
        {
            return new FileStream(filePath, mode, access, share);
        }

        /// <summary>
        /// 创建二进制读取器
        /// </summary>
        /// <param name="stream">基础流</param>
        /// <returns>二进制读取器</returns>
        public static BinaryReader CreateBinaryReader(Stream stream)
        {
            return new BinaryReader(stream);
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
    }
}
