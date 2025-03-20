using System.Text;
using KimoTech.PcapFile.IO.Structures;
using NUnit.Framework;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    /// <summary>
    /// 数据包测试
    /// </summary>
    [TestFixture]
    public class DataPacketTest : TestBase
    {
        protected override string TestName => "数据包创建测试";

        [Test]
        public void DataPacket_Creation_ShouldHaveCorrectProperties()
        {
            // Arrange
            var timestamp = DateTime.Now;
            var payload = TestData.SamplePayload;

            // Act
            var packet = new DataPacket(timestamp, payload);

            // Assert
            Assert.That(
                packet.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Is.EqualTo(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")),
                "时间戳不匹配"
            );
            Assert.That(packet.PacketLength, Is.EqualTo(payload.Length));
            Assert.That(packet.Data, Is.EqualTo(payload));
            Assert.That(packet.TotalSize, Is.GreaterThan(payload.Length));
            Assert.That(packet.Checksum, Is.Not.Zero);
        }

        [Test]
        public void DataPacket_WithCustomTimestamp_ShouldHaveCorrectTimestamp()
        {
            // Arrange
            var customTime = DateTime.Now.AddDays(-1);
            var payload = TestData.SamplePayload;

            // Act
            var packet = new DataPacket(customTime, payload);

            // Assert
            Assert.That(
                packet.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Is.EqualTo(customTime.ToString("yyyy-MM-dd HH:mm:ss.fff")),
                "时间戳不匹配"
            );
            Assert.That(packet.PacketLength, Is.EqualTo(payload.Length));
        }

        [Test]
        public void DataPacket_WithEmptyPayload_ShouldHaveZeroLength()
        {
            // Arrange
            var timestamp = DateTime.Now;
            var emptyPayload = Array.Empty<byte>();

            // Act
            var packet = new DataPacket(timestamp, emptyPayload);

            // Assert
            Assert.That(packet.PacketLength, Is.Zero);
            Assert.That(packet.Data, Is.Empty);
        }
    }
}
