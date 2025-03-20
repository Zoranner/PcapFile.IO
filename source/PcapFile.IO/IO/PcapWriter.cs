using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Interfaces;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP数据写入器，提供PCAP文件的创建、打开、写入和关闭操作
    /// </summary>
    /// <remarks>
    /// 该类封装了PCAP文件的底层操作，提供了简单易用的接口。
    /// 支持同步和异步操作，自动管理文件资源，确保正确释放。
    /// 该类不支持并发写入，所有写入操作都是串行执行的。
    /// </remarks>
    public sealed class PcapWriter : IPcapWriter
    {
        #region 字段

        /// <summary>
        /// PCAP文件管理器，负责管理PCAP索引文件的创建、打开、写入和关闭操作
        /// </summary>
        private readonly PcapFileManager _PcapFileManager;

        /// <summary>
        /// PATA文件管理器，负责管理PATA数据文件的创建、打开、写入和关闭操作
        /// </summary>
        private readonly PataFileManager _PataFileManager;

        /// <summary>
        /// 用于同步写入操作的锁对象
        /// </summary>
        private readonly object _WriteLock = new object();

        /// <summary>
        /// 标记对象是否已被释放
        /// </summary>
        private bool _IsDisposed;

        /// <summary>
        /// 当前写入的数据包总大小
        /// </summary>
        private long _TotalSize;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PCAP写入器的新实例
        /// </summary>
        /// <remarks>
        /// 构造函数会创建PCAP和PATA文件管理器的实例。
        /// 这些实例会在对象释放时自动释放。
        /// </remarks>
        public PcapWriter()
        {
            _PcapFileManager = new PcapFileManager();
            _PataFileManager = new PataFileManager();
        }

        #endregion

        #region 属性

        /// <inheritdoc />
        public long PacketCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 计算总文件大小时会包含：
        /// 1. PCAP索引文件的大小
        /// 2. PATA数据目录下所有数据文件的大小总和
        /// </remarks>
        public long FileSize
        {
            get
            {
                ThrowIfDisposed();
                return _PcapFileManager.FileSize + _TotalSize;
            }
        }

        /// <inheritdoc />
        public string FilePath => _PcapFileManager.FilePath;

        /// <inheritdoc />
        /// <remarks>
        /// 设置为true时，每次写入数据包后会自动刷新文件缓冲区。
        /// 设置为false时，需要手动调用Flush方法来刷新缓冲区。
        /// </remarks>
        public bool AutoFlush { get; set; }

        /// <inheritdoc />
        public bool IsOpen => _PcapFileManager.IsOpen;

        #endregion

        #region 公共方法

        /// <inheritdoc />
        /// <remarks>
        /// 创建新文件时会：
        /// 1. 创建必要的目录结构
        /// 2. 创建PCAP索引文件
        /// 3. 创建PATA数据文件
        /// 4. 写入默认的文件头信息
        /// </remarks>
        public bool Create(string filePath, PcapFileHeader header = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            try
            {
                // 创建目录
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    var pataDirectory = PathHelper.GetPataDirectoryPath(filePath);
                    Directory.CreateDirectory(pataDirectory);
                }

                // 创建文件
                _PcapFileManager.Create(filePath);
                _PataFileManager.Create(filePath);

                // 写入文件头
                if (header.MagicNumber == 0)
                {
                    header = PcapFileHeader.Create(1, 0);
                }

                _PcapFileManager.WriteHeader(header);

                PacketCount = 0;
                AutoFlush = true;

                return true;
            }
            catch (Exception ex)
            {
                Close();
                throw new IOException($"创建文件失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 打开现有文件时会：
        /// 1. 验证文件是否存在
        /// 2. 打开PCAP索引文件
        /// 3. 打开PATA数据文件
        /// 4. 读取文件头信息以获取数据包计数
        /// </remarks>
        public bool Open(string filePath)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            try
            {
                // 打开文件
                _PcapFileManager.Open(filePath);
                _PataFileManager.Open(filePath);

                // 读取文件头
                var header = _PcapFileManager.ReadHeader();
                PacketCount = header.TotalIndexCount;
                AutoFlush = true;

                return true;
            }
            catch (Exception ex)
            {
                Close();
                throw new IOException($"打开文件失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 关闭文件时会：
        /// 1. 关闭PCAP索引文件
        /// 2. 关闭PATA数据文件
        /// 3. 重置数据包计数
        /// </remarks>
        public void Close()
        {
            if (!_IsDisposed)
            {
                _PataFileManager.Close();
                _PcapFileManager.Close();
                PacketCount = 0;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 写入数据包时会：
        /// 1. 将数据包写入PATA文件
        /// 2. 在PCAP文件中创建对应的索引条目
        /// 3. 更新数据包计数
        /// 4. 如果启用了自动刷新，则刷新文件缓冲区
        /// 注意：该方法不支持并发调用，所有写入操作都是串行执行的
        /// </remarks>
        public bool WritePacket(DataPacket packet)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            lock (_WriteLock)
            {
                try
                {
                    // 写入数据包
                    _PataFileManager.WritePacket(packet);

                    // 更新索引
                    var indexEntry = PataFileIndexEntry.Create(
                        packet.Header.Timestamp,
                        _PataFileManager.Position - packet.TotalSize
                    );
                    _PcapFileManager.WriteIndexEntry(indexEntry);

                    // 更新大小
                    _TotalSize += packet.TotalSize;
                    PacketCount++;

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
        }

        /// <inheritdoc />
        /// <remarks>
        /// 批量写入数据包时会：
        /// 1. 遍历数据包集合
        /// 2. 逐个写入数据包
        /// 3. 如果任何一个数据包写入失败，则立即返回false
        /// 注意：该方法不支持并发调用，所有写入操作都是串行执行的
        /// </remarks>
        public bool WritePackets(IEnumerable<DataPacket> packets)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (packets == null)
            {
                throw new ArgumentNullException(nameof(packets));
            }

            lock (_WriteLock)
            {
                try
                {
                    foreach (var packet in packets)
                    {
                        if (!WritePacket(packet))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    throw new IOException($"批量写入数据包失败: {ex.Message}", ex);
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 刷新文件缓冲区时会：
        /// 1. 刷新PATA文件的缓冲区
        /// 2. 刷新PCAP文件的缓冲区
        /// </remarks>
        public void Flush()
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            _PataFileManager.Flush();
            _PcapFileManager.Flush();
        }

        #endregion

        #region 异步方法

        /// <inheritdoc />
        /// <remarks>
        /// 异步写入数据包时会：
        /// 1. 将数据包异步写入PATA文件
        /// 2. 在PCAP文件中创建对应的索引条目
        /// 3. 更新数据包计数
        /// 4. 如果启用了自动刷新，则异步刷新文件缓冲区
        /// 注意：该方法不支持并发调用，所有写入操作都是串行执行的
        /// </remarks>
        public async Task<bool> WritePacketAsync(
            DataPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            try
            {
                // 写入数据包
                await _PataFileManager.WritePacketAsync(packet, cancellationToken);

                // 更新索引
                var indexEntry = PataFileIndexEntry.Create(
                    packet.Header.Timestamp,
                    _PataFileManager.Position - packet.TotalSize
                );

                lock (_WriteLock)
                {
                    _PcapFileManager.WriteIndexEntry(indexEntry);
                    _TotalSize += packet.TotalSize;
                    PacketCount++;
                }

                if (AutoFlush)
                {
                    await FlushAsync(cancellationToken);
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"异步写入数据包失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 异步批量写入数据包时会：
        /// 1. 遍历数据包集合
        /// 2. 逐个异步写入数据包
        /// 3. 如果任何一个数据包写入失败，则立即返回false
        /// 注意：该方法不支持并发调用，所有写入操作都是串行执行的
        /// </remarks>
        public async Task<bool> WritePacketsAsync(
            IEnumerable<DataPacket> packets,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            if (packets == null)
            {
                throw new ArgumentNullException(nameof(packets));
            }

            try
            {
                foreach (var packet in packets)
                {
                    if (!await WritePacketAsync(packet, cancellationToken))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"异步批量写入数据包失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 异步刷新文件缓冲区时会：
        /// 1. 异步刷新PATA文件的缓冲区
        /// 2. 异步刷新PCAP文件的缓冲区
        /// </remarks>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!IsOpen)
            {
                throw new InvalidOperationException("文件未打开");
            }

            await _PataFileManager.FlushAsync(cancellationToken);
            await _PcapFileManager.FlushAsync(cancellationToken);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        /// <remarks>
        /// 释放资源时会：
        /// 1. 释放PATA文件管理器的资源
        /// 2. 释放PCAP文件管理器的资源
        /// 3. 标记对象为已释放状态
        /// </remarks>
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                _PataFileManager.Dispose();
                _PcapFileManager.Dispose();
                _IsDisposed = true;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 检查对象是否已释放，如果已释放则抛出异常
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <remarks>
        /// 此方法用于确保在对象被释放后不会继续使用。
        /// 所有公共方法都应该在开始时调用此方法。
        /// </remarks>
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
