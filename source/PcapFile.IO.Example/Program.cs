using KimoTech.PcapFile.IO;
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
            Console.WriteLine("PcapFile.IO 示例程序 - 基础数据包写入测试");
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

                for (var i = 0; i < PACKET_COUNT; i++)
                {
                    var packet = CreateSamplePacket(i, DEFAULT_PACKET_SIZE);
                    writer.WritePacket(packet);

                    if (i % 5 == 0 || i == PACKET_COUNT - 1)
                    {
                        Console.WriteLine(
                            $"已写入数据包: {i + 1}/{PACKET_COUNT}, 时间戳: {packet.Timestamp:yyyy-MM-dd HH:mm:ss.fff}, 大小: {packet.PacketLength} 字节"
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

            // 创建包含实际时间戳的数据包
            var timestamp = DateTime.Now.AddMilliseconds(sequence * 100);
            return new DataPacket(timestamp, data);
        }
    }
}
