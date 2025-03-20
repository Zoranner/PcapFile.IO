using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KimoTech.PcapFile.IO.Utils
{
    /// <summary>
    /// 二进制数据转换工具类
    /// </summary>
    public static class BinaryConverter
    {
        /// <summary>
        /// 将字节数组转换为指定类型
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="bytes">字节数组</param>
        /// <param name="startIndex">起始索引</param>
        /// <param name="littleEndian">是否使用小端序</param>
        /// <returns>转换后的值</returns>
        public static T FromBytes<T>(byte[] bytes, int startIndex = 0, bool littleEndian = true)
            where T : struct
        {
            if (bytes == null || bytes.Length < startIndex + Marshal.SizeOf<T>())
            {
                throw new ArgumentException("字节数组长度不足");
            }

            // 如果需要处理字节序，先复制并转换字节数组
            if (littleEndian != BitConverter.IsLittleEndian)
            {
                var size = Marshal.SizeOf<T>();
                var temp = new byte[size];
                Array.Copy(bytes, startIndex, temp, 0, size);
                Array.Reverse(temp);
                bytes = temp;
                startIndex = 0;
            }

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject() + startIndex;
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// 将值转换为字节数组
        /// </summary>
        /// <typeparam name="T">源类型</typeparam>
        /// <param name="value">要转换的值</param>
        /// <param name="littleEndian">是否使用小端序</param>
        /// <returns>转换后的字节数组</returns>
        public static byte[] ToBytes<T>(T value, bool littleEndian = true)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var bytes = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(value, ptr, true);
                Marshal.Copy(ptr, bytes, 0, size);

                // 如果需要处理字节序，转换字节数组
                if (littleEndian != BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 将字符串转换为UTF8字节数组
        /// </summary>
        /// <param name="str">字符串</param>
        /// <returns>UTF8字节数组</returns>
        public static byte[] ToUtf8Bytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        /// <summary>
        /// 将UTF8字节数组转换为字符串
        /// </summary>
        /// <param name="bytes">UTF8字节数组</param>
        /// <returns>字符串</returns>
        public static string FromUtf8Bytes(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 将字节数组转换为Base64字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>Base64字符串</returns>
        public static string ToBase64String(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 将Base64字符串转换为字节数组
        /// </summary>
        /// <param name="base64">Base64字符串</param>
        /// <returns>字节数组</returns>
        public static byte[] FromBase64String(string base64)
        {
            return Convert.FromBase64String(base64);
        }
    }
}
