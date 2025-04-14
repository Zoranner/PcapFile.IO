using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KimoTech.PcapFile.IO
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
        /// 创建二进制读取器
        /// </summary>
        /// <param name="stream">要读取的流</param>
        /// <returns>二进制读取器</returns>
        /// <exception cref="ArgumentNullException">stream 为 null</exception>
        public static BinaryReader CreateBinaryReader(Stream stream)
        {
            return stream == null
                ? throw new ArgumentNullException(nameof(stream))
                : new BinaryReader(stream);
        }

        /// <summary>
        /// 从流中读取结构体
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="stream">流</param>
        /// <returns>结构体实例</returns>
        /// <exception cref="ArgumentNullException">stream 为 null</exception>
        /// <exception cref="IOException">读取失败</exception>
        public static T ReadStructure<T>(Stream stream)
            where T : struct
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var size = Marshal.SizeOf<T>();
            var buffer = new byte[size];
            stream.Read(buffer, 0, size);
            return BinaryConverter.FromBytes<T>(buffer);
        }

        /// <summary>
        /// 异步从流中读取结构体
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="stream">要读取的流</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读取的结构体</returns>
        /// <exception cref="ArgumentNullException">stream 为 null</exception>
        /// <exception cref="IOException">读取失败</exception>
        public static async Task<T> ReadStructureAsync<T>(
            Stream stream,
            CancellationToken cancellationToken = default
        )
            where T : struct
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var size = Marshal.SizeOf<T>();
            var buffer = new byte[size];
            await stream.ReadAsync(buffer.AsMemory(0, size), cancellationToken);
            return BinaryConverter.FromBytes<T>(buffer);
        }
    }
}
