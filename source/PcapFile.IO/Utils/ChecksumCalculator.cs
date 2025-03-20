using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 校验和计算工具
    /// </summary>
    public static class ChecksumCalculator
    {
        /// <summary>
        /// 计算CRC32校验和
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>校验和</returns>
        public static uint CalculateCrc32(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0;
            }

            var crc = 0xFFFFFFFF;
            var polynomial = 0xEDB88320;

            for (var i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (var j = 0; j < 8; j++)
                {
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : (crc >> 1);
                }
            }

            return ~crc;
        }

        /// <summary>
        /// 计算Adler32校验和
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>校验和</returns>
        public static uint CalculateAdler32(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 1; // Adler32初始值为1
            }

            const uint modAdler = 65521;
            uint a = 1,
                b = 0;

            for (var i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % modAdler;
                b = (b + a) % modAdler;
            }

            return (b << 16) | a;
        }

        /// <summary>
        /// 计算MD5哈希值
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>MD5哈希值(16字节)</returns>
        public static byte[] CalculateMd5(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return new byte[16];
            }

            using var md5 = MD5.Create();
            return md5.ComputeHash(data);
        }

        /// <summary>
        /// 计算MD5哈希值并转为32位十六进制字符串
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>MD5哈希字符串</returns>
        public static string CalculateMd5String(byte[] data)
        {
            var hash = CalculateMd5(data);
            var sb = new StringBuilder(32);

            for (var i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 计算SHA256哈希值
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>SHA256哈希值(32字节)</returns>
        public static byte[] CalculateSha256(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return new byte[32];
            }

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(data);
        }

        /// <summary>
        /// 计算SHA256哈希值并转为64位十六进制字符串
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>SHA256哈希字符串</returns>
        public static string CalculateSha256String(byte[] data)
        {
            var hash = CalculateSha256(data);
            var sb = new StringBuilder(64);

            for (var i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 计算简单的校验和(所有字节相加)
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>校验和</returns>
        public static uint CalculateSimpleSum(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0;
            }

            uint sum = 0;
            for (var i = 0; i < data.Length; i++)
            {
                sum += data[i];
            }

            return sum;
        }

        /// <summary>
        /// 计算Fletcher32校验和
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>校验和</returns>
        public static uint CalculateFletcher32(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0;
            }

            const uint mod = 65535;
            uint sum1 = 0,
                sum2 = 0;

            for (var i = 0; i < data.Length; i++)
            {
                sum1 = (sum1 + data[i]) % mod;
                sum2 = (sum2 + sum1) % mod;
            }

            return (sum2 << 16) | sum1;
        }

        /// <summary>
        /// 计算文件的校验和(使用CRC32算法)
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>校验和，如果文件不存在或出错则返回0</returns>
        public static uint CalculateFileCrc32(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return 0;
                }

                using var stream = File.OpenRead(filePath);
                const int bufferSize = 4096;
                var buffer = new byte[bufferSize];
                var crc = 0xFFFFFFFF;
                var polynomial = 0xEDB88320;
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                {
                    for (var i = 0; i < bytesRead; i++)
                    {
                        crc ^= buffer[i];
                        for (var j = 0; j < 8; j++)
                        {
                            crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : (crc >> 1);
                        }
                    }
                }

                return ~crc;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 计算流的CRC32校验和
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns>校验和</returns>
        public static uint CalculateStreamCrc32(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var currentPosition = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var crc = 0xFFFFFFFF;
                var polynomial = 0xEDB88320;
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var i = 0; i < bytesRead; i++)
                    {
                        crc ^= buffer[i];
                        for (var j = 0; j < 8; j++)
                        {
                            crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : (crc >> 1);
                        }
                    }
                }

                return ~crc;
            }
            finally
            {
                stream.Seek(currentPosition, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// 异步计算流的CRC32校验和
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>校验和</returns>
        public static async Task<uint> CalculateStreamCrc32Async(
            Stream stream,
            CancellationToken cancellationToken = default
        )
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var currentPosition = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var crc = 0xFFFFFFFF;
                var polynomial = 0xEDB88320;
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    for (var i = 0; i < bytesRead; i++)
                    {
                        crc ^= buffer[i];
                        for (var j = 0; j < 8; j++)
                        {
                            crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : (crc >> 1);
                        }
                    }
                }

                return ~crc;
            }
            finally
            {
                stream.Seek(currentPosition, SeekOrigin.Begin);
            }
        }
    }
}
