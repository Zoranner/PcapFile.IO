using System;
using System.IO;
using System.Linq;
using KimoTech.PcapFile.IO.Configuration;

namespace KimoTech.PcapFile.IO.Utils
{
    /// <summary>
    /// 路径帮助类，提供路径相关的工具方法
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// 获取PCAP数据目录路径
        /// </summary>
        /// <param name="projFilePath">PROJ工程文件路径</param>
        /// <returns>PCAP数据目录路径</returns>
        public static string GetPcapDirectoryPath(string projFilePath)
        {
            if (string.IsNullOrEmpty(projFilePath))
            {
                throw new ArgumentNullException(nameof(projFilePath));
            }

            // 获取PROJ文件所在目录
            string projDirectory = Path.GetDirectoryName(projFilePath);

            // 获取PROJ文件名（不包含扩展名）
            string projName = Path.GetFileNameWithoutExtension(projFilePath);

            // 返回PCAP数据目录路径
            return Path.Combine(projDirectory, projName);
        }

        /// <summary>
        /// 根据时间戳创建PCAP文件路径
        /// </summary>
        /// <param name="projFilePath">PROJ工程文件路径</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>PCAP文件路径</returns>
        public static string GetPcapFilePath(string projFilePath, DateTime timestamp)
        {
            if (string.IsNullOrEmpty(projFilePath))
            {
                throw new ArgumentNullException(nameof(projFilePath));
            }

            // 获取PCAP数据目录路径
            string pcapDirectory = GetPcapDirectoryPath(projFilePath);

            // 确保目录存在
            Directory.CreateDirectory(pcapDirectory);

            // 创建文件名：data_yyMMdd_HHmmss_fffffff.pcap
            string fileName =
                $"data_{timestamp.ToString(FileVersionConfig.DEFAULT_FILE_NAME_FORMAT)}.pcap";

            // 返回完整的PCAP文件路径
            return Path.Combine(pcapDirectory, fileName);
        }

        /// <summary>
        /// 获取PCAP文件的完整路径
        /// </summary>
        /// <param name="projFilePath">PROJ工程文件路径</param>
        /// <param name="relativePath">PCAP文件相对路径</param>
        /// <returns>PCAP文件完整路径</returns>
        public static string GetFullPcapFilePath(string projFilePath, string relativePath)
        {
            if (string.IsNullOrEmpty(projFilePath))
            {
                throw new ArgumentNullException(nameof(projFilePath));
            }

            if (string.IsNullOrEmpty(relativePath))
            {
                throw new ArgumentNullException(nameof(relativePath));
            }

            // 获取PCAP数据目录路径
            string pcapDirectory = GetPcapDirectoryPath(projFilePath);

            // 返回完整的PCAP文件路径
            return Path.Combine(pcapDirectory, relativePath);
        }

        /// <summary>
        /// 获取最新的PCAP文件路径
        /// </summary>
        /// <param name="projFilePath">PROJ工程文件路径</param>
        /// <returns>最新的PCAP文件路径，如果不存在则返回null</returns>
        public static string GetLatestPcapFile(string projFilePath)
        {
            if (string.IsNullOrEmpty(projFilePath))
            {
                throw new ArgumentNullException(nameof(projFilePath));
            }

            // 获取PCAP数据目录路径
            string pcapDirectory = GetPcapDirectoryPath(projFilePath);

            // 检查目录是否存在
            if (!Directory.Exists(pcapDirectory))
            {
                return null;
            }

            // 获取所有PCAP文件
            var files = Directory.GetFiles(pcapDirectory, "*.pcap");

            // 如果没有文件，返回null
            if (files.Length == 0)
            {
                return null;
            }

            // 返回最新的文件
            return files.OrderByDescending(f => new FileInfo(f).CreationTime).First();
        }

        /// <summary>
        /// 清空PCAP目录
        /// </summary>
        /// <param name="projFilePath">PROJ工程文件路径</param>
        /// <returns>是否成功清空</returns>
        public static bool ClearPcapDirectory(string projFilePath)
        {
            var pcapDirectory = GetPcapDirectoryPath(projFilePath);
            if (!Directory.Exists(pcapDirectory))
            {
                return false;
            }

            try
            {
                // 删除目录及其所有内容，然后重新创建
                Directory.Delete(pcapDirectory, true);
                Directory.CreateDirectory(pcapDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
