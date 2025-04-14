using KimoTech.PcapFile.IO;

namespace PcapFile.IO.Example
{
    internal class Program
    {
        // 输出目录路径
        private const string OUTPUT_DIRECTORY = "data";

        // 数据工程名称
        private const string PROJECT_NAME = "test_project";

        // 测试数据包数量
        private const int PACKET_COUNT = 1000;

        // 每个数据包的默认大小（字节）
        private const int DEFAULT_PACKET_SIZE = 102400;

        private static void Main(string[] _)
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
                Console.WriteLine($"输出目录: {outputDir}");
                Console.WriteLine($"数据工程名称: {PROJECT_NAME}");

                // 删除可能存在的旧文件
                var projectDir = Path.Combine(outputDir, PROJECT_NAME);
                if (Directory.Exists(projectDir))
                {
                    var di = new DirectoryInfo(projectDir);
                    foreach (var file in di.GetFiles())
                    {
                        file.Delete();
                    }

                    Console.WriteLine("删除已存在的文件");
                }

                // 创建 PcapWriter 实例
                Console.WriteLine("\n[PCAP写入测试]");
                Console.WriteLine("------------------");

                using var writer = new PcapWriter();

                // 创建 PCAP 目录
                Console.WriteLine("创建 PCAP 目录...");
                writer.Create(outputDir, PROJECT_NAME);
                Console.WriteLine($"PCAP工程已创建: {writer.OutputDirectory}");

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

                Console.WriteLine($"PCAP数据目录:");
                Console.WriteLine($"  - 路径: {projectDir}");

                var pcapFiles = Directory.GetFiles(
                    projectDir,
                    "*.pcap",
                    SearchOption.AllDirectories
                );
                Console.WriteLine($"  - 包含 {pcapFiles.Length} 个PCAP数据文件");

                long totalPcapSize = 0;
                foreach (var pcapFilePath in pcapFiles)
                {
                    var file = new FileInfo(pcapFilePath);
                    totalPcapSize += file.Length;
                    Console.WriteLine($"    * {file.Name} - {file.Length / 1024.0:F2} KB");
                }

                Console.WriteLine($"\n总统计信息:");
                Console.WriteLine($"  - 总文件数: {pcapFiles.Length} 个PCAP文件");
                Console.WriteLine($"  - 总大小: {totalPcapSize / 1024.0:F2} KB");
                Console.WriteLine(
                    $"  - 平均每个数据包大小: {totalPcapSize / PACKET_COUNT / 1024.0:F2} KB"
                );

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

            // 创建包含实际捕获时间的数据包
            var captureTime = DateTime.Now.AddMilliseconds(sequence * 100);
            // 使用新的构造函数直接传递DateTime对象
            return new DataPacket(captureTime, data);
        }
    }
}
