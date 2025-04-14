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

        /// <summary>
        /// 将 DateTime 转换为 Unix 时间戳(秒)
        /// </summary>
        /// <param name="dateTime">日期时间</param>
        /// <returns>Unix 时间戳(秒)</returns>
        public static uint ToUnixTimeSeconds(this DateTime dateTime)
        {
            return (uint)(dateTime.ToUniversalTime() - _UnixEpoch).TotalSeconds;
        }

        /// <summary>
        /// 获取 DateTime 的纳秒部分
        /// </summary>
        /// <param name="dateTime">日期时间</param>
        /// <returns>纳秒值</returns>
        public static uint GetNanoseconds(this DateTime dateTime)
        {
            // 获取纳秒部分
            // 将Ticks转换为纳秒，1 tick = 100纳秒
            // 只保留时分秒以内的纳秒部分
            long ticksInSecond = dateTime.Ticks % TimeSpan.TicksPerSecond;

            // 将Ticks转换为纳秒，1 tick = 100纳秒
            uint nanoseconds = (uint)(ticksInSecond * 100);

            // 注意：由于DateTime的精度限制（最小单位是100纳秒），
            // 所以nanoseconds的最后两位始终为0
            // 但保留这种结构可以在将来支持更高精度的时间类型

            return nanoseconds;
        }

        /// <summary>
        /// 将 Unix 时间戳(秒)和纳秒部分转换为 DateTime
        /// </summary>
        /// <param name="seconds">Unix 时间戳(秒)</param>
        /// <param name="nanoseconds">纳秒部分</param>
        /// <returns>日期时间</returns>
        public static DateTime FromUnixTimeWithNanoseconds(uint seconds, uint nanoseconds)
        {
            // 先添加秒部分
            DateTime result = _UnixEpoch.AddSeconds(seconds);

            // 再添加纳秒部分（转换为ticks，1纳秒 = 0.01 ticks）
            // 注意：由于DateTime的精度限制，实际上只有100纳秒的精度
            // 所以只取纳秒的前7位有效（去掉最后两位）
            long ticksToAdd = nanoseconds / 100;
            result = result.AddTicks(ticksToAdd);

            return result.ToLocalTime();
        }
    }
}
