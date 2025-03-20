using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// Stream 扩展方法
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// 读取指定长度的字节数组
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="count">要读取的字节数</param>
        /// <returns>读取的字节数组，如果到达流末尾则返回null</returns>
        public static byte[] ReadExactly(this Stream stream, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var buffer = new byte[count];
            var totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                var bytesRead = stream.Read(buffer, totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    return null; // 到达流末尾
                }

                totalBytesRead += bytesRead;
            }

            return buffer;
        }

        /// <summary>
        /// 异步读取指定长度的字节数组
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="count">要读取的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读取的字节数组，如果到达流末尾则返回null</returns>
        public static async Task<byte[]> ReadExactlyAsync(
            this Stream stream,
            int count,
            CancellationToken cancellationToken = default
        )
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var buffer = new byte[count];
            var totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                var bytesRead = await stream.ReadAsync(
                    buffer,
                    totalBytesRead,
                    count - totalBytesRead,
                    cancellationToken
                );
                if (bytesRead == 0)
                {
                    return null; // 到达流末尾
                }

                totalBytesRead += bytesRead;
            }

            return buffer;
        }

        /// <summary>
        /// 跳过指定数量的字节
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="count">要跳过的字节数</param>
        /// <returns>实际跳过的字节数</returns>
        public static long Skip(this Stream stream, long count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // 如果流支持查找，直接使用 Seek
            if (stream.CanSeek)
            {
                var currentPosition = stream.Position;
                var newPosition = stream.Seek(count, SeekOrigin.Current);
                return newPosition - currentPosition;
            }

            // 否则，通过读取来跳过
            const int bufferSize = 8192;
            var buffer = new byte[Math.Min(bufferSize, count)];
            var totalBytesSkipped = 0L;

            while (totalBytesSkipped < count)
            {
                var bytesToSkip = (int)Math.Min(buffer.Length, count - totalBytesSkipped);
                var bytesRead = stream.Read(buffer, 0, bytesToSkip);
                if (bytesRead == 0)
                {
                    break; // 到达流末尾
                }

                totalBytesSkipped += bytesRead;
            }

            return totalBytesSkipped;
        }

        /// <summary>
        /// 异步跳过指定数量的字节
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="count">要跳过的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实际跳过的字节数</returns>
        public static async Task<long> SkipAsync(
            this Stream stream,
            long count,
            CancellationToken cancellationToken = default
        )
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // 如果流支持查找，直接使用 Seek
            if (stream.CanSeek)
            {
                var currentPosition = stream.Position;
                var newPosition = stream.Seek(count, SeekOrigin.Current);
                return newPosition - currentPosition;
            }

            // 否则，通过读取来跳过
            const int bufferSize = 8192;
            var buffer = new byte[Math.Min(bufferSize, count)];
            var totalBytesSkipped = 0L;

            while (totalBytesSkipped < count)
            {
                var bytesToSkip = (int)Math.Min(buffer.Length, count - totalBytesSkipped);
                var bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(0, bytesToSkip),
                    cancellationToken
                );
                if (bytesRead == 0)
                {
                    break; // 到达流末尾
                }

                totalBytesSkipped += bytesRead;
            }

            return totalBytesSkipped;
        }
    }
}
