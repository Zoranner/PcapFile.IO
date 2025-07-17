using System;
using System.Collections.Generic;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 数据读取接口
    /// </summary>
    public interface IPcapReader : IDisposable
    {
        #region 属性

        /// <summary>
        /// 获取已读取的数据包数量
        /// </summary>
        long PacketCount { get; }

        /// <summary>
        /// 获取文件大小
        /// </summary>
        long FileSize { get; }

        /// <summary>
        /// 获取当前文件路径
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// 文件是否已打开
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// 获取数据集名称
        /// </summary>
        string ProjectName { get; }

        /// <summary>
        /// 获取输入目录路径
        /// </summary>
        string InputDirectory { get; }

        /// <summary>
        /// 获取当前读取位置（字节偏移量）
        /// </summary>
        long Position { get; }

        /// <summary>
        /// 获取是否已到达文件末尾
        /// </summary>
        bool EndOfFile { get; }

        /// <summary>
        /// 获取当前文件中的总数据包数量
        /// </summary>
        long TotalPacketCount { get; }

        #endregion

        #region 方法

        /// <summary>
        /// 打开现有数据目录
        /// </summary>
        /// <param name="baseDirectory">基础目录路径</param>
        /// <param name="projectName">数据集名称</param>
        /// <returns>是否成功打开</returns>
        bool Open(string baseDirectory, string projectName);

        /// <summary>
        /// 打开指定的PCAP文件
        /// </summary>
        /// <param name="filePath">PCAP文件路径</param>
        /// <returns>是否成功打开</returns>
        bool OpenFile(string filePath);

        /// <summary>
        /// 关闭数据文件
        /// </summary>
        void Close();

        /// <summary>
        /// 读取下一个数据包
        /// </summary>
        /// <returns>读取到的数据包，如果到达文件末尾则返回null</returns>
        DataPacket ReadNextPacket();

        /// <summary>
        /// 读取指定数量的数据包
        /// </summary>
        /// <param name="count">要读取的数据包数量</param>
        /// <returns>读取到的数据包列表</returns>
        IEnumerable<DataPacket> ReadPackets(int count);

        /// <summary>
        /// 读取所有剩余的数据包
        /// </summary>
        /// <returns>读取到的数据包列表</returns>
        IEnumerable<DataPacket> ReadAllPackets();

        /// <summary>
        /// 重置读取位置到文件开头
        /// </summary>
        void Reset();

        /// <summary>
        /// 移动到指定的数据包位置
        /// </summary>
        /// <param name="packetIndex">数据包索引（从0开始）</param>
        /// <returns>是否成功移动到指定位置</returns>
        bool SeekToPacket(long packetIndex);

        /// <summary>
        /// 获取数据集目录中的所有PCAP文件列表
        /// </summary>
        /// <returns>PCAP文件路径列表</returns>
        IEnumerable<string> GetProjectFiles();

        #endregion
    }
}
