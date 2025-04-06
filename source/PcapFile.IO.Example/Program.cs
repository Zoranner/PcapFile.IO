using KimoTech.PcapFile.IO;
using KimoTech.PcapFile.IO.Extensions;
using KimoTech.PcapFile.IO.Structures;

namespace PcapFile.IO.Example
{
    class Program
    {
        // 输出文件路径
        private const string OUTPUT_DIRECTORY = "data";
        private const string OUTPUT_FILE_NAME = "test_output.pcap";

        // 测试数据包数量
        private const int PACKET_COUNT = 1000;

        // 每个数据包的默认大小（字节）
        private const int DEFAULT_PACKET_SIZE = 102400;

        static void Main(string[] _)
        {
            Console.WriteLine("PcapFile.IO 示例程序 - 基础数据包写入和读取测试");
            Console.WriteLine("=====================================");

            try
            {
                // 获取程序根目录（而不是当前工作目录）
                var rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"程序根目录: {rootDirectory}");

                // 确保输出目录存在
                var outputDir = Path.Combine(rootDirectory, OUTPUT_DIRECTORY);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Console.WriteLine($"创建输出目录: {outputDir}");
                }

                // 构建完整的输出路径
                var filePath = Path.Combine(outputDir, OUTPUT_FILE_NAME);
                Console.WriteLine($"输出文件: {filePath}");

                // 删除可能存在的旧文件
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine("删除已存在的文件");
                }

                var pataDirectory = Path.Combine(
                    Path.GetDirectoryName(filePath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(filePath)
                );
                if (Directory.Exists(pataDirectory))
                {
                    Directory.Delete(pataDirectory, true);
                    Console.WriteLine($"删除已存在的PATA目录: {pataDirectory}");
                }

                // 创建 PcapWriter 实例
                Console.WriteLine("\n[PCAP写入测试]");
                Console.WriteLine("------------------");

                using var writer = new PcapWriter();

                // 创建 PCAP 文件
                Console.WriteLine("创建 PCAP 文件...");
                writer.Create(filePath);
                Console.WriteLine($"PCAP文件已创建: {filePath}");

                // 生成并写入模拟数据包
                Console.WriteLine($"\n开始写入 {PACKET_COUNT} 个数据包...");
                var startTime = DateTime.Now;
                var timestamps = new List<DateTime>();

                for (var i = 0; i < PACKET_COUNT; i++)
                {
                    var packet = CreateSamplePacket(i, DEFAULT_PACKET_SIZE);
                    timestamps.Add(packet.CaptureTime); // 保存捕获时间以便后续验证
                    writer.WritePacket(packet);

                    if (i % 5 == 0 || i == PACKET_COUNT - 1)
                    {
                        Console.WriteLine(
                            $"已写入数据包: {i + 1}/{PACKET_COUNT}, 捕获时间: {packet.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}, 大小: {packet.PacketLength} 字节"
                        );
                    }
                }

                // 关闭文件
                Console.WriteLine("\n所有数据包写入完成，关闭文件...");
                writer.Close();

                var elapsed = DateTime.Now - startTime;
                Console.WriteLine($"写入完成，耗时: {elapsed.TotalSeconds:F2} 秒");

                // 验证文件结构
                Console.WriteLine("\n[文件结构验证]");
                Console.WriteLine("------------------");

                var pcapFile = new FileInfo(filePath);
                Console.WriteLine($"PCAP索引文件:");
                Console.WriteLine($"  - 路径: {pcapFile.Name}");
                Console.WriteLine($"  - 大小: {pcapFile.Length / 1024.0:F2} KB");
                Console.WriteLine($"  - 创建时间: {pcapFile.CreationTime}");

                var pataDir = Path.Combine(
                    Path.GetDirectoryName(filePath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(filePath)
                );
                if (Directory.Exists(pataDir))
                {
                    Console.WriteLine($"\nPATA数据目录:");
                    Console.WriteLine($"  - 路径: {pataDir}");

                    // 递归查找所有.pata文件
                    var pataFiles = Directory.GetFiles(
                        pataDir,
                        "*.pata",
                        SearchOption.AllDirectories
                    );
                    Console.WriteLine($"  - 包含 {pataFiles.Length} 个PATA数据文件");

                    long totalPataSize = 0;
                    foreach (var pataFilePath in pataFiles)
                    {
                        var file = new FileInfo(pataFilePath);
                        totalPataSize += file.Length;
                        Console.WriteLine($"    * {file.Name} - {file.Length / 1024.0:F2} KB");
                    }

                    Console.WriteLine($"\n总统计信息:");
                    Console.WriteLine(
                        $"  - 总文件数: {pataFiles.Length + 1} (1个PCAP文件 + {pataFiles.Length}个PATA文件)"
                    );
                    Console.WriteLine(
                        $"  - 总大小: {(pcapFile.Length + totalPataSize) / 1024.0:F2} KB"
                    );
                    Console.WriteLine(
                        $"  - 平均每个数据包大小: {(pcapFile.Length + totalPataSize) / PACKET_COUNT / 1024.0:F2} KB"
                    );
                }
                else
                {
                    Console.WriteLine($"\n错误: PATA数据目录不存在: {pataDir}");
                }

                // 读取测试
                Console.WriteLine("\n[PCAP读取测试]");
                Console.WriteLine("------------------");
                using var reader = new PcapReader();

                // 打开PCAP文件
                Console.WriteLine($"打开PCAP文件: {filePath}");
                if (reader.Open(filePath))
                {
                    Console.WriteLine("PCAP文件打开成功");
                    Console.WriteLine($"数据包总数: {reader.PacketCount}");
                    Console.WriteLine(
                        $"时间范围: {reader.StartTime:yyyy-MM-dd HH:mm:ss.fff} - {reader.EndTime:yyyy-MM-dd HH:mm:ss.fff}"
                    );

                    // 顺序读取测试
                    Console.WriteLine("\n[顺序读取测试]");
                    Console.WriteLine("------------------");
                    startTime = DateTime.Now;
                    int sequentialReadCount = 0;
                    int matchCount = 0;

                    // 读取前10个包演示
                    Console.WriteLine("读取前10个数据包...");
                    for (int i = 0; i < 10; i++)
                    {
                        var packet = reader.ReadNextPacket();
                        if (packet == null)
                            break;

                        sequentialReadCount++;
                        // 验证时间是否匹配
                        if (packet.CaptureTime == timestamps[i])
                            matchCount++;

                        Console.WriteLine(
                            $"包 {i + 1}: 捕获时间={packet.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}, 大小={packet.PacketLength} 字节"
                        );
                    }

                    // 继续读取剩下的包但不显示详情
                    Console.WriteLine($"继续读取剩余数据包...");
                    reader.Reset(); // 重置到开始位置，重新读取所有包
                    while (reader.ReadNextPacket() != null)
                    {
                        sequentialReadCount++;
                    }

                    elapsed = DateTime.Now - startTime;
                    Console.WriteLine($"顺序读取完成，耗时: {elapsed.TotalSeconds:F2} 秒");
                    Console.WriteLine($"实际读取数据包: {sequentialReadCount} 个");

                    // 随机访问测试
                    Console.WriteLine("\n[随机访问测试]");
                    Console.WriteLine("------------------");

                    // 测试随机位置访问
                    var randomPositions = new int[] { 0, 10, 50, 100, 500, PACKET_COUNT - 1 };
                    Console.WriteLine("测试随机位置访问...");
                    foreach (var pos in randomPositions)
                    {
                        if (pos >= reader.PacketCount)
                            continue;

                        var packet = reader.ReadPacketAt(pos);
                        Console.WriteLine(
                            $"位置 {pos}: 捕获时间={packet?.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}, 大小={packet?.PacketLength} 字节"
                        );
                    }

                    // 测试随机时间访问
                    if (timestamps.Count > 0)
                    {
                        Console.WriteLine("\n测试随机时间访问...");
                        var randomTimes = new DateTime[]
                        {
                            timestamps[0], // 第一个包
                            timestamps[10], // 第11个包
                            timestamps[PACKET_COUNT / 2], // 中间的包
                            timestamps[PACKET_COUNT - 1], // 最后一个包
                            timestamps[0].AddMilliseconds(-10), // 稍早于第一个包
                            timestamps[PACKET_COUNT - 1]
                                .AddMilliseconds(
                                    10
                                ) // 稍晚于最后一个包
                            ,
                        };

                        foreach (var time in randomTimes)
                        {
                            var success = reader.SeekToTime(time);
                            var packet = success ? reader.ReadNextPacket() : null;
                            Console.WriteLine(
                                $"时间 {time:yyyy-MM-dd HH:mm:ss.fff}: {(success ? "定位成功" : "定位失败")}, 读取到的包: {(packet != null ? $"捕获时间={packet.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}" : "无")}"
                            );
                        }
                    }

                    // 批量读取测试
                    Console.WriteLine("\n[批量读取测试]");
                    Console.WriteLine("------------------");
                    reader.Reset();
                    startTime = DateTime.Now;
                    var batchSize = 100;
                    int totalRead = 0;

                    Console.WriteLine($"使用批量读取，每批 {batchSize} 个数据包...");
                    var batches = 0;
                    while (true)
                    {
                        var packets = reader.ReadPackets(batchSize);
                        if (packets.Count == 0)
                            break;

                        totalRead += packets.Count;
                        batches++;

                        if (batches <= 3 || totalRead == reader.PacketCount) // 只显示前3批和最后一批
                            Console.WriteLine(
                                $"批次 {batches}: 读取了 {packets.Count} 个数据包，总计: {totalRead}"
                            );
                    }

                    elapsed = DateTime.Now - startTime;
                    Console.WriteLine(
                        $"批量读取完成，总共读取 {totalRead} 个数据包，批次数: {batches}，耗时: {elapsed.TotalSeconds:F2} 秒"
                    );

                    // 异步读取测试
                    Console.WriteLine("\n[异步读取测试]");
                    Console.WriteLine("------------------");
                    reader.Reset();
                    startTime = DateTime.Now;

                    Console.WriteLine("异步读取所有数据包...");
                    var asyncReadTask = ReadAllPacketsAsync(reader);
                    asyncReadTask.Wait();
                    var asyncReadCount = asyncReadTask.Result;

                    elapsed = DateTime.Now - startTime;
                    Console.WriteLine(
                        $"异步读取完成，总共读取 {asyncReadCount} 个数据包，耗时: {elapsed.TotalSeconds:F2} 秒"
                    );

                    // 关闭文件
                    Console.WriteLine("\n关闭PCAP文件...");
                    reader.Close();
                }
                else
                {
                    Console.WriteLine("无法打开PCAP文件");
                }

                Console.WriteLine("\n测试完成，按任意键退出...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.ReadKey();
        }

        /// <summary>
        /// 异步读取所有数据包
        /// </summary>
        private static async Task<int> ReadAllPacketsAsync(PcapReader reader)
        {
            int count = 0;
            using var cts = new CancellationTokenSource();

            while (true)
            {
                var packets = await reader.ReadPacketsAsync(200, cts.Token);
                if (packets.Count == 0)
                    break;

                count += packets.Count;

                if (count % 1000 == 0 || count < 1000)
                    Console.WriteLine($"已异步读取 {count} 个数据包");
            }

            return count;
        }

        /// <summary>
        /// 创建模拟数据包
        /// </summary>
        /// <param name="sequence">序列号</param>
        /// <param name="size">数据包大小（字节）</param>
        /// <returns>模拟数据包</returns>
        private static DataPacket CreateSamplePacket(int sequence, int size)
        {
            // 创建数据包负载（模拟数据）
            var data = new byte[size];

            // 在数据的开头写入一些识别信息
            BitConverter.GetBytes(sequence).CopyTo(data, 0);
            BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).CopyTo(data, 4);

            // 填充剩余数据
            for (var i = 12; i < size; i++)
            {
                data[i] = (byte)(i % 256);
            }

            // 创建包含实际捕获时间的数据包
            var captureTime = DateTime.Now.AddMilliseconds(sequence * 100);
            // 将 DateTime 转换为时间戳
            var timestamp = captureTime.ToUnixTimeMilliseconds();
            return new DataPacket(timestamp, data);
        }
    }
}
