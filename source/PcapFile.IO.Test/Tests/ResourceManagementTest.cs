using KimoTech.PcapFile.IO.Structures;
using NUnit.Framework;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    /// <summary>
    /// 资源管理测试
    /// </summary>
    [TestFixture]
    public class ResourceManagementTest : TestBase
    {
        protected override string TestName => "资源管理测试";

        [Test]
        public void PcapWriter_ShouldDisposeCorrectly()
        {
            // Arrange
            var filePath = Path.Combine(TestDirectory, "resource_test.pcap");
            var packet = new DataPacket(DateTime.Now, TestData.SamplePayload);

            // Act & Assert
            using (var writer = new PcapWriter())
            {
                writer.Create(filePath);
                writer.WritePacket(packet);
                Assert.That(File.Exists(filePath), Is.True);
            }

            // 验证文件是否被正确关闭
            Assert.That(
                () => File.Open(filePath, FileMode.Open, FileAccess.ReadWrite),
                Throws.Nothing
            );
        }

        [Test]
        public void PcapWriter_ShouldDisposeOnException()
        {
            // Arrange
            var filePath = Path.Combine(TestDirectory, "resource_test_exception.pcap");

            // Act & Assert
            Assert.That(
                () =>
                {
                    using var writer = new PcapWriter();
                    writer.Create(filePath);
                    throw new Exception("模拟异常");
                },
                Throws.Exception
            );

            // 验证文件是否被正确关闭
            Assert.That(
                () => File.Open(filePath, FileMode.Open, FileAccess.ReadWrite),
                Throws.Nothing
            );
        }

        [Test]
        public void PcapWriter_ShouldHandleMultipleDispose()
        {
            // Arrange
            var writer = new PcapWriter();

            // Act & Assert
            Assert.That(
                () =>
                {
                    writer.Dispose();
                    writer.Dispose();
                },
                Throws.Nothing
            );
        }
    }
}
