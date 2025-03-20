using System;

namespace KimoTech.PcapFile.IO
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
        /// 将 DateTime 转换为 Unix 时间戳（秒）
        /// </summary>
        /// <param name="dateTime">时间</param>
        /// <returns>Unix 时间戳（秒）</returns>
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - _UnixEpoch).TotalSeconds;
        }

        /// <summary>
        /// 将 DateTime 转换为 Unix 时间戳（微秒）
        /// </summary>
        /// <param name="dateTime">时间</param>
        /// <returns>Unix 时间戳（微秒）</returns>
        public static ulong ToUnixTimeMicroseconds(this DateTime dateTime)
        {
            var elapsedTime = dateTime.ToUniversalTime() - _UnixEpoch;
            return (ulong)(elapsedTime.TotalSeconds * 1_000_000);
        }

        /// <summary>
        /// 将 Unix 时间戳（秒）转换为 DateTime
        /// </summary>
        /// <param name="unixTimeSeconds">Unix 时间戳（秒）</param>
        /// <returns>DateTime</returns>
        public static DateTime FromUnixTimeSeconds(this long unixTimeSeconds)
        {
            return _UnixEpoch.AddSeconds(unixTimeSeconds);
        }

        /// <summary>
        /// 将 Unix 时间戳（微秒）转换为 DateTime
        /// </summary>
        /// <param name="unixTimeMicroseconds">Unix 时间戳（微秒）</param>
        /// <returns>DateTime</returns>
        public static DateTime FromUnixTimeMicroseconds(this ulong unixTimeMicroseconds)
        {
            var seconds = unixTimeMicroseconds / 1_000_000;
            var microseconds = unixTimeMicroseconds % 1_000_000;
            return _UnixEpoch.AddSeconds(seconds).AddMicroseconds(microseconds);
        }

        /// <summary>
        /// 添加微秒
        /// </summary>
        /// <param name="dateTime">时间</param>
        /// <param name="microseconds">微秒数</param>
        /// <returns>新的时间</returns>
        public static DateTime AddMicroseconds(this DateTime dateTime, double microseconds)
        {
            return dateTime.AddTicks((long)(microseconds * TimeSpan.TicksPerSecond / 1_000_000));
        }

        /// <summary>
        /// 获取微秒部分
        /// </summary>
        /// <param name="dateTime">时间</param>
        /// <returns>微秒数（0-999999）</returns>
        public static int GetMicroseconds(this DateTime dateTime)
        {
            return (int)(
                dateTime.Ticks % TimeSpan.TicksPerSecond / (TimeSpan.TicksPerSecond / 1_000_000)
            );
        }

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
