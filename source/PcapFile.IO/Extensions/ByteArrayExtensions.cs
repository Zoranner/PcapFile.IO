using System;
using System.Text;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 字节数组扩展方法
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// 获取字节数组的子数组
        /// </summary>
        /// <param name="bytes">源字节数组</param>
        /// <param name="startIndex">起始索引</param>
        /// <param name="length">长度</param>
        /// <returns>子数组</returns>
        public static byte[] SubArray(this byte[] bytes, int startIndex, int length)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (startIndex < 0 || startIndex >= bytes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (length < 0 || startIndex + length > bytes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            var result = new byte[length];
            Array.Copy(bytes, startIndex, result, 0, length);
            return result;
        }

        /// <summary>
        /// 将字节数组转换为十六进制字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="separator">分隔符</param>
        /// <returns>十六进制字符串</returns>
        public static string ToHexString(this byte[] bytes, string separator = "")
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(bytes.Length * (2 + separator.Length));
            for (var i = 0; i < bytes.Length; i++)
            {
                if (i > 0 && !string.IsNullOrEmpty(separator))
                {
                    sb.Append(separator);
                }

                sb.Append(bytes[i].ToString("X2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将字节数组转换为Base64字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>Base64字符串</returns>
        public static string ToBase64String(this byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 将字节数组转换为UTF8字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>UTF8字符串</returns>
        public static string ToUtf8String(this byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 将字节数组转换为指定编码的字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="encoding">编码</param>
        /// <returns>字符串</returns>
        public static string ToString(this byte[] bytes, Encoding encoding)
        {
            return encoding.GetString(bytes);
        }

        /// <summary>
        /// 比较两个字节数组是否相等
        /// </summary>
        /// <param name="bytes1">第一个字节数组</param>
        /// <param name="bytes2">第二个字节数组</param>
        /// <returns>是否相等</returns>
        public static bool Equals(this byte[] bytes1, byte[] bytes2)
        {
            if (bytes1 == null || bytes2 == null)
            {
                return bytes1 == bytes2;
            }

            if (bytes1.Length != bytes2.Length)
            {
                return false;
            }

            for (var i = 0; i < bytes1.Length; i++)
            {
                if (bytes1[i] != bytes2[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 计算字节数组的哈希值
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>哈希值</returns>
        public static int GetHashCode(this byte[] bytes)
        {
            if (bytes == null)
            {
                return 0;
            }

            var hash = 17;
            for (var i = 0; i < bytes.Length; i++)
            {
                hash = hash * 31 + bytes[i];
            }

            return hash;
        }
    }
}
