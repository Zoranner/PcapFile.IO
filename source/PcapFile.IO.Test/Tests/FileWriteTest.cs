using System.Text;
using KimoTech.PcapFile.IO.Structures;
using NUnit.Framework;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    /// <summary>
    /// 文件写入测试
    /// </summary>
    [TestFixture]
    public class FileWriteTest : TestBase
    {
        private string _FilePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _FilePath = Path.Combine(TestDirectory, "test_data.pcap");
        }

        protected override string TestName => "文件写入测试";

        [Test]
        public async Task WritePackets_ShouldSucceed()
        {
            // 先创建一个测试文件
            using (var writer = new PcapWriter())
            {
                Assert.That(writer.Create(_FilePath), Is.True, "创建文件失败");
                var testPacket = new DataPacket(DateTime.Now, Encoding.UTF8.GetBytes("测试数据"));
                Assert.That(writer.WritePacket(testPacket), Is.True, "写入数据包失败");
                writer.Flush();
            }

            // 验证PATA文件已创建
            var pcapFileName = Path.GetFileNameWithoutExtension(_FilePath);
            var directory = Path.GetDirectoryName(_FilePath) ?? Directory.GetCurrentDirectory();
            var pataDirectory = Path.Combine(directory, pcapFileName);
            var initialPataFiles = Directory.GetFiles(pataDirectory, "data_*.pata");
            Assert.That(initialPataFiles, Is.Not.Empty, "未找到PATA文件");

            // 重新创建文件，应该会删除旧的PATA文件
            using var newWriter = new PcapWriter();
            Assert.That(newWriter.Create(_FilePath), Is.True, "重新创建文件失败");

            // 写入测试数据包（同步）
            for (uint i = 1; i <= 10; i++)
            {
                var payload = Encoding.UTF8.GetBytes($"测试数据包 {i}");
                var packet = new DataPacket(DateTime.Now.AddSeconds(i), payload);
                Assert.That(newWriter.WritePacket(packet), Is.True, $"写入数据包 {i} 失败");
                newWriter.Flush();
            }

            // 异步写入测试
            var asyncTasks = new List<Task<bool>>();
            for (uint i = 11; i <= 20; i++)
            {
                var packet = new DataPacket(
                    DateTime.Now.AddSeconds(i),
                    Encoding.UTF8.GetBytes($"异步测试数据包 {i}")
                );
                asyncTasks.Add(newWriter.WritePacketAsync(packet));
            }

            var results = await Task.WhenAll(asyncTasks);
            Assert.That(results, Is.All.True, "异步写入数据包失败");
            newWriter.Flush();

            // 验证最终状态
            Assert.That(newWriter.FileSize, Is.GreaterThan(0), "文件大小为0");
            Assert.That(newWriter.PacketCount, Is.EqualTo(20), "数据包数量不正确");

            var finalPataFiles = Directory.GetFiles(pataDirectory, "data_*.pata");
            Assert.That(finalPataFiles, Is.Not.Empty, "未找到最终的PATA文件");
        }
    }
}
