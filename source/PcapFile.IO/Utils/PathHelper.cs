using System;
using System.IO;
using System.Linq;
using KimoTech.PcapFile.IO.Configuration;

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
            return Path.Combine(
                pataDirectory,
                $"data_{timestamp.ToString(FileVersionConfig.DEFAULT_FILE_NAME_FORMAT)}.pata"
            );
        }

        /// <summary>
        /// 获取PATA目录下的所有PATA文件路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <returns>所有PATA文件路径的数组，按文件名排序</returns>
        /// <exception cref="ArgumentException">PCAP文件路径为空或无法获取目录路径</exception>
        public static string[] GetPataFiles(string pcapFilePath)
        {
            var pataDirectory = GetPataDirectoryPath(pcapFilePath);
            if (!Directory.Exists(pataDirectory))
            {
                return Array.Empty<string>();
            }

            var files = Directory.GetFiles(pataDirectory, "data_*.pata");
            Array.Sort(files);
            return files;
        }

        /// <summary>
        /// 获取最新的PATA文件路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <returns>最新的PATA文件路径，如果没有文件则返回null</returns>
        /// <exception cref="ArgumentException">PCAP文件路径为空或无法获取目录路径</exception>
        public static string GetLatestPataFile(string pcapFilePath)
        {
            var files = GetPataFiles(pcapFilePath);
            return files.Length > 0 ? files[^1] : null;
        }

        /// <summary>
        /// 清空PATA目录
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <returns>是否成功清空</returns>
        /// <exception cref="ArgumentException">PCAP文件路径为空或无法获取目录路径</exception>
        public static bool ClearPataDirectory(string pcapFilePath)
        {
            var pataDirectory = GetPataDirectoryPath(pcapFilePath);
            if (!Directory.Exists(pataDirectory))
            {
                return true;
            }

            try
            {
                Directory.Delete(pataDirectory, true);
                Directory.CreateDirectory(pataDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
