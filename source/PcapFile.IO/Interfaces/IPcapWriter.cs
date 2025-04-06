using System;
using System.Collections.Generic;
using KimoTech.PcapFile.IO.Structures;

namespace KimoTech.PcapFile.IO.Interfaces
{
    /// <summary>
    /// 数据写入接口
    /// </summary>
    public interface IPcapWriter : IDisposable
    {
        #region 属性

        /// <summary>
        /// 获取已写入的数据包数量
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
        /// 是否自动刷新
        /// </summary>
        bool AutoFlush { get; set; }

        /// <summary>
        /// 文件是否已打开
        /// </summary>
        bool IsOpen { get; }

        #endregion

        #region 方法

        /// <summary>
        /// 创建新的数据文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="header">PROJ文件头</param>
        /// <returns>是否成功创建</returns>
        bool Create(string filePath, ProjFileHeader header = default);

        /// <summary>
        /// 打开现有数据文件进行追加
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功打开</returns>
        bool Open(string filePath);

        /// <summary>
        /// 关闭数据文件
        /// </summary>
        void Close();

        /// <summary>
        /// 写入数据包
        /// </summary>
        /// <param name="packet">数据包</param>
        /// <returns>是否成功写入</returns>
        bool WritePacket(DataPacket packet);

        /// <summary>
        /// 批量写入数据包
        /// </summary>
        /// <param name="packets">数据包列表</param>
        /// <returns>是否成功写入</returns>
        bool WritePackets(IEnumerable<DataPacket> packets);

        /// <summary>
        /// 刷新缓冲区
        /// </summary>
        void Flush();

        /// <summary>
        /// 将当前位置设置到指定偏移量
        /// </summary>
        /// <param name="offset">偏移量</param>
        /// <returns>是否成功设置</returns>
        bool Seek(long offset);

        #endregion
    }
}
