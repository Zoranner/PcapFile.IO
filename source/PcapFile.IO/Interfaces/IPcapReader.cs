using System;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Structures;

namespace KimoTech.PcapFile.IO.Interfaces
{
    /// <summary>
    /// 数据读取接口
    /// </summary>
    public interface IPcapReader : IDisposable
    {
        #region 属性

        /// <summary>
        /// 获取文件头
        /// </summary>
        PcapFileHeader Header { get; }

        /// <summary>
        /// 获取已读取的数据包数量
        /// </summary>
        long PacketCount { get; }

        /// <summary>
        /// 获取文件的开始时间
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// 获取文件的结束时间
        /// </summary>
        DateTime EndTime { get; }

        /// <summary>
        /// 获取当前读取位置
        /// </summary>
        long Position { get; }

        /// <summary>
        /// 是否已经到达文件末尾
        /// </summary>
        bool IsEndOfFile { get; }

        /// <summary>
        /// 文件大小
        /// </summary>
        long FileSize { get; }

        /// <summary>
        /// 当前文件路径
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// 文件是否已打开
        /// </summary>
        bool IsOpen { get; }

        #endregion

        #region 同步方法

        /// <summary>
        /// 打开数据文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功打开</returns>
        bool Open(string filePath);

        /// <summary>
        /// 关闭数据文件
        /// </summary>
        void Close();

        /// <summary>
        /// 读取下一个数据包
        /// </summary>
        /// <returns>数据包，如果没有更多数据则返回空数据包</returns>
        DataPacket ReadNextPacket();

        /// <summary>
        /// 跳转到指定时间
        /// </summary>
        /// <param name="timestamp">目标时间戳</param>
        /// <returns>是否成功跳转</returns>
        bool SeekToTime(DateTime timestamp);

        /// <summary>
        /// 跳转到指定位置
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <returns>是否成功跳转</returns>
        bool Seek(long position);

        /// <summary>
        /// 重置读取位置到文件开始
        /// </summary>
        void Reset();

        #endregion

        #region 异步方法

        /// <summary>
        /// 异步读取下一个数据包
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据包，如果没有更多数据则返回空数据包</returns>
        Task<DataPacket> ReadNextPacketAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步跳转到指定时间
        /// </summary>
        /// <param name="timestamp">目标时间戳</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功跳转</returns>
        Task<bool> SeekToTimeAsync(
            DateTime timestamp,
            CancellationToken cancellationToken = default
        );

        #endregion
    }
}
