using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO;

namespace KimoTech.PcapFile.IO.UdpBroadcaster
{
    /// <summary>
    /// 广播协调器类，负责协调读取和发送线程，实现多线程处理
    /// </summary>
    public class BroadcastCoordinator : IDisposable
    {
        private readonly UdpBroadcaster _broadcaster;
        private readonly int _playbackSpeed;
        private readonly int _bufferSize;
        private readonly bool _verboseMode;
        private readonly BlockingCollection<DataPacket> _packetQueue;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Statistics _statistics;

        private Task _readerTask;
        private Task _broadcasterTask;
        private PcapReader _reader;
        private bool _isDisposed;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning =>
            (_readerTask != null && !_readerTask.IsCompleted)
            || (_broadcasterTask != null && !_broadcasterTask.IsCompleted);

        /// <summary>
        /// 统计信息
        /// </summary>
        public Statistics Statistics => _statistics;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="broadcaster">UDP广播器</param>
        /// <param name="playbackSpeed">播放速度</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="verboseMode">是否详细模式</param>
        public BroadcastCoordinator(
            UdpBroadcaster broadcaster,
            int playbackSpeed = 1,
            int bufferSize = 100,
            bool verboseMode = false
        )
        {
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _playbackSpeed = playbackSpeed > 0 ? playbackSpeed : 1;
            _bufferSize = bufferSize > 0 ? bufferSize : 100;
            _verboseMode = verboseMode;

            _packetQueue = new BlockingCollection<DataPacket>(_bufferSize);
            _cancellationSource = new CancellationTokenSource();
            _statistics = new Statistics();
        }

        /// <summary>
        /// 开始处理
        /// </summary>
        /// <param name="reader">PCAP读取器</param>
        public void Start(PcapReader reader)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("协调器已经在运行");
            }

            _reader = reader ?? throw new ArgumentNullException(nameof(reader));

            // 重置并启动统计
            _statistics.Reset();
            _statistics.Start();

            // 读取文件头
            var header = _reader.ReadHeader();
            if (_verboseMode)
            {
                Console.WriteLine($"PCAP文件版本: {header.MajorVersion}.{header.MinorVersion}");
            }

            // 启动读取线程
            _readerTask = Task.Run(() => ReaderThreadProc(_cancellationSource.Token));

            // 启动广播线程
            _broadcasterTask = Task.Run(() => BroadcasterThreadProc(_cancellationSource.Token));
        }

        /// <summary>
        /// 停止处理
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                // 取消所有操作
                _cancellationSource.Cancel();

                // 等待任务完成
                if (_readerTask != null)
                {
                    await _readerTask;
                }

                if (_broadcasterTask != null)
                {
                    await _broadcasterTask;
                }

                // 停止统计
                _statistics.Stop();

                // 清空队列
                _packetQueue.CompleteAdding();
                while (_packetQueue.TryTake(out _)) { }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_verboseMode)
                {
                    Console.WriteLine($"停止协调器时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 读取线程处理过程
        /// </summary>
        private async void ReaderThreadProc(CancellationToken cancellationToken)
        {
            try
            {
                // 每次读取批量数据包，使用合理的批次大小
                const int batchSize = 20;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // 读取一批数据包
                    var packets = await _reader.ReadPacketBatchAsync(batchSize, cancellationToken);

                    // 如果没有数据包，说明已经读取完毕
                    if (packets.Count == 0)
                    {
                        break;
                    }

                    // 添加到队列
                    foreach (var packet in packets)
                    {
                        _packetQueue.Add(packet, cancellationToken);
                    }

                    // 更新队列大小统计
                    _statistics.QueueSize = _packetQueue.Count;
                }

                // 标记队列已完成添加
                _packetQueue.CompleteAdding();
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_verboseMode)
                {
                    Console.WriteLine($"读取线程出错: {ex.Message}");
                }

                // 标记队列已完成添加，以便广播线程退出
                _packetQueue.CompleteAdding();
            }
        }

        /// <summary>
        /// 广播线程处理过程
        /// </summary>
        private void BroadcasterThreadProc(CancellationToken cancellationToken)
        {
            try
            {
                DateTime? lastPacketTime = null;

                // 处理队列中的数据包
                while (!_packetQueue.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    // 尝试从队列中获取数据包
                    if (_packetQueue.TryTake(out var packet, 100, cancellationToken))
                    {
                        // 计算延迟
                        if (lastPacketTime != null)
                        {
                            var delay = packet.CaptureTime - lastPacketTime.Value;
                            var delayMs = (int)(delay.TotalMilliseconds / _playbackSpeed);

                            if (delayMs > 0)
                            {
                                // 睡眠以模拟实际数据包间隔
                                Thread.Sleep(delayMs);
                            }
                        }

                        // 验证校验和 - 使用ChecksumCalculator计算校验和
                        var calculatedChecksum = ChecksumCalculator.CalculateCrc32(packet.Data);
                        var checksumValid = calculatedChecksum == packet.Checksum;

                        // 发送数据包
                        _broadcaster.SendPacket(packet);

                        // 更新状态
                        lastPacketTime = packet.CaptureTime;
                        _statistics.UpdatePacketProcessed(packet.Data.Length, checksumValid);
                        _statistics.QueueSize = _packetQueue.Count;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_verboseMode)
                {
                    Console.WriteLine($"广播线程出错: {ex.Message}");
                }
            }
            finally
            {
                // 停止统计
                _statistics.Stop();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // 停止任务
                    StopAsync().Wait();

                    // 释放资源
                    _cancellationSource?.Dispose();
                    _packetQueue?.Dispose();
                    _reader?.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}
