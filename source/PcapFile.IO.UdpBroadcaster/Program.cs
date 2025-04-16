using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO;

namespace KimoTech.PcapFile.IO.UdpBroadcaster
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("PCAP文件UDP广播工具");
            Console.WriteLine("=================\n");

            string testDirectory =
                @"D:\dotnet\Packages\PcapFile.IO\source\PcapFile.IO.Example\bin\Debug\net8.0\data\test_project";

            if (!Directory.Exists(testDirectory))
            {
                Console.WriteLine($"错误: 测试目录不存在 - {testDirectory}");
                return;
            }

            Console.WriteLine($"测试目录: {testDirectory}\n");

            string[] pcapFiles = Directory.GetFiles(testDirectory, "*.pcap");

            if (pcapFiles.Length == 0)
            {
                Console.WriteLine("未找到PCAP文件");
                return;
            }

            Console.WriteLine($"找到 {pcapFiles.Length} 个PCAP文件\n");

            // 输出找到的PCAP文件和大小
            foreach (string pcapFile in pcapFiles)
            {
                var fileInfo = new FileInfo(pcapFile);
                Console.WriteLine(
                    $"文件: {Path.GetFileName(pcapFile)}, 大小: {fileInfo.Length / 1024.0 / 1024.0:F2} MB"
                );
            }

            Console.WriteLine();

            // 默认参数
            var broadcastIp = IPAddress.Parse("255.255.255.255"); // 广播地址
            int port = 12345; // 端口
            int playbackSpeed = 1; // 播放速度，1为正常速度
            bool verboseMode = true; // 详细输出模式

            // 确认是否开始广播
            Console.WriteLine($"准备广播到: {broadcastIp}:{port}, 播放速度: {playbackSpeed}x");
            Console.Write("是否继续? (Y/N): ");
            var key = Console.ReadKey();
            Console.WriteLine();

            if (key.Key != ConsoleKey.Y)
            {
                Console.WriteLine("操作已取消");
                return;
            }

            // 遍历所有PCAP文件进行广播
            Console.WriteLine();
            foreach (string pcapFile in pcapFiles)
            {
                await BroadcastPcapFile(pcapFile, broadcastIp, port, playbackSpeed, verboseMode);
                Console.WriteLine("----------------------------------------\n");
            }

            Console.WriteLine($"广播完成，共处理 {pcapFiles.Length} 个文件");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        private static async Task TestReadPcapFile(string filePath)
        {
            Console.WriteLine($"验证文件: {Path.GetFileName(filePath)}");

            try
            {
                using (var reader = new PcapReader(filePath))
                {
                    // 读取文件头
                    var header = reader.ReadHeader();
                    var fileSize = reader.FileLength;
                    var fileInfo = new FileInfo(filePath);

                    Console.WriteLine($"文件头: {GetHeaderBytes(header)}");
                    Console.WriteLine(
                        $"字节序: {(header.MagicNumber == 0xA1B2C3D4 ? "小端" : "大端")}"
                    );
                    Console.WriteLine($"版本: {header.MajorVersion}.{header.MinorVersion}");

                    if (header.MajorVersion != 2 || header.MinorVersion != 4)
                    {
                        Console.WriteLine(
                            $"警告: 版本号 {header.MajorVersion}.{header.MinorVersion} 不是标准版本(2.4)"
                        );
                    }

                    Console.WriteLine(
                        $"时区偏移: {header.TimezoneOffset}, 精度设置: {header.TimestampAccuracy}"
                    );

                    var stats = new PacketStatistics();

                    // 读取数据包
                    Console.WriteLine("\n开始验证数据包...");

                    int batchSize = 10;
                    bool showAllPackets = false; // 设置为true可以显示所有数据包信息
                    bool showDataPreview = false; // 设置为true可以显示数据预览

                    if (showAllPackets)
                    {
                        Console.WriteLine("\n数据包详细信息:");
                        Console.WriteLine("序号\t时间戳\t\t\t\t\t数据大小\t校验和\t\t校验结果");
                        Console.WriteLine(
                            "--------------------------------------------------------"
                        );
                    }

                    while (true)
                    {
                        var packets = await reader.ReadPacketBatchAsync(batchSize);

                        if (packets.Count == 0)
                        {
                            break;
                        }

                        foreach (var packet in packets)
                        {
                            // 更新统计信息
                            stats.PacketCount++;

                            // 计算校验和并验证
                            var calculatedChecksum = ChecksumCalculator.CalculateCrc32(packet.Data);
                            var checksumValid = calculatedChecksum == packet.Header.Checksum;

                            // 更新统计数据
                            stats.PacketList.Add(packet);
                            stats.PacketNumbers[packet] = stats.PacketCount;
                            stats.CalculatedChecksums[packet] = calculatedChecksum;
                            stats.ChecksumResults[packet] = checksumValid;

                            // 更新首个和最后一个包的时间戳
                            if (stats.PacketCount == 1)
                            {
                                stats.FirstPacketTime = packet.CaptureTime;
                            }
                            stats.LastPacketTime = packet.CaptureTime;

                            // 更新包大小统计
                            stats.TotalSize += packet.PacketLength;

                            if (stats.PacketCount == 1 || packet.PacketLength < stats.MinPacketSize)
                            {
                                stats.MinPacketSize = packet.PacketLength;
                            }

                            if (packet.PacketLength > stats.MaxPacketSize)
                            {
                                stats.MaxPacketSize = packet.PacketLength;
                            }

                            // 校验结果统计
                            if (checksumValid)
                            {
                                stats.ValidPackets++;
                            }
                            else
                            {
                                stats.InvalidPackets++;
                            }

                            // 输出详细信息
                            if (showAllPackets)
                            {
                                Console.WriteLine(
                                    $"{stats.PacketCount}\t{packet.CaptureTime:yyyy-MM-dd HH:mm:ss.fffffff}\t{packet.PacketLength}\t\t0x{packet.Header.Checksum:X8}\t{(checksumValid ? "成功" : "失败")}"
                                );

                                // 如果需要，显示数据预览
                                if (showDataPreview && packet.Data.Length > 0)
                                {
                                    Console.WriteLine("数据预览:");
                                    PrintDataPreview(packet.Data);
                                    Console.WriteLine();
                                }
                            }
                        }

                        // 每100个包输出一次进度
                        if (!showAllPackets && stats.PacketCount % 100 == 0)
                        {
                            Console.Write($"\r验证数据包中: {stats.PacketCount}");
                        }
                    }

                    if (!showAllPackets)
                    {
                        Console.WriteLine($"\r验证数据包完成: 共 {stats.PacketCount} 个数据包");
                    }
                    else
                    {
                        Console.WriteLine(
                            "--------------------------------------------------------"
                        );
                    }

                    // 计算平均数据包率
                    if (stats.PacketCount > 1)
                    {
                        var duration = stats.LastPacketTime - stats.FirstPacketTime;
                        if (duration.TotalSeconds > 0)
                        {
                            stats.AveragePacketRate = stats.PacketCount / duration.TotalSeconds;
                        }
                    }

                    // 打印统计信息
                    PrintStatistics(filePath, stats);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证过程中出错: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部错误: {ex.InnerException.Message}");
                }
            }
        }

        private static string GetHeaderBytes(PcapFileHeader header)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(header.MagicNumber);
            writer.Write(header.MajorVersion);
            writer.Write(header.MinorVersion);
            writer.Write(header.TimezoneOffset);
            writer.Write(header.TimestampAccuracy);
            // PcapFile.IO库中PcapFileHeader没有SnapshotLength和LinkLayerType属性
            // 写入默认值，保持与验证器格式一致
            writer.Write((uint)65535); // 默认SnapshotLength
            writer.Write((uint)1); // 默认LinkLayerType (1表示以太网)

            return BitConverter.ToString(ms.ToArray()).Replace("-", " ");
        }

        private static void PrintStatistics(string filePath, PacketStatistics stats)
        {
            Console.WriteLine("\n数据文件统计信息:");
            Console.WriteLine($"文件名称: {Path.GetFileName(filePath)}");
            Console.WriteLine($"文件大小: {new FileInfo(filePath).Length / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"数据包总数: {stats.PacketCount}");

            if (stats.PacketCount > 0)
            {
                Console.WriteLine(
                    $"校验成功数: {stats.ValidPackets} ({stats.ValidPackets * 100.0 / stats.PacketCount:F2}%)"
                );
                Console.WriteLine(
                    $"校验失败数: {stats.InvalidPackets} ({stats.InvalidPackets * 100.0 / stats.PacketCount:F2}%)"
                );
                Console.WriteLine(
                    $"首个数据包时间: {stats.FirstPacketTime:yyyy-MM-dd HH:mm:ss.fffffff}"
                );
                Console.WriteLine(
                    $"最后数据包时间: {stats.LastPacketTime:yyyy-MM-dd HH:mm:ss.fffffff}"
                );
                Console.WriteLine(
                    $"时间跨度: {(stats.LastPacketTime - stats.FirstPacketTime).TotalSeconds:F3} 秒"
                );
                Console.WriteLine($"最小数据包大小: {stats.MinPacketSize} 字节");
                Console.WriteLine($"最大数据包大小: {stats.MaxPacketSize} 字节");
                Console.WriteLine(
                    $"平均数据包大小: {stats.TotalSize / (ulong)stats.PacketCount:F2} 字节"
                );
                Console.WriteLine($"总数据大小: {stats.TotalSize / 1024.0 / 1024.0:F2} MB");
                Console.WriteLine($"平均数据包率: {stats.AveragePacketRate:F2} 包/秒");

                // 如果有校验失败的包，输出列表
                if (stats.InvalidPackets > 0)
                {
                    Console.WriteLine("\n校验失败的数据包列表:");
                    var count = 0;
                    foreach (var packet in stats.PacketList)
                    {
                        if (!stats.ChecksumResults[packet])
                        {
                            var packetNumber = stats.PacketNumbers[packet];
                            var calculatedChecksum = stats.CalculatedChecksums[packet];

                            Console.WriteLine(
                                $"  - 数据包 #{packetNumber}: 时间={packet.CaptureTime:yyyy-MM-dd HH:mm:ss.fffffff}, 大小={packet.PacketLength}字节, 校验和=0x{packet.Header.Checksum:X8}/0x{calculatedChecksum:X8}"
                            );
                            count++;
                            if (count >= 10)
                            {
                                Console.WriteLine(
                                    $"  ... 还有 {stats.InvalidPackets - 10} 个校验失败的包未显示"
                                );
                                break;
                            }
                        }
                    }
                }
            }

            Console.WriteLine(
                $"\n验证结果: {(stats.InvalidPackets == 0 ? "全部数据包校验成功" : "存在校验失败的数据包")}"
            );
        }

        private static void PrintDataPreview(byte[] data, int maxBytes = 64)
        {
            var previewLength = Math.Min(data.Length, maxBytes);
            var hexBuilder = new StringBuilder();
            var asciiBuilder = new StringBuilder();

            for (var i = 0; i < previewLength; i++)
            {
                if (i % 16 == 0 && i > 0)
                {
                    Console.WriteLine(
                        $"  {hexBuilder.ToString().PadRight(48)} | {asciiBuilder.ToString()}"
                    );
                    hexBuilder.Clear();
                    asciiBuilder.Clear();
                }

                hexBuilder.Append(data[i].ToString("X2") + " ");

                // ASCII显示，非可打印字符显示为'.'
                if (data[i] >= 32 && data[i] <= 126)
                {
                    asciiBuilder.Append((char)data[i]);
                }
                else
                {
                    asciiBuilder.Append('.');
                }
            }

            // 输出最后一行
            if (hexBuilder.Length > 0)
            {
                Console.WriteLine(
                    $"  {hexBuilder.ToString().PadRight(48)} | {asciiBuilder.ToString()}"
                );
            }

            if (data.Length > maxBytes)
            {
                Console.WriteLine($"  ... 共 {data.Length} 字节");
            }
        }

        /// <summary>
        /// 广播单个PCAP文件
        /// </summary>
        private static async Task BroadcastPcapFile(
            string filePath,
            IPAddress broadcastIp,
            int port,
            int playbackSpeed,
            bool verboseMode
        )
        {
            try
            {
                Console.WriteLine($"开始广播文件: {Path.GetFileName(filePath)}");

                // 创建UDP广播器
                var endPoint = new IPEndPoint(broadcastIp, port);
                using var broadcaster = new UdpBroadcaster(endPoint);

                Console.WriteLine($"广播目标: {broadcastIp}:{port}");
                Console.WriteLine($"播放速度: {playbackSpeed}x");

                // 读取PCAP文件
                using var reader = new PcapReader(filePath);

                // 读取文件头
                var header = reader.ReadHeader();
                Console.WriteLine($"文件版本: {header.MajorVersion}.{header.MinorVersion}");

                // 创建并启动广播协调器
                var bufferSize = 1000; // 缓冲区大小
                using var coordinator = new BroadcastCoordinator(
                    broadcaster,
                    playbackSpeed,
                    bufferSize,
                    verboseMode
                );

                // 启动协调器
                coordinator.Start(reader);

                // 显示进度
                var cancellationTokenSource = new CancellationTokenSource();
                var progressTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellationTokenSource.IsCancellationRequested)
                        {
                            var stats = coordinator.Statistics;
                            Console.Write(
                                $"\r已广播: {stats.ProcessedPackets} 个数据包, "
                                    + $"{stats.ProcessedBytes / 1024.0 / 1024.0:F2} MB, "
                                    + $"每秒 {stats.PacketsPerSecond:F2} 个数据包, "
                                    + $"校验错误: {stats.ChecksumErrors}"
                            );

                            await Task.Delay(1000, cancellationTokenSource.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消
                    }
                });

                // 等待广播完成
                while (coordinator.IsRunning)
                {
                    await Task.Delay(500);
                }

                // 取消进度显示
                cancellationTokenSource.Cancel();
                try
                {
                    await progressTask;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }

                // 停止协调器
                await coordinator.StopAsync();

                // 打印最终统计信息
                var finalStats = coordinator.Statistics;
                Console.WriteLine();
                Console.WriteLine("\n广播统计信息：");
                Console.WriteLine($"总包数: {finalStats.ProcessedPackets}");
                Console.WriteLine($"总字节数: {finalStats.ProcessedBytes / 1024.0 / 1024.0:F2} MB");
                Console.WriteLine($"运行时间: {finalStats.ElapsedTime.TotalSeconds:F2} 秒");
                Console.WriteLine($"平均包率: {finalStats.PacketsPerSecond:F2} 包/秒");
                Console.WriteLine($"校验错误: {finalStats.ChecksumErrors}");
                Console.WriteLine($"最小包大小: {finalStats.MinPacketSize} 字节");
                Console.WriteLine($"最大包大小: {finalStats.MaxPacketSize} 字节");

                Console.WriteLine($"广播完成: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n广播出错: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部错误: {ex.InnerException.Message}");
                }
            }
        }
    }

    class PacketStatistics
    {
        public long PacketCount { get; set; } = 0;
        public long ValidPackets { get; set; } = 0;
        public long InvalidPackets { get; set; } = 0;
        public DateTime FirstPacketTime { get; set; }
        public DateTime LastPacketTime { get; set; }
        public uint MinPacketSize { get; set; } = uint.MaxValue;
        public uint MaxPacketSize { get; set; } = 0;
        public ulong TotalSize { get; set; } = 0;
        public double AveragePacketRate { get; set; } = 0; // 平均数据包率（包/秒）

        // 数据包列表
        public List<DataPacket> PacketList { get; set; } = new List<DataPacket>();

        // 额外跟踪信息的字典
        public Dictionary<DataPacket, long> PacketNumbers { get; set; } =
            new Dictionary<DataPacket, long>();
        public Dictionary<DataPacket, bool> ChecksumResults { get; set; } =
            new Dictionary<DataPacket, bool>();
        public Dictionary<DataPacket, uint> CalculatedChecksums { get; set; } =
            new Dictionary<DataPacket, uint>();
    }
}
