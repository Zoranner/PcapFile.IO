using KimoTech.PcapFile.IO.Example;

namespace PcapFile.IO.Example
{
    internal class Program
    {
        // 配置参数
        private const string OUTPUT_DIRECTORY = "data";
        private const string PROJECT_NAME = "test_project";
        private const int PACKET_COUNT = 2000;
        private const int PACKET_SIZE = 1024;

        private static void Main(string[] _)
        {
            Console.WriteLine("PcapFile.IO 示例程序 - 写入读取验证测试");
            Console.WriteLine("=========================================");
            Console.WriteLine();

            try
            {
                // 获取程序根目录
                var rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var outputDir = Path.Combine(rootDirectory, OUTPUT_DIRECTORY);

                Console.WriteLine($"程序根目录: {rootDirectory}");
                Console.WriteLine($"输出目录: {outputDir}");
                Console.WriteLine($"数据集名称: {PROJECT_NAME}");
                Console.WriteLine($"测试参数: {PACKET_COUNT} 个数据包，每个 {PACKET_SIZE} 字节");
                Console.WriteLine();

                // 步骤1: 写入测试数据
                Console.WriteLine("步骤 1/3: 写入测试数据");
                Console.WriteLine("========================");
                var writtenPackets = WriterExample.WriteTestData(
                    outputDir,
                    PROJECT_NAME,
                    PACKET_COUNT,
                    PACKET_SIZE
                );

                if (writtenPackets.Count == 0)
                {
                    Console.WriteLine("写入失败，程序退出");
                    return;
                }

                Console.WriteLine();

                // 步骤2: 读取测试数据
                Console.WriteLine("步骤 2/3: 读取测试数据");
                Console.WriteLine("========================");
                var readPackets = ReaderExample.ReadTestData(outputDir, PROJECT_NAME);

                if (readPackets.Count == 0)
                {
                    Console.WriteLine("读取失败，程序退出");
                    return;
                }

                Console.WriteLine();

                // 步骤3: 验证数据一致性
                Console.WriteLine("步骤 3/3: 验证数据一致性");
                Console.WriteLine("========================");
                var isValid = ValidateDataConsistency(writtenPackets, readPackets);

                Console.WriteLine();

                // 显示最终结果
                ShowFinalResults(writtenPackets, readPackets, isValid);

                Console.WriteLine();
                Console.WriteLine("测试完成，按任意键退出...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.ReadKey();
        }

        /// <summary>
        /// 验证写入和读取的数据一致性
        /// </summary>
        private static bool ValidateDataConsistency(
            List<PacketInfo> writtenPackets,
            List<PacketInfo> readPackets
        )
        {
            Console.WriteLine("正在验证数据一致性...");

            var isValid = true;
            var errors = new List<string>();

            // 验证数据包数量
            if (writtenPackets.Count != readPackets.Count)
            {
                errors.Add(
                    $"数据包数量不匹配：写入 {writtenPackets.Count}，读取 {readPackets.Count}"
                );
                isValid = false;
            }

            // 验证每个数据包
            var minCount = Math.Min(writtenPackets.Count, readPackets.Count);
            for (var i = 0; i < minCount; i++)
            {
                var written = writtenPackets[i];
                var read = readPackets[i];

                // 验证索引
                if (written.Index != read.Index)
                {
                    errors.Add(
                        $"数据包 {i}: 索引不匹配 (写入: {written.Index}, 读取: {read.Index})"
                    );
                    isValid = false;
                }

                // 验证大小
                if (written.PacketLength != read.PacketLength)
                {
                    errors.Add(
                        $"数据包 {i}: 长度不匹配 (写入: {written.PacketLength}, 读取: {read.PacketLength})"
                    );
                    isValid = false;
                }

                // 验证校验和
                if (written.Checksum != read.Checksum)
                {
                    errors.Add(
                        $"数据包 {i}: 校验和不匹配 (写入: 0x{written.Checksum:X8}, 读取: 0x{read.Checksum:X8})"
                    );
                    isValid = false;
                }

                // 验证前16字节数据
                if (!written.FirstBytes.SequenceEqual(read.FirstBytes))
                {
                    errors.Add($"数据包 {i}: 数据内容不匹配");
                    isValid = false;
                }

                // 验证时间戳（允许小幅差异）
                var timeDiff = Math.Abs((written.CaptureTime - read.CaptureTime).TotalMilliseconds);
                if (timeDiff > 100) // 允许100毫秒误差
                {
                    errors.Add($"数据包 {i}: 时间戳差异过大 ({timeDiff:F0} 毫秒)");
                    isValid = false;
                }
            }

            // 显示验证结果
            if (isValid)
            {
                Console.WriteLine("✓ 数据一致性验证通过！");
                Console.WriteLine($"  - 成功验证了 {minCount} 个数据包");
                Console.WriteLine("  - 所有数据包的长度、校验和、内容和时间戳都匹配");
            }
            else
            {
                Console.WriteLine("✗ 数据一致性验证失败！");
                foreach (var error in errors.Take(10)) // 只显示前10个错误
                {
                    Console.WriteLine($"  - {error}");
                }

                if (errors.Count > 10)
                {
                    Console.WriteLine($"  ... 还有 {errors.Count - 10} 个错误");
                }
            }

            return isValid;
        }

        /// <summary>
        /// 显示最终测试结果
        /// </summary>
        private static void ShowFinalResults(
            List<PacketInfo> writtenPackets,
            List<PacketInfo> readPackets,
            bool isValid
        )
        {
            Console.WriteLine("最终测试结果");
            Console.WriteLine("============");
            Console.WriteLine($"写入数据包数量: {writtenPackets.Count}");
            Console.WriteLine($"读取数据包数量: {readPackets.Count}");
            Console.WriteLine($"数据一致性: {(isValid ? "通过" : "失败")}");

            if (writtenPackets.Count > 0 && readPackets.Count > 0)
            {
                var firstWritten = writtenPackets.First();
                var lastWritten = writtenPackets.Last();
                var firstRead = readPackets.First();
                var lastRead = readPackets.Last();

                Console.WriteLine(
                    $"时间范围 (写入): {firstWritten.CaptureTime:HH:mm:ss.fff} - {lastWritten.CaptureTime:HH:mm:ss.fff}"
                );
                Console.WriteLine(
                    $"时间范围 (读取): {firstRead.CaptureTime:HH:mm:ss.fff} - {lastRead.CaptureTime:HH:mm:ss.fff}"
                );
            }

            Console.WriteLine($"测试状态: {(isValid ? "✓ 成功" : "✗ 失败")}");
        }
    }
}
