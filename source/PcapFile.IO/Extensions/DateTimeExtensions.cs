using System;

namespace KimoTech.PcapFile.IO.Extensions
{
    /// <summary>
    /// DateTime 扩展方法
    /// </summary>
    public static class DateTimeExtensions
    {
        private static readonly DateTime _UnixEpoch = new DateTime(
            1970,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc
        );

        /// <summary>
        /// 将 DateTime 转换为 Unix 时间戳(毫秒)
        /// </summary>
        /// <param name="dateTime">日期时间</param>
        /// <returns>Unix 时间戳(毫秒)</returns>
        public static long ToUnixTimeMilliseconds(this DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - _UnixEpoch).TotalMilliseconds;
        }

        /// <summary>
        /// 将 Unix 时间戳(毫秒)转换为 DateTime
        /// </summary>
        /// <param name="milliseconds">Unix 时间戳(毫秒)</param>
        /// <returns>日期时间</returns>
        public static DateTime FromUnixTimeMilliseconds(this long milliseconds)
        {
            return _UnixEpoch.AddMilliseconds(milliseconds).ToLocalTime();
        }
    }
}
