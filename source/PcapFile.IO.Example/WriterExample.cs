namespace KimoTech.PcapFile.IO.Example
{
    /// <summary>
    /// PCAP文件写入示例
    /// </summary>
    public static class WriterExample
    {
        /// <summary>
        /// 写入测试数据包到PCAP文件
        /// </summary>
        /// <param name="outputDirectory">输出目录</param>
        /// <param name="projectName">数据集名称</param>
        /// <param name="packetCount">数据包数量</param>
        /// <param name="packetSize">每个数据包大小</param>
        /// <returns>写入的数据包信息列表</returns>
        public static List<PacketInfo> WriteTestData(
            string outputDirectory,
            string projectName,
            int packetCount = 100,
            int packetSize = 1024
        )
        {
            Console.WriteLine("=== PCAP文件写入示例 ===");

            var writtenPackets = new List<PacketInfo>();

            // 确保输出目录存在
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                Console.WriteLine($"创建输出目录: {outputDirectory}");
            }

            // 清理已存在的文件
            var projectDir = Path.Combine(outputDirectory, projectName);
            if (Directory.Exists(projectDir))
            {
                var di = new DirectoryInfo(projectDir);
                foreach (var file in di.GetFiles("*.pcap"))
                {
                    file.Delete();
                }

                Console.WriteLine("清理已存在的PCAP文件");
            }

            try
            {
                using var writer = new PcapWriter();

                // 创建PCAP数据集
                writer.Create(outputDirectory, projectName);
                Console.WriteLine($"PCAP数据集已创建: {writer.OutputDirectory}");

                Console.WriteLine($"开始写入 {packetCount} 个数据包...");
                var startTime = DateTime.Now;

                for (var i = 0; i < packetCount; i++)
                {
                    var packet = CreateTestPacket(i, packetSize);
                    writer.WritePacket(packet);

                    // 记录写入的数据包信息用于后续验证
                    writtenPackets.Add(
                        new PacketInfo
                        {
                            Index = i,
                            CaptureTime = packet.CaptureTime,
                            PacketLength = packet.PacketLength,
                            Checksum = packet.Checksum,
                            FirstBytes = packet.Data.Take(16).ToArray(), // 记录前16字节用于验证
                        }
                    );

                    if (i % 20 == 0 || i == packetCount - 1)
                    {
                        Console.WriteLine(
                            $"已写入: {i + 1}/{packetCount}, 时间: {packet.CaptureTime:HH:mm:ss.fff}, 大小: {packet.PacketLength} 字节"
                        );
                    }
                }

                // 确保所有数据都写入到磁盘
                writer.Flush();
                writer.Close();

                var elapsed = DateTime.Now - startTime;
                Console.WriteLine($"写入完成，耗时: {elapsed.TotalMilliseconds:F0} 毫秒");
                Console.WriteLine(
                    $"平均每包耗时: {elapsed.TotalMilliseconds / packetCount:F2} 毫秒"
                );

                return writtenPackets;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入过程中发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建测试数据包
        /// </summary>
        /// <param name="sequence">序列号</param>
        /// <param name="size">数据包大小</param>
        /// <returns>测试数据包</returns>
        private static DataPacket CreateTestPacket(int sequence, int size)
        {
            var data = new byte[size];

            // 写入识别信息到数据包头部
            BitConverter.GetBytes(sequence).CopyTo(data, 0); // 序列号
            BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).CopyTo(data, 4); // 时间戳
            BitConverter.GetBytes(size).CopyTo(data, 8); // 数据包大小

            // 填充测试数据模式
            for (var i = 12; i < size; i++)
            {
                data[i] = (byte)((sequence + i) % 256);
            }

            // 创建带有真实捕获时间的数据包
            var captureTime = DateTime.Now.AddMilliseconds(sequence * 10); // 每个包间隔10毫秒
            // 使用时间戳构造函数创建数据包
            return new DataPacket(captureTime, data);
        }
    }

    /// <summary>
    /// 数据包信息类，用于验证
    /// </summary>
    public class PacketInfo
    {
        public int Index { get; set; }
        public DateTime CaptureTime { get; set; }
        public uint PacketLength { get; set; }
        public uint Checksum { get; set; }
        public byte[] FirstBytes { get; set; }
    }
}
