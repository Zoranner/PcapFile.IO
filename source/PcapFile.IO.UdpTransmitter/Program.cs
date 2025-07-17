using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KimoTech.PcapFile.IO.UdpTransmitter
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("PCAP文件UDP网络传输工具");
            Console.WriteLine("=====================\n");

            // 默认参数
            var targetIp = IPAddress.Parse("255.255.255.255"); // 默认传输地址
            var port = 12345; // 端口
            var playbackSpeed = 1; // 播放速度，1为正常速度
            var verboseMode = true; // 详细输出模式
            var bufferSize = 1000; // 缓冲区大小
            var networkMode = NetworkMode.Broadcast; // 默认传输模式

            // 处理命令行参数
            if (args.Length < 2)
            {
                ShowUsage();
                return;
            }

            var baseDirectory = args[0];
            var projectName = args[1];

            // 解析其他命令行参数
            for (var i = 2; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-a":
                    case "--address":
                        if (i + 1 < args.Length && IPAddress.TryParse(args[++i], out var ip))
                        {
                            targetIp = ip;
                        }

                        break;
                    case "-p":
                    case "--port":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var p))
                        {
                            port = p;
                        }

                        break;
                    case "-s":
                    case "--speed":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var s))
                        {
                            playbackSpeed = s;
                        }

                        break;
                    case "-b":
                    case "--buffer":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var b))
                        {
                            bufferSize = b;
                        }

                        break;
                    case "-m":
                    case "--mode":
                        if (
                            i + 1 < args.Length
                            && Enum.TryParse<NetworkMode>(args[++i], true, out var mode)
                        )
                        {
                            networkMode = mode;
                        }

                        break;
                    case "--quiet":
                        verboseMode = false;
                        break;
                }
            }

            // 如果没有明确指定模式，根据IP地址自动判断
            if (
                args.Length > 2
                && !args.Any(arg => arg.ToLower() == "-m" || arg.ToLower() == "--mode")
            )
            {
                networkMode = DetermineNetworkMode(targetIp);
            }

            try
            {
                // 检查基础目录
                if (!Directory.Exists(baseDirectory))
                {
                    Console.WriteLine($"错误: 基础目录不存在 - {baseDirectory}");
                    return;
                }

                // 检查数据集目录
                var projectDirectory = Path.Combine(baseDirectory, projectName);
                if (!Directory.Exists(projectDirectory))
                {
                    Console.WriteLine($"错误: 数据集目录不存在 - {projectDirectory}");
                    Console.WriteLine(
                        $"请确保在基础目录 '{baseDirectory}' 下存在名为 '{projectName}' 的数据集目录"
                    );
                    return;
                }

                Console.WriteLine($"基础目录: {baseDirectory}");
                Console.WriteLine($"数据集名称: {projectName}");
                Console.WriteLine($"数据集目录: {projectDirectory}");
                Console.WriteLine($"网络模式: {GetNetworkModeDescription(networkMode)}");
                Console.WriteLine($"目标地址: {targetIp}:{port}");
                Console.WriteLine($"播放速度: {playbackSpeed}x");
                Console.WriteLine();

                // 预览数据集中的PCAP文件
                var pcapFiles = Directory
                    .GetFiles(projectDirectory, "*.pcap", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f)
                    .ToArray();

                if (pcapFiles.Length == 0)
                {
                    Console.WriteLine($"错误: 数据集目录中未找到PCAP文件 - {projectDirectory}");
                    return;
                }

                Console.WriteLine($"发现 {pcapFiles.Length} 个PCAP文件: ");
                foreach (var pcapFile in pcapFiles)
                {
                    var fileInfo = new FileInfo(pcapFile);
                    Console.WriteLine(
                        $"  - {Path.GetFileName(pcapFile)} ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)"
                    );
                }

                Console.WriteLine();

                // 确认是否开始传输
                Console.Write("是否开始传输? (Y/N): ");
                var key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key != ConsoleKey.Y)
                {
                    Console.WriteLine("操作已取消");
                    return;
                }

                Console.WriteLine();

                // 处理数据集目录
                await ProcessProject(
                    baseDirectory,
                    projectName,
                    targetIp,
                    port,
                    networkMode,
                    playbackSpeed,
                    verboseMode,
                    bufferSize
                );

                Console.WriteLine("传输完成！");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"程序执行出错: {ex.Message}");
                if (verboseMode && ex.InnerException != null)
                {
                    Console.WriteLine($"详细错误: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// 显示使用说明
        /// </summary>
        private static void ShowUsage()
        {
            Console.WriteLine("用法: PcapFileUdpTransmitter <基础目录> <数据集名称> [选项]");
            Console.WriteLine();
            Console.WriteLine("参数:");
            Console.WriteLine("  基础目录        包含数据集的基础路径");
            Console.WriteLine("  数据集名称      数据集目录的名称（基础目录下的子目录）");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  -a, --address <IP地址>    指定目标IP地址");
            Console.WriteLine("                            广播: 255.255.255.255 (默认)");
            Console.WriteLine("                            组播: 224.0.0.0-239.255.255.255");
            Console.WriteLine("                            单播: 其他有效IP地址");
            Console.WriteLine("  -p, --port <端口>         指定目标端口 (默认: 12345)");
            Console.WriteLine("  -m, --mode <模式>         指定网络模式:");
            Console.WriteLine("                            Broadcast  - 广播模式");
            Console.WriteLine("                            Multicast  - 组播模式");
            Console.WriteLine("                            Unicast    - 单播模式");
            Console.WriteLine("  -s, --speed <倍速>        指定播放速度 (默认: 1)");
            Console.WriteLine("  -b, --buffer <大小>       指定缓冲区大小 (默认: 1000)");
            Console.WriteLine("  --quiet                   关闭详细输出模式");
            Console.WriteLine();
            Console.WriteLine("目录结构要求:");
            Console.WriteLine("  基础目录/");
            Console.WriteLine("    └── 数据集名称/");
            Console.WriteLine("        ├── file1.pcap");
            Console.WriteLine("        ├── file2.pcap");
            Console.WriteLine("        └── ...");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  # 广播模式");
            Console.WriteLine(
                "  PcapFileUdpTransmitter D:\\Data MyProject -a 255.255.255.255 -p 12345"
            );
            Console.WriteLine();
            Console.WriteLine("  # 组播模式");
            Console.WriteLine(
                "  PcapFileUdpTransmitter /data MyProject -a 239.255.255.250 -p 12345"
            );
            Console.WriteLine();
            Console.WriteLine("  # 单播模式");
            Console.WriteLine(
                "  PcapFileUdpTransmitter ./data MyProject -a 192.168.1.100 -p 12345"
            );
        }

        /// <summary>
        /// 根据IP地址自动判断网络模式
        /// </summary>
        private static NetworkMode DetermineNetworkMode(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();

                // 检查传输地址
                if (bytes[3] == 255)
                {
                    return NetworkMode.Broadcast;
                }

                // 检查组播地址 (224.0.0.0 - 239.255.255.255)
                if (bytes[0] >= 224 && bytes[0] <= 239)
                {
                    return NetworkMode.Multicast;
                }
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var bytes = address.GetAddressBytes();

                // 检查IPv6组播地址 (ff00::/8)
                if (bytes[0] == 0xFF)
                {
                    return NetworkMode.Multicast;
                }
            }

            return NetworkMode.Unicast;
        }

        /// <summary>
        /// 获取网络模式描述
        /// </summary>
        private static string GetNetworkModeDescription(NetworkMode mode)
        {
            return mode switch
            {
                NetworkMode.Broadcast => "传输模式",
                NetworkMode.Multicast => "组播模式",
                NetworkMode.Unicast => "单播模式",
                _ => "未知模式",
            };
        }

        /// <summary>
        /// 处理数据集
        /// </summary>
        private static async Task ProcessProject(
            string baseDirectory,
            string projectName,
            IPAddress targetIp,
            int port,
            NetworkMode networkMode,
            int playbackSpeed,
            bool verboseMode,
            int bufferSize
        )
        {
            try
            {
                using var reader = new PcapReader();
                if (!reader.Open(baseDirectory, projectName))
                {
                    Console.WriteLine(
                        $"无法打开数据集: {Path.Combine(baseDirectory, projectName)}"
                    );
                    return;
                }

                Console.WriteLine($"成功打开数据集: {reader.ProjectName}");
                Console.WriteLine($"数据集目录: {reader.InputDirectory}");
                Console.WriteLine($"总文件数: {reader.GetProjectFiles().Count()}");
                Console.WriteLine($"总数据包数: {reader.TotalPacketCount}");
                Console.WriteLine();

                await TransmitData(
                    reader,
                    targetIp,
                    port,
                    networkMode,
                    playbackSpeed,
                    verboseMode,
                    bufferSize
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理数据集时出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 传输数据
        /// </summary>
        private static async Task TransmitData(
            IPcapReader reader,
            IPAddress targetIp,
            int port,
            NetworkMode networkMode,
            int playbackSpeed,
            bool verboseMode,
            int bufferSize
        )
        {
            try
            {
                var endPoint = new IPEndPoint(targetIp, port);
                using var transmitter = new UdpTransmitter(endPoint, networkMode);

                Console.WriteLine($"传输模式: {GetNetworkModeDescription(networkMode)}");
                Console.WriteLine($"目标: {endPoint}");
                Console.WriteLine($"播放速度: {playbackSpeed}x");
                Console.WriteLine();

                using var coordinator = new UdpCoordinator(
                    transmitter,
                    playbackSpeed,
                    bufferSize,
                    verboseMode
                );

                coordinator.Start(reader);

                // 显示进度
                var cancellationTokenSource = new CancellationTokenSource();
                var progressTask = ShowProgress(coordinator, cancellationTokenSource.Token);

                // 等待传输完成
                while (coordinator.IsRunning)
                {
                    await Task.Delay(500);
                }

                // 停止进度显示
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

                // 显示最终统计信息
                ShowFinalStatistics(coordinator.Statistics);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"传输过程中出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 显示进度
        /// </summary>
        private static async Task ShowProgress(
            UdpCoordinator coordinator,
            CancellationToken cancellationToken
        )
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var stats = coordinator.Statistics;
                    Console.Write(
                        $"\r已传输: {stats.ProcessedPackets} 个数据包, "
                            + $"{stats.ProcessedBytes / 1024.0 / 1024.0:F2} MB, "
                            + $"速率: {stats.PacketsPerSecond:F2} 包/秒, "
                            + $"队列: {stats.QueueSize}, "
                            + $"校验错误: {stats.ChecksumErrors}"
                    );

                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        /// <summary>
        /// 显示最终统计信息
        /// </summary>
        private static void ShowFinalStatistics(Statistics stats)
        {
            Console.WriteLine();
            Console.WriteLine("=== 传输统计信息 ===");
            Console.WriteLine($"总数据包数: {stats.ProcessedPackets}");
            Console.WriteLine($"总数据量: {stats.ProcessedBytes / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"传输时间: {stats.ElapsedTime.TotalSeconds:F2} 秒");
            Console.WriteLine($"平均速率: {stats.PacketsPerSecond:F2} 包/秒");
            Console.WriteLine($"校验错误: {stats.ChecksumErrors}");

            if (stats.ProcessedPackets > 0)
            {
                Console.WriteLine($"最小包大小: {stats.MinPacketSize} 字节");
                Console.WriteLine($"最大包大小: {stats.MaxPacketSize} 字节");
                Console.WriteLine(
                    $"平均包大小: {stats.ProcessedBytes / stats.ProcessedPackets:F2} 字节"
                );
            }
        }
    }
}
