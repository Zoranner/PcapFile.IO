using System.Text;
using KimoTech.PcapFile.IO.Structures;
using NUnit.Framework;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    [TestFixture]
    public class ErrorPathTest : TestBase
    {
        private string _FilePath;

        protected override string TestName => "错误路径测试";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _FilePath = Path.Combine(TestDirectory, "error_test.pcap");
        }

        [Test]
        public void WritePacket_WithoutCreate_ShouldFail()
        {
            using var writer = new PcapWriter();
            var packet = new DataPacket(DateTime.Now, Encoding.UTF8.GetBytes("测试数据"));

            var ex = Assert.Throws<InvalidOperationException>(() => writer.WritePacket(packet));
            Assert.That(ex.Message, Is.EqualTo("文件未打开"));
        }

        [Test]
        public void WritePacket_WithNullData_ShouldFail()
        {
            using var writer = new PcapWriter();
            writer.Create(_FilePath);

            var ex = Assert.Throws<ArgumentNullException>(() => new DataPacket(DateTime.Now, null));
            Assert.That(ex.ParamName, Is.EqualTo("data"));
        }

        [Test]
        public Task WritePacketAsync_WithoutCreate_ShouldFail()
        {
            using var writer = new PcapWriter();
            var packet = new DataPacket(DateTime.Now, Encoding.UTF8.GetBytes("测试数据"));

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                () => writer.WritePacketAsync(packet)
            );
            Assert.That(ex.Message, Is.EqualTo("文件未打开"));
            return Task.CompletedTask;
        }

        [Test]
        public void Create_WithInvalidPath_ShouldFail()
        {
            using var writer = new PcapWriter();
            var invalidPath = Path.Combine(TestDirectory, "invalid/path/test.pcap");

            var ex = Assert.Throws<ArgumentException>(() => writer.Create(string.Empty));
            Assert.That(ex.Message, Contains.Substring("文件路径不能为空"));
        }
    }
}
