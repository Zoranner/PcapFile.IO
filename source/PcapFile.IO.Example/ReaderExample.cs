namespace KimoTech.PcapFile.IO.Example
{
    /// <summary>
    /// PCAP文件读取示例
    /// </summary>
    public static class ReaderExample
    {
        /// <summary>
        /// 读取PCAP工程目录中的所有数据包
        /// </summary>
        /// <param name="inputDirectory">输入目录</param>
        /// <param name="projectName">工程名称</param>
        /// <returns>读取的数据包信息列表</returns>
        public static List<PacketInfo> ReadTestData(string inputDirectory, string projectName)
        {
            Console.WriteLine("=== PCAP文件读取示例 ===");

            var readPackets = new List<PacketInfo>();

            try
            {
                using var reader = new PcapReader();

                // 打开PCAP工程目录
                if (!reader.Open(inputDirectory, projectName))
                {
                    Console.WriteLine(
                        $"无法打开工程目录: {Path.Combine(inputDirectory, projectName)}"
                    );
                    return readPackets;
                }

                Console.WriteLine($"成功打开工程: {reader.ProjectName}");
                Console.WriteLine($"工程目录: {reader.InputDirectory}");
                Console.WriteLine($"发现的文件数量: {reader.GetProjectFiles().Count()}");
                Console.WriteLine($"总数据包数量: {reader.TotalPacketCount}");

                var startTime = DateTime.Now;
                var packetIndex = 0;

                // 读取所有数据包
                foreach (var packet in reader.ReadAllPackets())
                {
                    // 记录读取的数据包信息
                    readPackets.Add(
                        new PacketInfo
                        {
                            Index = packetIndex,
                            CaptureTime = packet.CaptureTime,
                            PacketLength = packet.PacketLength,
                            Checksum = packet.Checksum,
                            FirstBytes = packet.Data.Take(16).ToArray(), // 记录前16字节用于验证
                        }
                    );

                    if (packetIndex % 20 == 0 || packetIndex == reader.TotalPacketCount - 1)
                    {
                        Console.WriteLine(
                            $"已读取: {packetIndex + 1}/{reader.TotalPacketCount}, 时间: {packet.CaptureTime:HH:mm:ss.fff}, 大小: {packet.PacketLength} 字节"
                        );
                    }

                    packetIndex++;
                }

                var elapsed = DateTime.Now - startTime;
                Console.WriteLine($"读取完成，总共读取了 {readPackets.Count} 个数据包");
                Console.WriteLine($"读取耗时: {elapsed.TotalMilliseconds:F0} 毫秒");
                Console.WriteLine(
                    $"平均每包耗时: {elapsed.TotalMilliseconds / readPackets.Count:F2} 毫秒"
                );

                return readPackets;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取过程中发生错误: {ex.Message}");
                throw;
            }
        }
    }
}
