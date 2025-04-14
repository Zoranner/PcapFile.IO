using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// CRC32校验和计算工具
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
        /// 计算字节数组的CRC32值
        /// </summary>
        public static uint CalculateCrc32(byte[] data, int offset, int length)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || offset >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || offset + length > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            using var crc32 = new CRC32();
            return crc32.ComputeHash(data, offset, length);
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

    /// <summary>
    /// CRC32算法实现
    /// </summary>
    internal class CRC32 : HashAlgorithm
    {
        private static readonly uint[] _Table;
        private uint _Value;

        static CRC32()
        {
            _Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var value = i;
                for (var j = 0; j < 8; j++)
                {
                    value = (value & 1) == 1 ? (value >> 1) ^ 0xEDB88320 : value >> 1;
                }

                _Table[i] = value;
            }
        }

        public CRC32()
        {
            HashSizeValue = 32;
            Initialize();
        }

        public override void Initialize()
        {
            _Value = 0xFFFFFFFF;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            for (var i = ibStart; i < ibStart + cbSize; i++)
            {
                _Value = (_Value >> 8) ^ _Table[(_Value & 0xFF) ^ array[i]];
            }
        }

        protected override byte[] HashFinal()
        {
            _Value = ~_Value;
            return BitConverter.GetBytes(_Value);
        }

        public new uint ComputeHash(byte[] buffer, int offset, int count)
        {
            HashCore(buffer, offset, count);
            return ~_Value;
        }
    }
}
