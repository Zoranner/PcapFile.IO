using System.Text;
using KimoTech.PcapFile.IO.Structures;
using NUnit.Framework;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    [TestFixture]
    public class LargeFileTest : TestBase
    {
        private string _FilePath;

        protected override string TestName => "大文件测试";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _FilePath = Path.Combine(TestDirectory, "large_test.pcap");
        }

        [Test]
        public async Task WriteLargeFile_ShouldSucceed()
        {
            using var writer = new PcapWriter();
            Assert.That(writer.Create(_FilePath), Is.True, "创建文件失败");

            // 写入大量数据包
            const int packetCount = 1000;
            var payload = new byte[1024]; // 1KB 数据包
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            for (var i = 0; i < packetCount; i++)
            {
                var packet = new DataPacket(DateTime.Now.AddMilliseconds(i), payload);
                Assert.That(await writer.WritePacketAsync(packet), Is.True, $"写入数据包 {i} 失败");

                if (i % 100 == 0)
                {
                    writer.Flush();
                }
            }

            writer.Flush();

            // 验证结果
            Assert.That(writer.PacketCount, Is.EqualTo(packetCount), "数据包数量不正确");
            Assert.That(writer.FileSize, Is.GreaterThan(packetCount * 1024), "文件大小异常");

            // 验证PATA文件
            var pcapFileName = Path.GetFileNameWithoutExtension(_FilePath);
            var directory = Path.GetDirectoryName(_FilePath) ?? Directory.GetCurrentDirectory();
            var pataDirectory = Path.Combine(directory, pcapFileName);
            var pataFiles = Directory.GetFiles(pataDirectory, "data_*.pata");
            Assert.That(pataFiles.Length, Is.GreaterThan(0), "未找到PATA文件");
        }
    }
}
