using System;
using System.Collections.Generic;
using System.IO;
using KimoTech.PcapFile.IO.Configuration;
using KimoTech.PcapFile.IO.Interfaces;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Writers;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件写入器，提供创建和写入PCAP文件的功能
    /// </summary>
    /// <remarks>
    /// 该类负责管理数据文件的写入操作
    /// </remarks>
    public class PcapWriter : IPcapWriter
    {
        #region 私有字段

        /// <summary>
        /// PCAP文件写入器，负责管理PCAP数据文件的创建、打开、写入和关闭操作
        /// </summary>
        private readonly PcapFileWriter _PcapFileWriter;

        /// <summary>
        /// 标记对象是否已被释放
        /// </summary>
        private bool _IsDisposed;

        /// <summary>
        /// 当前写入的数据包总大小
        /// </summary>
        private long _TotalSize;

        /// <summary>
        /// 标记是否已写入第一个数据包
        /// </summary>
        private bool _FirstPacketWritten;

        /// <summary>
        /// 当前文件ID，从1开始计数
        /// </summary>
        private uint _CurrentFileId;

        /// <summary>
        /// 最后一个数据包的时间戳
        /// </summary>
        private long _LastPacketTimestamp;

        /// <summary>
        /// 基础目录路径
        /// </summary>
        private string _BaseDirectory;

        /// <summary>
        /// 数据工程名称
        /// </summary>
        private string _ProjectName;

        /// <summary>
        /// 输出目录路径（工程数据目录）
        /// </summary>
        private string _OutputDirectory;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PCAP文件写入器
        /// </summary>
        public PcapWriter()
        {
            _PcapFileWriter = new PcapFileWriter();
            _IsDisposed = false;
            _CurrentFileId = 0;
        }

        #endregion

        #region 属性

        /// <inheritdoc />
        public long PacketCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 计算总文件大小只包含当前PCAP数据文件的大小
        /// </remarks>
        public long FileSize
        {
            get
            {
                ThrowIfDisposed();
                return _TotalSize;
            }
        }

        /// <inheritdoc />
        public string FilePath => _PcapFileWriter.FilePath;

        /// <inheritdoc />
        /// <remarks>
        /// 设置为true时，每次写入数据包后会自动刷新文件缓冲区。
        /// 设置为false时，需要手动调用Flush方法来刷新缓冲区。
        /// </remarks>
        public bool AutoFlush { get; set; }

        /// <inheritdoc />
        public bool IsOpen => _PcapFileWriter.IsOpen;

        /// <inheritdoc />
        public string ProjectName => _ProjectName;

        /// <inheritdoc />
        public string OutputDirectory => _OutputDirectory;

        #endregion

        #region 核心方法

        /// <summary>
        /// 创建新的PCAP文件
        /// </summary>
        /// <param name="baseDirectory">基础目录路径</param>
        /// <param name="projectName">数据工程名称</param>
        /// <returns>是否成功创建</returns>
        /// <exception cref="ArgumentNullException">目录路径为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public bool Create(string baseDirectory, string projectName)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(baseDirectory))
            {
                throw new ArgumentException("基础目录路径不能为空", nameof(baseDirectory));
            }

            if (string.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException("数据工程名称不能为空", nameof(projectName));
            }

            try
            {
                // 保存基础目录和工程名称
                _BaseDirectory = baseDirectory;
                _ProjectName = projectName;

                // 创建基础目录
                if (!Directory.Exists(baseDirectory))
                {
                    Directory.CreateDirectory(baseDirectory);
                }

                // 创建数据工程目录
                _OutputDirectory = Path.Combine(baseDirectory, projectName);
                if (!Directory.Exists(_OutputDirectory))
                {
                    Directory.CreateDirectory(_OutputDirectory);
                }

                // 初始化状态
                ResetState();
                AutoFlush = true;

                return true;
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"创建文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 打开现有PCAP文件
        /// </summary>
        /// <param name="baseDirectory">基础目录路径</param>
        /// <param name="projectName">数据工程名称</param>
        /// <returns>是否成功打开</returns>
        public bool Open(string baseDirectory, string projectName)
        {
            ThrowIfDisposed();

            try
            {
                if (string.IsNullOrEmpty(baseDirectory))
                {
                    throw new ArgumentException("基础目录路径不能为空", nameof(baseDirectory));
                }

                if (string.IsNullOrEmpty(projectName))
                {
                    throw new ArgumentException("数据工程名称不能为空", nameof(projectName));
                }

                // 保存基础目录和工程名称
                _BaseDirectory = baseDirectory;
                _ProjectName = projectName;

                // 检查并创建基础目录
                if (!Directory.Exists(baseDirectory))
                {
                    Directory.CreateDirectory(baseDirectory);
                }

                // 检查数据工程目录
                _OutputDirectory = Path.Combine(baseDirectory, projectName);
                if (!Directory.Exists(_OutputDirectory))
                {
                    Directory.CreateDirectory(_OutputDirectory);
                }

                // 初始化状态
                ResetState();
                return true;
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"打开文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 关闭PCAP文件写入器
        /// </summary>
        public void Close()
        {
            ThrowIfDisposed();

            // 关闭数据文件
            _PcapFileWriter.Close();

            // 重置状态
            ResetState();
        }

        /// <summary>
        /// 写入单个数据包
        /// </summary>
        /// <param name="packet">数据包</param>
        /// <returns>是否成功写入</returns>
        public bool WritePacket(DataPacket packet)
        {
            ThrowIfDisposed();

            try
            {
                if (packet == null)
                {
                    throw new ArgumentNullException(nameof(packet));
                }

                // 处理第一个数据包的初始化
                if (!_FirstPacketWritten)
                {
                    InitializeFirstPacket(packet);
                }

                // 检查是否需要创建新文件
                CheckAndCreateNewFile(packet);

                // 写入数据包到PCAP文件
                _PcapFileWriter.WritePacket(packet);

                // 更新统计信息
                _TotalSize += packet.TotalSize;
                PacketCount++;
                _LastPacketTimestamp =
                    packet.Header.TimestampSeconds * 1_000_000_000L
                    + packet.Header.TimestampNanoseconds;

                // 如果启用了自动刷新，则立即刷新缓冲区
                if (AutoFlush)
                {
                    Flush();
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"写入数据包失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 批量写入数据包
        /// </summary>
        /// <param name="packets">数据包集合</param>
        /// <returns>是否成功写入</returns>
        public bool WritePackets(IEnumerable<DataPacket> packets)
        {
            ThrowIfDisposed();

            try
            {
                if (packets == null)
                {
                    throw new ArgumentNullException(nameof(packets));
                }

                var result = true;
                foreach (var packet in packets)
                {
                    result &= WritePacket(packet);
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new IOException($"批量写入数据包失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 刷新文件缓冲区
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _PcapFileWriter.Flush();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 重置状态
        /// </summary>
        private void ResetState()
        {
            PacketCount = 0;
            _TotalSize = 0;
            _FirstPacketWritten = false;
            _CurrentFileId = 0;
            _LastPacketTimestamp = 0;
        }

        /// <summary>
        /// 初始化第一个数据包写入
        /// </summary>
        private void InitializeFirstPacket(DataPacket packet)
        {
            // 创建第一个数据文件
            CreateNewFile(packet);
            _FirstPacketWritten = true;
        }

        /// <summary>
        /// 检查是否应该创建新文件
        /// </summary>
        private bool ShouldCreateNewFile()
        {
            return _PcapFileWriter.CurrentPacketCount >= _PcapFileWriter.MaxPacketsPerFile;
        }

        /// <summary>
        /// 检查并创建新文件
        /// </summary>
        private void CheckAndCreateNewFile(DataPacket packet)
        {
            if (!_FirstPacketWritten || ShouldCreateNewFile())
            {
                CreateNewFile(packet);
            }
        }

        /// <summary>
        /// 创建新文件
        /// </summary>
        private void CreateNewFile(DataPacket packet)
        {
            // 确保目录存在
            if (!Directory.Exists(_OutputDirectory))
            {
                Directory.CreateDirectory(_OutputDirectory);
            }

            // 获取数据包捕获时间
            var timestamp = packet.CaptureTime;

            // 创建新的PCAP文件
            var path = Path.Combine(
                _OutputDirectory,
                $"data_{timestamp.ToString(FileVersionConfig.DEFAULT_FILE_NAME_FORMAT)}.pcap"
            );

            if (_PcapFileWriter.IsOpen)
            {
                _PcapFileWriter.Close();
            }

            _PcapFileWriter.Create(path);
            _CurrentFileId++;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    // 关闭数据文件
                    _PcapFileWriter?.Close();
                    _PcapFileWriter?.Dispose();
                }

                _IsDisposed = true;
            }
        }

        /// <summary>
        /// 释放流资源
        /// </summary>
        private void DisposeStreams()
        {
            _PcapFileWriter?.Dispose();
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapWriter));
            }
        }

        #endregion
    }
}
