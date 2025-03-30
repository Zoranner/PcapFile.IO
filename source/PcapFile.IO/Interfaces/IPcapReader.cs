using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Structures;

namespace KimoTech.PcapFile.IO.Interfaces
{
    /// <summary>
    /// PCAP数据读取器接口，定义PCAP文件的读取和定位操作
    /// </summary>
    public interface IPcapReader : IDisposable
    {
        /// <summary>
        /// 获取数据包总数
        /// </summary>
        long PacketCount { get; }

        /// <summary>
        /// 获取当前读取位置
        /// </summary>
        long CurrentPosition { get; }

        /// <summary>
        /// 获取数据起始时间
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// 获取数据结束时间
        /// </summary>
        DateTime EndTime { get; }

        /// <summary>
        /// 获取当前文件路径
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// 获取文件是否已打开
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// 打开PCAP文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功打开</returns>
        /// <exception cref="ArgumentException">文件路径为空或无效</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">打开文件时发生错误</exception>
        bool Open(string filePath);

        /// <summary>
        /// 关闭PCAP文件
        /// </summary>
        void Close();

        /// <summary>
        /// 读取下一个数据包
        /// </summary>
        /// <returns>数据包，如果没有更多数据包则返回null</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        DataPacket ReadNextPacket();

        /// <summary>
        /// 异步读取下一个数据包
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据包，如果没有更多数据包则返回null</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        Task<DataPacket> ReadNextPacketAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量读取数据包
        /// </summary>
        /// <param name="count">要读取的数据包数量</param>
        /// <returns>数据包列表</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="ArgumentOutOfRangeException">count小于等于0</exception>
        List<DataPacket> ReadPackets(int count);

        /// <summary>
        /// 异步批量读取数据包
        /// </summary>
        /// <param name="count">要读取的数据包数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据包列表</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="ArgumentOutOfRangeException">count小于等于0</exception>
        Task<List<DataPacket>> ReadPacketsAsync(
            int count,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// 定位到指定时间
        /// </summary>
        /// <param name="timestamp">目标时间戳</param>
        /// <returns>是否成功定位</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        bool SeekToTime(DateTime timestamp);

        /// <summary>
        /// 定位到指定位置
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <returns>是否成功定位</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        bool SeekToPosition(long position);

        /// <summary>
        /// 读取指定位置的数据包
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <returns>数据包，如果没有更多数据包则返回null</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        DataPacket ReadPacketAt(long position);

        /// <summary>
        /// 异步读取指定位置的数据包
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据包，如果没有更多数据包则返回null</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        Task<DataPacket> ReadPacketAtAsync(
            long position,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// 重置读取位置到文件开头
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        void Reset();
    }
}
