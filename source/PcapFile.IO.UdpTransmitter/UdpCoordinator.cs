using System.Collections.Concurrent;

namespace KimoTech.PcapFile.IO.UdpTransmitter
{
    /// <summary>
    /// 传输协调器类，负责协调读取和发送线程，实现多线程处理
    /// </summary>
    public class UdpCoordinator : IDisposable
    {
        private readonly UdpTransmitter _Transmitter;
        private readonly int _PlaybackSpeed;
        private readonly int _BufferSize;
        private readonly bool _VerboseMode;
        private readonly BlockingCollection<DataPacket> _PacketQueue;
        private readonly CancellationTokenSource _CancellationSource;
        private Task _ReaderTask;
        private Task _TransmitterTask;
        private IPcapReader _Reader;
        private bool _IsDisposed;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning =>
            _ReaderTask != null && !_ReaderTask.IsCompleted
            || _TransmitterTask != null && !_TransmitterTask.IsCompleted;

        /// <summary>
        /// 统计信息
        /// </summary>
        public Statistics Statistics { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="transmitter">UDP传输器</param>
        /// <param name="playbackSpeed">播放速度</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="verboseMode">是否详细模式</param>
        public UdpCoordinator(
            UdpTransmitter transmitter,
            int playbackSpeed = 1,
            int bufferSize = 100,
            bool verboseMode = false
        )
        {
            _Transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));
            _PlaybackSpeed = playbackSpeed > 0 ? playbackSpeed : 1;
            _BufferSize = bufferSize > 0 ? bufferSize : 100;
            _VerboseMode = verboseMode;

            _PacketQueue = new BlockingCollection<DataPacket>(_BufferSize);
            _CancellationSource = new CancellationTokenSource();
            Statistics = new Statistics();
        }

        /// <summary>
        /// 开始处理
        /// </summary>
        /// <param name="reader">PCAP读取器</param>
        public void Start(IPcapReader reader)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("协调器已经在运行");
            }

            _Reader = reader ?? throw new ArgumentNullException(nameof(reader));

            // 重置并启动统计
            Statistics.Reset();
            Statistics.Start();

            if (_VerboseMode)
            {
                Console.WriteLine($"已打开PCAP文件: {_Reader.FilePath}");
                Console.WriteLine($"总数据包数量: {_Reader.TotalPacketCount}");
            }

            // 启动读取线程
            _ReaderTask = Task.Run(() => ReaderThreadProc(_CancellationSource.Token));

            // 启动传输线程
            _TransmitterTask = Task.Run(() => TransmitterThreadProc(_CancellationSource.Token));
        }

        /// <summary>
        /// 停止处理
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                // 取消所有操作
                _CancellationSource.Cancel();

                // 等待任务完成
                if (_ReaderTask != null)
                {
                    await _ReaderTask;
                }

                if (_TransmitterTask != null)
                {
                    await _TransmitterTask;
                }

                // 停止统计
                Statistics.Stop();

                // 清空队列
                _PacketQueue.CompleteAdding();
                while (_PacketQueue.TryTake(out _)) { }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_VerboseMode)
                {
                    Console.WriteLine($"停止协调器时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 读取线程处理过程
        /// </summary>
        private void ReaderThreadProc(CancellationToken cancellationToken)
        {
            try
            {
                // 每次读取批量数据包，使用合理的批次大小
                const int batchSize = 20;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // 读取一批数据包
                    var packets = _Reader.ReadPackets(batchSize).ToList();

                    // 如果没有数据包，说明已经读取完毕
                    if (packets.Count == 0)
                    {
                        break;
                    }

                    // 添加到队列
                    foreach (var packet in packets)
                    {
                        _PacketQueue.Add(packet, cancellationToken);
                    }

                    // 更新队列大小统计
                    Statistics.QueueSize = _PacketQueue.Count;
                }

                // 标记队列已完成添加
                _PacketQueue.CompleteAdding();
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_VerboseMode)
                {
                    Console.WriteLine($"读取线程出错: {ex.Message}");
                }

                // 标记队列已完成添加，以便传输线程退出
                _PacketQueue.CompleteAdding();
            }
        }

        /// <summary>
        /// 传输线程处理过程
        /// </summary>
        private void TransmitterThreadProc(CancellationToken cancellationToken)
        {
            try
            {
                DateTime? lastPacketTime = null;

                // 处理队列中的数据包
                while (!_PacketQueue.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    // 尝试从队列中获取数据包
                    if (_PacketQueue.TryTake(out var packet, 100, cancellationToken))
                    {
                        // 计算延迟
                        if (lastPacketTime != null)
                        {
                            var delay = packet.CaptureTime - lastPacketTime.Value;
                            var delayMs = (int)(delay.TotalMilliseconds / _PlaybackSpeed);

                            if (delayMs > 0)
                            {
                                // 睡眠以模拟实际数据包间隔
                                Thread.Sleep(delayMs);
                            }
                        }

                        // 验证校验和 - 使用ChecksumCalculator计算校验和
                        var calculatedChecksum = ChecksumCalculator.CalculateCrc32(packet.Data);
                        var checksumValid = calculatedChecksum == packet.Header.Checksum;

                        // 发送数据包
                        _Transmitter.SendPacket(packet);

                        // 更新状态
                        lastPacketTime = packet.CaptureTime;
                        Statistics.UpdatePacketProcessed(packet.Data.Count, checksumValid);
                        Statistics.QueueSize = _PacketQueue.Count;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_VerboseMode)
                {
                    Console.WriteLine($"传输线程出错: {ex.Message}");
                }
            }
            finally
            {
                // 停止统计
                Statistics.Stop();
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
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    // 停止任务
                    StopAsync().Wait();

                    // 释放资源
                    _CancellationSource?.Dispose();
                    _PacketQueue?.Dispose();
                    _Reader?.Dispose();
                }

                _IsDisposed = true;
            }
        }
    }
}
