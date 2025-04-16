using System.Text;
using KimoTech.PcapFile.IO;

namespace PcapFileValidator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("PCAP文件验证工具");
            Console.WriteLine("================\n");

            var path = "";
            var showAllPackets = false;
            var showDataPreview = false;

            // 处理命令行参数
            if (args.Length == 0)
            {
                Console.WriteLine(
                    "用法: PcapFileValidator <PCAP文件路径或目录路径> [--all] [--preview]"
                );
                Console.WriteLine("  --all: 输出所有数据包的详细信息");
                Console.WriteLine("  --preview: 显示数据包内容预览");
                return;
            }
            else
            {
                path = args[0];
                showAllPackets = Array.Exists(args, arg => arg == "--all");
                showDataPreview = Array.Exists(args, arg => arg == "--preview");
            }

            if (Directory.Exists(path))
            {
                ValidateDirectory(path, showAllPackets, showDataPreview);
            }
            else if (File.Exists(path))
            {
                ValidateFile(path, showAllPackets, showDataPreview);
            }
            else
            {
                Console.WriteLine($"错误: 路径不存在 - {path}");
            }
        }

        static void ValidateDirectory(
            string directoryPath,
            bool showAllPackets,
            bool showDataPreview
        )
        {
            Console.WriteLine($"验证目录: {directoryPath}\n");

            var pcapFiles = Directory.GetFiles(
                directoryPath,
                "*.pcap",
                SearchOption.AllDirectories
            );

            if (pcapFiles.Length == 0)
            {
                Console.WriteLine("未找到PCAP文件");
                return;
            }

            Console.WriteLine($"找到 {pcapFiles.Length} 个PCAP文件\n");

            foreach (var pcapFile in pcapFiles)
            {
                Console.WriteLine($"验证文件: {Path.GetFileName(pcapFile)}");
                ValidateFile(pcapFile, showAllPackets, showDataPreview);
                Console.WriteLine("----------------------------------------\n");
            }

            Console.WriteLine($"目录验证完成，共处理 {pcapFiles.Length} 个文件");
        }

        static void ValidateFile(string filePath, bool showAllPackets, bool showDataPreview)
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fileStream);
                // 验证文件头
                var isHeaderValid = ValidateFileHeader(reader);

                if (!isHeaderValid)
                {
                    Console.WriteLine("文件头验证失败，不是有效的PCAP文件");
                    return;
                }

                // 验证数据包
                var packetStats = ValidatePackets(reader, showAllPackets, showDataPreview);

                // 输出统计信息
                PrintStatistics(filePath, packetStats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证过程中出错: {ex.Message}");
            }
        }

        static bool ValidateFileHeader(BinaryReader reader)
        {
            try
            {
                // 读取文件头字节
                var headerBytes = reader.ReadBytes(PcapFileHeader.HEADER_SIZE);

                // 使用库中的方法解析文件头
                var header = PcapFileHeader.FromBytes(headerBytes);

                Console.WriteLine($"文件头: {headerBytes.ToHexString(" ")}");
                Console.WriteLine(
                    $"字节序: {(header.MagicNumber == 0xA1B2C3D4 ? "小端" : "大端")}"
                );
                Console.WriteLine($"版本: {header.MajorVersion}.{header.MinorVersion}");

                if (header.MajorVersion != 2 || header.MinorVersion != 4)
                {
                    Console.WriteLine(
                        $"警告: 版本号 {header.MajorVersion}.{header.MinorVersion} 不是标准版本(2.4)"
                    );
                }

                Console.WriteLine(
                    $"时区偏移: {header.TimezoneOffset}, 精度设置: {header.TimestampAccuracy}"
                );

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证文件头时出错: {ex.Message}");
                return false;
            }
        }

        static PacketStatistics ValidatePackets(
            BinaryReader reader,
            bool showAllPackets,
            bool showDataPreview
        )
        {
            var stats = new PacketStatistics();

            var fileSize = reader.BaseStream.Length;
            var position = reader.BaseStream.Position;

            Console.WriteLine("\n开始验证数据包...");

            if (showAllPackets)
            {
                Console.WriteLine("\n数据包详细信息:");
                Console.WriteLine("序号\t时间戳\t\t\t\t\t数据大小\t校验和\t\t校验结果");
                Console.WriteLine("--------------------------------------------------------");
            }

            while (position < fileSize)
            {
                try
                {
                    // 读取数据包头
                    var headerBytes = reader.ReadBytes(DataPacketHeader.HEADER_SIZE);
                    var header = BinaryConverter.FromBytes<DataPacketHeader>(headerBytes);

                    // 读取实际数据
                    var packetData = reader.ReadBytes((int)header.PacketLength);

                    // 使用PcapFile.IO库中的DataPacket类
                    var dataPacket = new DataPacket(header, packetData);

                    // 为数据包分配序号
                    var packetNumber = stats.PacketCount + 1;

                    // 计算校验和并验证
                    var calculatedChecksum = ChecksumCalculator.CalculateCrc32(packetData);
                    var checksumValid = calculatedChecksum == header.Checksum;

                    // 更新统计信息
                    stats.PacketNumbers[dataPacket] = packetNumber;
                    stats.CalculatedChecksums[dataPacket] = calculatedChecksum;
                    stats.ChecksumResults[dataPacket] = checksumValid;

                    // 更新首个和最后一个包的时间戳
                    if (stats.PacketCount == 0)
                    {
                        stats.FirstPacketTime = dataPacket.CaptureTime;
                    }

                    stats.LastPacketTime = dataPacket.CaptureTime;

                    // 更新包大小统计
                    stats.TotalSize += header.PacketLength;
                    stats.PacketCount++;

                    if (stats.PacketCount == 1 || header.PacketLength < stats.MinPacketSize)
                    {
                        stats.MinPacketSize = header.PacketLength;
                    }

                    if (header.PacketLength > stats.MaxPacketSize)
                    {
                        stats.MaxPacketSize = header.PacketLength;
                    }

                    // 校验结果统计
                    if (checksumValid)
                    {
                        stats.ValidPackets++;
                    }
                    else
                    {
                        stats.InvalidPackets++;
                    }

                    // 保存数据包信息
                    stats.PacketList.Add(dataPacket);

                    // 输出详细信息
                    if (showAllPackets)
                    {
                        Console.WriteLine(
                            $"{packetNumber}\t{dataPacket.CaptureTime:yyyy-MM-dd HH:mm:ss.fffffff}\t{dataPacket.PacketLength}\t\t0x{dataPacket.Checksum:X8}\t{(checksumValid ? "成功" : "失败")}"
                        );

                        // 如果需要，显示数据预览
                        if (showDataPreview && dataPacket.Data.Length > 0)
                        {
                            Console.WriteLine("数据预览:");
                            PrintDataPreview(dataPacket.Data);
                            Console.WriteLine();
                        }
                    }

                    // 跳过到下一个包的位置
                    position = reader.BaseStream.Position;

                    // 每100个包输出一次进度
                    if (!showAllPackets && stats.PacketCount % 100 == 0)
                    {
                        Console.Write($"\r验证数据包中: {stats.PacketCount}");
                    }
                }
                catch (EndOfStreamException)
                {
                    Console.WriteLine("\n警告: 文件末尾不完整");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"\n解析数据包 #{stats.PacketCount + 1} 时出错: {ex.Message}"
                    );
                    break;
                }
            }

            if (!showAllPackets)
            {
                Console.WriteLine($"\r验证数据包完成: 共 {stats.PacketCount} 个数据包");
            }
            else
            {
                Console.WriteLine("--------------------------------------------------------");
            }

            // 计算平均数据包率
            if (stats.PacketCount > 1)
            {
                var duration = stats.LastPacketTime - stats.FirstPacketTime;
                if (duration.TotalSeconds > 0)
                {
                    stats.AveragePacketRate = stats.PacketCount / duration.TotalSeconds;
                }
            }

            return stats;
        }

        static void PrintStatistics(string filePath, PacketStatistics stats)
        {
            Console.WriteLine("\n数据文件统计信息:");
            Console.WriteLine($"文件名称: {Path.GetFileName(filePath)}");
            Console.WriteLine($"文件大小: {new FileInfo(filePath).Length / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"数据包总数: {stats.PacketCount}");

            if (stats.PacketCount > 0)
            {
                Console.WriteLine(
                    $"校验成功数: {stats.ValidPackets} ({stats.ValidPackets * 100.0 / stats.PacketCount:F2}%)"
                );
                Console.WriteLine(
                    $"校验失败数: {stats.InvalidPackets} ({stats.InvalidPackets * 100.0 / stats.PacketCount:F2}%)"
                );
                Console.WriteLine(
                    $"首个数据包时间: {stats.FirstPacketTime:yyyy-MM-dd HH:mm:ss.fffffff}"
                );
                Console.WriteLine(
                    $"最后数据包时间: {stats.LastPacketTime:yyyy-MM-dd HH:mm:ss.fffffff}"
                );
                Console.WriteLine(
                    $"时间跨度: {(stats.LastPacketTime - stats.FirstPacketTime).TotalSeconds:F3} 秒"
                );
                Console.WriteLine($"最小数据包大小: {stats.MinPacketSize} 字节");
                Console.WriteLine($"最大数据包大小: {stats.MaxPacketSize} 字节");
                Console.WriteLine(
                    $"平均数据包大小: {stats.TotalSize / (ulong)stats.PacketCount:F2} 字节"
                );
                Console.WriteLine($"总数据大小: {stats.TotalSize / 1024.0 / 1024.0:F2} MB");
                Console.WriteLine($"平均数据包率: {stats.AveragePacketRate:F2} 包/秒");

                // 如果有校验失败的包，输出列表
                if (stats.InvalidPackets > 0)
                {
                    Console.WriteLine("\n校验失败的数据包列表:");
                    var count = 0;
                    foreach (var packet in stats.PacketList)
                    {
                        if (!stats.ChecksumResults[packet])
                        {
                            var packetNumber = stats.PacketNumbers[packet];
                            var calculatedChecksum = stats.CalculatedChecksums[packet];

                            Console.WriteLine(
                                $"  - 数据包 #{packetNumber}: 时间={packet.CaptureTime:yyyy-MM-dd HH:mm:ss.fffffff}, 大小={packet.PacketLength}字节, 校验和=0x{packet.Checksum:X8}/0x{calculatedChecksum:X8}"
                            );
                            count++;
                            if (count >= 10)
                            {
                                Console.WriteLine(
                                    $"  ... 还有 {stats.InvalidPackets - 10} 个校验失败的包未显示"
                                );
                                break;
                            }
                        }
                    }
                }
            }

            Console.WriteLine(
                $"\n验证结果: {(stats.InvalidPackets == 0 ? "全部数据包校验成功" : "存在校验失败的数据包")}"
            );
        }

        static void PrintDataPreview(byte[] data, int maxBytes = 64)
        {
            var previewLength = Math.Min(data.Length, maxBytes);
            var hexBuilder = new StringBuilder();
            var asciiBuilder = new StringBuilder();

            for (var i = 0; i < previewLength; i++)
            {
                if (i % 16 == 0 && i > 0)
                {
                    Console.WriteLine(
                        $"  {hexBuilder.ToString().PadRight(48)} | {asciiBuilder.ToString()}"
                    );
                    hexBuilder.Clear();
                    asciiBuilder.Clear();
                }

                hexBuilder.Append(data[i].ToString("X2") + " ");

                // ASCII显示，非可打印字符显示为'.'
                if (data[i] >= 32 && data[i] <= 126)
                {
                    asciiBuilder.Append((char)data[i]);
                }
                else
                {
                    asciiBuilder.Append('.');
                }
            }

            // 输出最后一行
            if (hexBuilder.Length > 0)
            {
                Console.WriteLine(
                    $"  {hexBuilder.ToString().PadRight(48)} | {asciiBuilder.ToString()}"
                );
            }

            if (data.Length > maxBytes)
            {
                Console.WriteLine($"  ... 共 {data.Length} 字节");
            }
        }
    }

    class PacketStatistics
    {
        public long PacketCount { get; set; } = 0;
        public long ValidPackets { get; set; } = 0;
        public long InvalidPackets { get; set; } = 0;
        public DateTime FirstPacketTime { get; set; }
        public DateTime LastPacketTime { get; set; }
        public uint MinPacketSize { get; set; } = uint.MaxValue;
        public uint MaxPacketSize { get; set; } = 0;
        public ulong TotalSize { get; set; } = 0;
        public double AveragePacketRate { get; set; } = 0; // 平均数据包率（包/秒）

        // 使用 DataPacket 代替 PacketInfo
        public List<DataPacket> PacketList { get; set; } = new List<DataPacket>();

        // 为了保持兼容性，添加额外跟踪信息的字典
        public Dictionary<DataPacket, long> PacketNumbers { get; set; } =
            new Dictionary<DataPacket, long>();
        public Dictionary<DataPacket, bool> ChecksumResults { get; set; } =
            new Dictionary<DataPacket, bool>();
        public Dictionary<DataPacket, uint> CalculatedChecksums { get; set; } =
            new Dictionary<DataPacket, uint>();
    }
}
