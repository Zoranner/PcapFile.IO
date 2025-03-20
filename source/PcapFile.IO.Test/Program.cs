using KimoTech.PcapFile.IO.Structures;
using System.Text;

namespace KimoTech.PcapFile.IO.Test
{
    class Program
    {
        static async Task Main(string[] _)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.WriteLine("数据回放测试程序");
                Console.WriteLine("==================");

                // 测试创建数据包
                TestCreateDataPacket();

                // 测试文件写入
                var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
                Console.WriteLine($"\n创建数据目录: {dataDir}");
                try
                {
                    Directory.CreateDirectory(dataDir);
                    Console.WriteLine("数据目录创建成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建数据目录失败: {ex.Message}");
                    return;
                }

                var filePath = Path.Combine(dataDir, "test_data.pcap");
                Console.WriteLine($"\n测试文件路径: {filePath}");
                TestFileWrite(filePath);

                // 执行压力测试
                await StressTest.RunAllTests();

                Console.WriteLine("\n所有测试完成！");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n发生错误: {ex.Message}");
                Console.WriteLine($"错误类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\n内部错误: {ex.InnerException.Message}");
                }
            }
        }

        static void TestCreateDataPacket()
        {
            Console.WriteLine("测试创建数据包:");

            // 创建一个简单的数据负载
            var payload = Encoding.UTF8.GetBytes("这是一个测试数据包");

            // 创建数据包
            var packet = new DataPacket(DateTime.Now, payload);

            // 显示数据包信息
            Console.WriteLine($"数据包创建时间: {packet.Timestamp}");
            Console.WriteLine($"负载大小: {packet.PacketLength} 字节");
            Console.WriteLine($"负载内容: {Encoding.UTF8.GetString(packet.Data)}");
            Console.WriteLine($"校验和: 0x{packet.Checksum:X8}");
            Console.WriteLine($"总大小: {packet.TotalSize} 字节");

            // 创建带指定时间戳的数据包
            var customTime = DateTime.Now.AddDays(-1); // 昨天的此时
            var packetWithTime = new DataPacket(customTime, payload);
            Console.WriteLine($"\n带指定时间戳的数据包:");
            Console.WriteLine($"数据包时间: {packetWithTime.Timestamp}");
            Console.WriteLine($"负载大小: {packetWithTime.PacketLength} 字节");
            Console.WriteLine($"校验和: 0x{packetWithTime.Checksum:X8}");
            Console.WriteLine($"总大小: {packetWithTime.TotalSize} 字节");
        }

        static void TestFileWrite(string filePath)
        {
            Console.WriteLine($"\n测试文件写入: {filePath}");

            try
            {
                var baseDirectory = Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory;
                var pataDataDirectory = Path.Combine(baseDirectory, "Packet_Data");

                // 先创建一个测试文件
                using (var writer = new PcapWriter())
                {
                    if (writer.Create(filePath))
                    {
                        var testPacket = new DataPacket(
                            DateTime.Now,
                            Encoding.UTF8.GetBytes("测试数据")
                        );
                        writer.WritePacket(testPacket);
                        writer.Flush();
                    }
                }

                // 验证PATA文件已创建
                var initialPataFiles = Directory.GetFiles(pataDataDirectory, "data_*.pata");
                Console.WriteLine($"\n初始PATA文件数量: {initialPataFiles.Length}");
                foreach (var pataFile in initialPataFiles)
                {
                    Console.WriteLine($"初始PATA文件: {Path.GetFileName(pataFile)}");
                }

                // 重新创建文件，应该会删除旧的PATA文件
                using var newWriter = new PcapWriter();
                if (!newWriter.Create(filePath))
                {
                    Console.WriteLine("创建文件失败");
                    return;
                }

                Console.WriteLine("\n重新创建文件成功");
                Console.WriteLine($"初始文件大小: {newWriter.FileSize} 字节");

                // 验证旧的PATA文件已被删除
                var remainingPataFiles = Array.Empty<string>();
                if (Directory.Exists(pataDataDirectory))
                {
                    remainingPataFiles = Directory.GetFiles(pataDataDirectory, "data_*.pata");
                }

                Console.WriteLine($"\n重新创建后的PATA文件数量: {remainingPataFiles.Length}");
                if (remainingPataFiles.Length > 0)
                {
                    Console.WriteLine("警告：旧的PATA文件未被完全删除！");
                    foreach (var pataFile in remainingPataFiles)
                    {
                        Console.WriteLine($"剩余PATA文件: {Path.GetFileName(pataFile)}");
                    }
                }

                // 写入100个随机大小的数据包
                Console.WriteLine("\n开始写入100个随机大小的数据包...");
                var random = new Random();
                var startTime = DateTime.Now;
                var lastProgressTime = DateTime.Now;
                var progressInterval = TimeSpan.FromSeconds(1);

                // 写入100个随机大小的数据包
                for (uint i = 1; i <= 100; i++)
                {
                    try
                    {
                        // 生成随机大小的数据（1KB到1MB之间）
                        var size = random.Next(1024, 1024 * 1024);
                        var payload = new byte[size];
                        random.NextBytes(payload);

                        // 创建数据包
                        var packet = new DataPacket(DateTime.Now.AddSeconds(i), payload);

                        // 写入数据包
                        var success = newWriter.WritePacket(packet);

                        if (success)
                        {
                            newWriter.Flush(); // 确保数据被写入磁盘

                            // 每秒显示一次进度
                            var now = DateTime.Now;
                            if (now - lastProgressTime >= progressInterval)
                            {
                                var progress = (double)i / 100 * 100;
                                var elapsed = (now - startTime).TotalSeconds;
                                Console.WriteLine($"进度: {progress:F1}% ({i}/100), 已写入: {i} 个数据包");
                                lastProgressTime = now;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"写入数据包 #{i} 失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"写入数据包 #{i} 时发生错误: {ex.Message}");
                    }
                }

                // 确保所有数据都被写入磁盘
                newWriter.Flush();

                var totalTime = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine("\n写入完成统计:");
                Console.WriteLine($"总数据包数量: {newWriter.PacketCount}");
                Console.WriteLine($"总耗时: {totalTime:F2} 秒");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件写入过程中发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
