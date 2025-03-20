using System;
using System.IO;

namespace KimoTech.PcapFile.IO.Utils
{
    /// <summary>
    /// 路径处理工具类
    /// </summary>
    public static class PathHelper
    {
        /// <summary>
        /// 根据PCAP文件路径获取对应的PATA目录路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <returns>PATA目录路径</returns>
        /// <exception cref="ArgumentException">PCAP文件路径为空或无法获取目录路径</exception>
        public static string GetPataDirectoryPath(string pcapFilePath)
        {
            if (string.IsNullOrEmpty(pcapFilePath))
            {
                throw new ArgumentException("PCAP文件路径不能为空", nameof(pcapFilePath));
            }

            var directory = Path.GetDirectoryName(pcapFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("无法获取目录路径", nameof(pcapFilePath));
            }

            var pcapFileName = Path.GetFileNameWithoutExtension(pcapFilePath);
            return Path.Combine(directory, pcapFileName);
        }

        /// <summary>
        /// 根据PCAP文件路径和时间戳生成PATA文件路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>PATA文件路径</returns>
        /// <exception cref="ArgumentException">PCAP文件路径为空或无法获取目录路径</exception>
        public static string GetPataFilePath(string pcapFilePath, DateTime timestamp)
        {
            var pataDirectory = GetPataDirectoryPath(pcapFilePath);
            Directory.CreateDirectory(pataDirectory);
            return Path.Combine(pataDirectory, $"data_{timestamp:yyMMdd}_{timestamp:HHmmss}.pata");
        }
    }
}
