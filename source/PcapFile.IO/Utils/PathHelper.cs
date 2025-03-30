using System;
using System.IO;
using System.Linq;
using KimoTech.PcapFile.IO.Configuration;

namespace KimoTech.PcapFile.IO.Utils
{
    /// <summary>
    /// 路径处理工具类，提供文件路径的计算和转换功能
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// 获取PATA数据目录路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <returns>PATA数据目录路径</returns>
        public static string GetPataDirectoryPath(string pcapFilePath)
        {
            if (string.IsNullOrEmpty(pcapFilePath))
            {
                throw new ArgumentException("PCAP文件路径不能为空", nameof(pcapFilePath));
            }

            // 获取PCAP文件所在目录
            string directory = Path.GetDirectoryName(pcapFilePath);
            
            // 获取PCAP文件名（不含扩展名）作为数据目录名
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(pcapFilePath);

            // 返回PATA数据目录路径
            return Path.Combine(directory, fileNameWithoutExtension);
        }

        /// <summary>
        /// 根据时间戳创建PATA文件路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>PATA文件路径</returns>
        public static string GetPataFilePath(string pcapFilePath, DateTime timestamp)
        {
            if (string.IsNullOrEmpty(pcapFilePath))
            {
                throw new ArgumentException("PCAP文件路径不能为空", nameof(pcapFilePath));
            }

            // 获取PATA数据目录路径
            string pataDirectory = GetPataDirectoryPath(pcapFilePath);

            // 使用时间戳格式化文件名
            string fileName = $"data_{timestamp:yyMMdd_HHmmss_fff}.pata";

            // 返回完整的PATA文件路径
            return Path.Combine(pataDirectory, fileName);
        }

        /// <summary>
        /// 获取PATA文件的完整路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <param name="relativePath">PATA文件相对路径</param>
        /// <returns>PATA文件完整路径</returns>
        public static string GetFullPataFilePath(string pcapFilePath, string relativePath)
        {
            if (string.IsNullOrEmpty(pcapFilePath))
            {
                throw new ArgumentException("PCAP文件路径不能为空", nameof(pcapFilePath));
            }

            if (string.IsNullOrEmpty(relativePath))
            {
                throw new ArgumentException("相对路径不能为空", nameof(relativePath));
            }

            // 获取PATA数据目录路径
            string pataDirectory = GetPataDirectoryPath(pcapFilePath);

            // 返回完整的PATA文件路径
            return Path.Combine(pataDirectory, relativePath);
        }

        /// <summary>
        /// 获取最新的PATA文件路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <returns>最新的PATA文件路径，如果不存在则返回null</returns>
        public static string GetLatestPataFile(string pcapFilePath)
        {
            if (string.IsNullOrEmpty(pcapFilePath))
            {
                throw new ArgumentException("PCAP文件路径不能为空", nameof(pcapFilePath));
            }

            // 获取PATA数据目录路径
            string pataDirectory = GetPataDirectoryPath(pcapFilePath);
            
            // 检查目录是否存在
            if (!Directory.Exists(pataDirectory))
            {
                return null;
            }

            // 获取所有PATA文件
            var files = Directory.GetFiles(pataDirectory, "*.pata");
            if (files.Length == 0)
            {
                return null;
            }

            // 找到最新的文件（按文件名排序，因为文件名包含日期时间）
            Array.Sort(files);
            return files[files.Length - 1];
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
