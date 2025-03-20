using System.Text;
using KimoTech.PcapFile.IO.Structures;
using NUnit.Framework;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    [TestFixture]
    public class ConcurrentTest : TestBase
    {
        private string _FilePath;

        protected override string TestName => "并发测试";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _FilePath = Path.Combine(TestDirectory, "concurrent_test.pcap");
        }

        [Test]
        public async Task ConcurrentWrite_ShouldThrowException()
        {
            using var writer = new PcapWriter();
            Assert.That(writer.Create(_FilePath), Is.True, "创建文件失败");

            // 创建多个并发写入任务
            var tasks = new List<Task>();
            for (var i = 0; i < 10; i++)
            {
                var taskId = i;
                tasks.Add(
                    Task.Run(async () =>
                    {
                        var data = Encoding.UTF8.GetBytes($"并发测试数据 {taskId}");
                        var packet = new DataPacket(DateTime.Now, data);

                        try
                        {
                            await writer.WritePacketAsync(packet);
                            Assert.Fail("应该抛出异常，因为多个线程同时访问同一个文件");
                        }
                        catch (IOException)
                        {
                            // 预期的异常
                            Assert.Pass();
                        }
                    })
                );
            }

            await Task.WhenAll(tasks);
        }
    }
}
