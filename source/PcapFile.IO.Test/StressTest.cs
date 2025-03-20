using KimoTech.PcapFile.IO.Structures;
using System.Collections.Concurrent;
using System.Text;

namespace KimoTech.PcapFile.IO.Test
{
    public class StressTest
    {
        private const string TEST_DIR = "stress_test";
        private static readonly Random _Random = new Random();

        public static async Task RunAllTests()
        {
            try
            {
                Console.WriteLine("\n开始执行压力测试和极端测试...");
                Console.WriteLine("=====================================");

                var baseDir = Path.Combine(AppContext.BaseDirectory, TEST_DIR);
                Directory.CreateDirectory(baseDir);

                // 1. 大文件测试
                await TestLargeFile(Path.Combine(baseDir, "large_file.pcap"));

                // 2. 并发写入测试
                await TestConcurrentWrites(Path.Combine(baseDir, "concurrent_test.pcap"));

                // 3. 异常路径测试
                TestErrorPaths(Path.Combine(baseDir, "error_test.pcap"));

                // 4. 资源释放测试
                TestResourceManagement(Path.Combine(baseDir, "resource_test.pcap"));

                Console.WriteLine("\n所有压力测试完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n压力测试过程中发生错误: {ex.Message}");
                Console.WriteLine($"错误类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 大文件测试：写入大量大尺寸数据包
        /// </summary>
        private static async Task TestLargeFile(string filePath)
        {
            Console.WriteLine("\n1. 执行大文件测试...");
            const int packetCount = 1000;
            const int maxPacketSize = 10 * 1024 * 1024; // 10MB

            using var writer = new PcapWriter();
            writer.Create(filePath);

            var totalSize = 0L;
            var startTime = DateTime.Now;

            try
            {
                for (var i = 0; i < packetCount; i++)
                {
                    var size = _Random.Next(1024 * 1024, maxPacketSize);
                    var data = new byte[size];
                    _Random.NextBytes(data);

                    var packet = new DataPacket(DateTime.Now, data);
                    await writer.WritePacketAsync(packet);
                    totalSize += packet.TotalSize;

                    if (i % 100 == 0)
                    {
                        var progress = (i + 1.0) / packetCount * 100;
                        Console.WriteLine(
                            $"进度: {progress:F1}%, 已写入: {totalSize / 1024.0 / 1024.0:F1}MB"
                        );
                    }
                }

                var duration = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine(
                    $"大文件测试完成: 总大小 {totalSize / 1024.0 / 1024.0:F1}MB, 耗时 {duration:F1}秒"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"大文件测试失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 并发写入测试：多线程同时写入数据包
        /// </summary>
        private static async Task TestConcurrentWrites(string filePath)
        {
            Console.WriteLine("\n2. 执行并发写入测试...");
            const int threadCount = 10;
            const int packetsPerThread = 100;

            using var writer = new PcapWriter();
            writer.Create(filePath);

            var tasks = new List<Task>();
            var errors = new ConcurrentBag<Exception>();
            var startTime = DateTime.Now;

            try
            {
                for (var i = 0; i < threadCount; i++)
                {
                    var threadId = i;
                    tasks.Add(
                        Task.Run(async () =>
                        {
                            try
                            {
                                for (var j = 0; j < packetsPerThread; j++)
                                {
                                    var data = Encoding.UTF8.GetBytes(
                                        $"Thread {threadId} Packet {j}"
                                    );
                                    var packet = new DataPacket(DateTime.Now, data);
                                    await writer.WritePacketAsync(packet);
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add(ex);
                            }
                        })
                    );
                }

                await Task.WhenAll(tasks);

                if (!errors.IsEmpty)
                {
                    throw new AggregateException("并发写入测试失败", errors);
                }

                var duration = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine(
                    $"并发写入测试完成: {threadCount}个线程, 每线程{packetsPerThread}个数据包, 耗时{duration:F1}秒"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"并发写入测试失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 异常路径测试：测试各种错误情况
        /// </summary>
        private static void TestErrorPaths(string filePath)
        {
            Console.WriteLine("\n3. 执行异常路径测试...");

            try
            {
                // 测试1：空数据包
                using (var writer1 = new PcapWriter())
                {
                    writer1.Create(filePath);
                    Assert(() => writer1.WritePacket(null), "空数据包测试");
                }

                // 测试2：文件未打开就写入
                using (var writer2 = new PcapWriter())
                {
                    var packet = new DataPacket(DateTime.Now, [1, 2, 3]);
                    Assert(() => writer2.WritePacket(packet), "未打开文件测试");
                }

                // 测试3：重复创建文件
                using (var writer3 = new PcapWriter())
                {
                    writer3.Create(filePath);
                    Assert(() => writer3.Create(filePath), "重复创建文件测试");
                }

                // 测试4：打开不存在的文件
                using (var writer4 = new PcapWriter())
                {
                    Assert(() => writer4.Open("nonexistent.pcap"), "打开不存在文件测试");
                }

                // 测试5：使用已释放的对象
                var disposedWriter = new PcapWriter();
                disposedWriter.Dispose();
                Assert(() => disposedWriter.Create(filePath), "使用已释放对象测试");

                Console.WriteLine("异常路径测试完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异常路径测试失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 资源释放测试：测试资源管理
        /// </summary>
        private static void TestResourceManagement(string filePath)
        {
            Console.WriteLine("\n4. 执行资源释放测试...");

            try
            {
                // 测试1：正常释放
                using (var writer1 = new PcapWriter())
                {
                    writer1.Create(filePath);
                    var packet = new DataPacket(DateTime.Now, [1, 2, 3]);
                    writer1.WritePacket(packet);
                }

                // 测试2：异常情况下的释放
                try
                {
                    using var writer2 = new PcapWriter();
                    writer2.Create(filePath);
                    throw new Exception("模拟异常");
                }
                catch
                {
                    // 应该正常释放资源
                }

                // 测试3：重复释放
                var writer3 = new PcapWriter();
                writer3.Dispose();
                writer3.Dispose(); // 不应抛出异常

                Console.WriteLine("资源释放测试完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"资源释放测试失败: {ex.Message}");
                throw;
            }
        }

        private static void Assert(Action action, string testName)
        {
            try
            {
                action();
                Console.WriteLine($"错误：{testName}未能捕获预期异常");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{testName}通过，捕获到异常: {ex.GetType().Name}");
            }
        }
    }
}
