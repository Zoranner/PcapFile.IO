using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 二进制序列化工具类
    /// </summary>
    public static class BinarySerializer
    {
        /// <summary>
        /// 将结构体序列化为字节数组
        /// </summary>
        public static byte[] Serialize<T>(T structure)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var buffer = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(structure, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return buffer;
        }

        /// <summary>
        /// 将字节数组反序列化为结构体
        /// </summary>
        public static T Deserialize<T>(byte[] buffer)
            where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.Copy(buffer, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 将字符串序列化为固定长度的字节数组
        /// </summary>
        public static byte[] SerializeString(string value, int length)
        {
            var buffer = new byte[length];
            if (!string.IsNullOrEmpty(value))
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                Array.Copy(bytes, buffer, Math.Min(bytes.Length, length));
            }

            return buffer;
        }

        /// <summary>
        /// 将固定长度的字节数组反序列化为字符串
        /// </summary>
        public static string DeserializeString(byte[] buffer)
        {
            var nullIndex = Array.IndexOf(buffer, (byte)0);
            if (nullIndex >= 0)
            {
                Array.Resize(ref buffer, nullIndex);
            }

            return Encoding.UTF8.GetString(buffer);
        }
    }
}
