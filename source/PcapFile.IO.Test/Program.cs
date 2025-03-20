using System.Text;
using KimoTech.PcapFile.IO;

namespace KimoTech.PcapFile.IO.Test
{
    class Program
    {
        static void Main(string[] _)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.WriteLine("数据回放测试程序");
                Console.WriteLine("==================");

                // 测试创建数据包
                TestCreateDataPacket();

                Console.WriteLine("\n按任意键继续测试文件操作...");
                Console.ReadKey(true);

                // 测试文件写入
                var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
                Directory.CreateDirectory(dataDir);
                var filePath = Path.Combine(dataDir, "test_data.pcap");
                Console.WriteLine($"测试文件路径: {filePath}");
                TestFileWrite(filePath);

                Console.WriteLine("\n测试完成，按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
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
                using var writer = new PcapWriter();
                // 创建文件
                if (!writer.Create(filePath))
                {
                    Console.WriteLine("创建文件失败");
                    return;
                }

                Console.WriteLine("创建文件成功");
                Console.WriteLine($"初始文件大小: {writer.FileSize} 字节");

                // 写入随机大小的数据包
                Console.WriteLine("\n开始写入1000个随机大小的数据包...");
                var random = new Random();
                var totalBytes = 0L;
                var startTime = DateTime.Now;
                var lastProgressTime = DateTime.Now;
                var progressInterval = TimeSpan.FromSeconds(1);
                var lastFileSize = writer.FileSize;

                // 写入1000个随机大小的数据包
                for (uint i = 1; i <= 1000; i++)
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
                        var success = writer.WritePacket(packet);

                        if (success)
                        {
                            totalBytes += packet.PacketLength;
                            writer.Flush(); // 确保数据被写入磁盘

                            // 每秒显示一次进度
                            var now = DateTime.Now;
                            if (now - lastProgressTime >= progressInterval)
                            {
                                var currentFileSize = writer.FileSize;
                                var progress = (double)i / 1000 * 100;
                                var elapsed = (now - startTime).TotalSeconds;
                                var speed = totalBytes / elapsed / 1024 / 1024; // MB/s
                                var fileSizeMB = currentFileSize / 1024.0 / 1024.0;
                                var fileSizeChange =
                                    (currentFileSize - lastFileSize) / 1024.0 / 1024.0;
                                Console.WriteLine(
                                    $"进度: {progress:F1}% ({i}/1000), 已写入: {totalBytes / 1024 / 1024:F1} MB, 文件大小: {fileSizeMB:F1} MB (+{fileSizeChange:F1} MB), 速度: {speed:F1} MB/s"
                                );
                                lastProgressTime = now;
                                lastFileSize = currentFileSize;
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
                writer.Flush();

                var totalTime = (DateTime.Now - startTime).TotalSeconds;
                var finalFileSize = writer.FileSize;
                Console.WriteLine("\n写入完成统计:");
                Console.WriteLine($"总数据包数量: {writer.PacketCount}");
                Console.WriteLine($"总数据大小: {totalBytes / 1024 / 1024:F2} MB");
                Console.WriteLine($"最终文件大小: {finalFileSize / 1024 / 1024:F2} MB");
                Console.WriteLine(
                    $"平均每个数据包大小: {finalFileSize / writer.PacketCount / 1024:F2} KB"
                );
                Console.WriteLine($"总耗时: {totalTime:F2} 秒");
                Console.WriteLine($"平均写入速度: {totalBytes / totalTime / 1024 / 1024:F2} MB/s");

                // 验证文件大小
                var pcapFileInfo = new FileInfo(filePath);
                var directory = Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory;
                var pataDirectory = Path.Combine(directory, "Packet_Data");
                var pataFilePath = Path.Combine(pataDirectory, "data_001.pata");
                var pataFileInfo = new FileInfo(pataFilePath);
                var totalFileSize = pcapFileInfo.Length + pataFileInfo.Length;
                Console.WriteLine($"\n实际文件大小: {totalFileSize / 1024 / 1024:F2} MB");
                if (totalFileSize != finalFileSize)
                {
                    Console.WriteLine("警告：文件大小不匹配！");
                    Console.WriteLine($"PCAP文件大小: {pcapFileInfo.Length / 1024 / 1024:F2} MB");
                    Console.WriteLine($"PATA文件大小: {pataFileInfo.Length / 1024 / 1024:F2} MB");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件写入过程中发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
