using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace KimoTech.PcapFile.IO.UdpTransmitter
{
    /// <summary>
    /// 统计信息类，收集和显示传输过程中的性能统计数据
    /// </summary>
    public class Statistics
    {
        private readonly object _Lock = new object();
        private readonly Stopwatch _Stopwatch = new Stopwatch();
        private long _LastProcessedPackets = 0;
        private DateTime _LastSpeedCalculationTime = DateTime.Now;

        /// <summary>
        /// 已处理数据包数量
        /// </summary>
        public long ProcessedPackets { get; private set; }

        /// <summary>
        /// 已处理数据字节数
        /// </summary>
        public long ProcessedBytes { get; private set; }

        /// <summary>
        /// 校验和错误数量
        /// </summary>
        public long ChecksumErrors { get; private set; }

        /// <summary>
        /// 当前缓冲队列大小
        /// </summary>
        public int QueueSize { get; set; }

        /// <summary>
        /// 每秒处理的数据包数量
        /// </summary>
        public double PacketsPerSecond { get; private set; }

        /// <summary>
        /// 最小数据包大小(字节)
        /// </summary>
        public long MinPacketSize { get; private set; } = long.MaxValue;

        /// <summary>
        /// 最大数据包大小(字节)
        /// </summary>
        public long MaxPacketSize { get; private set; }

        /// <summary>
        /// 运行时间
        /// </summary>
        public TimeSpan ElapsedTime => _Stopwatch.Elapsed;

        /// <summary>
        /// 构造函数
        /// </summary>
        public Statistics()
        {
            Reset();
        }

        /// <summary>
        /// 开始统计
        /// </summary>
        public void Start()
        {
            lock (_Lock)
            {
                _Stopwatch.Start();
            }
        }

        /// <summary>
        /// 停止统计
        /// </summary>
        public void Stop()
        {
            lock (_Lock)
            {
                _Stopwatch.Stop();
            }
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void Reset()
        {
            lock (_Lock)
            {
                ProcessedPackets = 0;
                ProcessedBytes = 0;
                ChecksumErrors = 0;
                QueueSize = 0;
                PacketsPerSecond = 0;
                MinPacketSize = long.MaxValue;
                MaxPacketSize = 0;
                _LastProcessedPackets = 0;
                _LastSpeedCalculationTime = DateTime.Now;
                _Stopwatch.Reset();
            }
        }

        /// <summary>
        /// 更新数据包处理信息
        /// </summary>
        /// <param name="packetSize">数据包大小</param>
        /// <param name="isChecksumValid">校验和是否有效</param>
        public void UpdatePacketProcessed(long packetSize, bool isChecksumValid)
        {
            lock (_Lock)
            {
                ProcessedPackets++;
                ProcessedBytes += packetSize;

                if (!isChecksumValid)
                {
                    ChecksumErrors++;
                }

                // 更新包大小统计
                if (packetSize < MinPacketSize)
                {
                    MinPacketSize = packetSize;
                }

                if (packetSize > MaxPacketSize)
                {
                    MaxPacketSize = packetSize;
                }

                // 每秒更新一次速度统计
                var now = DateTime.Now;
                var timeSpan = now - _LastSpeedCalculationTime;
                if (timeSpan.TotalSeconds >= 1)
                {
                    var packetDelta = ProcessedPackets - _LastProcessedPackets;
                    PacketsPerSecond = packetDelta / timeSpan.TotalSeconds;
                    _LastProcessedPackets = ProcessedPackets;
                    _LastSpeedCalculationTime = now;
                }
            }
        }
    }
}
