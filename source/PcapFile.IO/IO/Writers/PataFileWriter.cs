using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Configuration;
using KimoTech.PcapFile.IO.Extensions;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PATA文件写入器，负责管理PATA数据文件的创建、打开、写入和关闭操作
    /// </summary>
    internal class PataFileWriter : IDisposable
    {
        #region 字段

        private FileStream _FileStream;
        private BinaryWriter _BinaryWriter;
        private int _CurrentPacketCount;
        private bool _IsDisposed;

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前PATA文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 获取当前文件流的位置
        /// </summary>
        public long Position => _FileStream?.Position ?? 0;

        /// <summary>
        /// 获取已写入的数据包总大小
        /// </summary>
        public long TotalSize { get; private set; }

        /// <summary>
        /// 获取文件是否已打开且未释放
        /// </summary>
        public bool IsOpen => _FileStream != null && !_IsDisposed;

        #endregion

        #region 公共方法

        /// <summary>
        /// 创建新的PATA文件，使用临时文件作为初始存储
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        public void Create(string pcapFilePath)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileWriter));
            }

            // 删除已存在的PATA文件
            var pataDirectory = PathHelper.GetPataDirectoryPath(pcapFilePath);
            if (Directory.Exists(pataDirectory))
            {
                try
                {
                    Directory.Delete(pataDirectory, true);
                }
                catch (Exception ex)
                {
                    throw new IOException($"删除已存在的PATA文件失败: {ex.Message}", ex);
                }
            }

            _CurrentPacketCount = 0;
            TotalSize = 0;
            FilePath = pcapFilePath;

            var tempPath = Path.GetTempFileName();
            _FileStream = StreamHelper.CreateFileStream(
                tempPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);

            WriteHeader(PataFileHeader.Create(0));
        }

        /// <summary>
        /// 打开现有的PATA文件
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        public void Open(string pcapFilePath)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileWriter));
            }

            _CurrentPacketCount = 0;
            var pataDirectory = PathHelper.GetPataDirectoryPath(pcapFilePath);
            var files = Directory.GetFiles(pataDirectory, "data_*.pata");

            if (files.Length > 0)
            {
                Array.Sort(files);
                FilePath = files[^1];
                _FileStream = StreamHelper.CreateFileStream(
                    FilePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);

                StreamHelper.ReadStructure<PataFileHeader>(_FileStream);
                _CurrentPacketCount = (int)(
                    (_FileStream.Length - PataFileHeader.HEADER_SIZE) / DataPacketHeader.HEADER_SIZE
                );
            }
        }

        /// <summary>
        /// 写入PATA文件头
        /// </summary>
        /// <param name="header">文件头信息</param>
        public void WriteHeader(PataFileHeader header)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileWriter));
            }

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(header.ToBytes());
        }

        /// <summary>
        /// 写入数据包到PATA文件
        /// </summary>
        /// <param name="packet">要写入的数据包</param>
        /// <remarks>
        /// 当数据包数量达到最大限制时，会自动创建新文件
        /// 第一个数据包写入时会重命名临时文件为正式文件
        /// </remarks>
        public void WritePacket(DataPacket packet)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileWriter));
            }

            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            try
            {
                if (_CurrentPacketCount >= FileVersionConfig.MAX_PACKETS_PER_FILE)
                {
                    CreateNewFile(
                        Path.GetDirectoryName(FilePath),
                        packet.Header.Timestamp.FromUnixTimeMilliseconds()
                    );
                    _CurrentPacketCount = 0;
                }
                else if (_CurrentPacketCount == 0)
                {
                    var newPath = GetPataFilePath(
                        FilePath,
                        packet.Header.Timestamp.FromUnixTimeMilliseconds()
                    );
                    _FileStream.Close();
                    File.Move(_FileStream.Name, newPath);
                    _FileStream = StreamHelper.CreateFileStream(
                        newPath,
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.None
                    );
                    _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);
                    FilePath = newPath;
                }

                _BinaryWriter.Write(packet.Header.ToBytes());
                _BinaryWriter.Write(packet.Data);
                _CurrentPacketCount++;
                TotalSize += packet.TotalSize;
            }
            catch (Exception ex)
            {
                throw new IOException("写入数据包时发生错误", ex);
            }
        }

        /// <summary>
        /// 异步写入数据包到PATA文件
        /// </summary>
        /// <param name="packet">要写入的数据包</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <remarks>
        /// 当数据包数量达到最大限制时，会自动创建新文件
        /// 第一个数据包写入时会重命名临时文件为正式文件
        /// </remarks>
        public async Task WritePacketAsync(DataPacket packet, CancellationToken cancellationToken)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileWriter));
            }

            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (_CurrentPacketCount >= FileVersionConfig.MAX_PACKETS_PER_FILE)
            {
                CreateNewFile(
                    Path.GetDirectoryName(FilePath),
                    packet.Header.Timestamp.FromUnixTimeMilliseconds()
                );
                _CurrentPacketCount = 0;
            }
            else if (_CurrentPacketCount == 0 || _BinaryWriter == null)
            {
                CreateNewFile(FilePath, packet.Header.Timestamp.FromUnixTimeMilliseconds());
            }

            var headerBytes = packet.Header.ToBytes();
            await _FileStream.WriteAsync(new ReadOnlyMemory<byte>(headerBytes), cancellationToken);
            await _FileStream.WriteAsync(new ReadOnlyMemory<byte>(packet.Data), cancellationToken);
            _CurrentPacketCount++;
            TotalSize += packet.TotalSize;
        }

        /// <summary>
        /// 刷新文件缓冲区
        /// </summary>
        public void Flush()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileWriter));
            }

            _BinaryWriter?.Flush();
        }

        /// <summary>
        /// 异步刷新文件缓冲区
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileWriter));
            }

            if (_FileStream != null)
            {
                await _FileStream.FlushAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 关闭文件管理器
        /// </summary>
        public void Close()
        {
            DisposeStreams();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                DisposeStreams();
                _IsDisposed = true;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 根据PCAP文件路径和时间戳生成PATA文件路径
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>PATA文件路径</returns>
        private static string GetPataFilePath(string pcapFilePath, DateTime timestamp)
        {
            return PathHelper.GetPataFilePath(pcapFilePath, timestamp);
        }

        /// <summary>
        /// 创建新的PATA文件
        /// </summary>
        /// <param name="pcapFilePath">PCAP文件路径</param>
        /// <param name="timestamp">时间戳</param>
        private void CreateNewFile(string pcapFilePath, DateTime timestamp)
        {
            if (string.IsNullOrEmpty(pcapFilePath))
            {
                throw new ArgumentException("PCAP文件路径不能为空", nameof(pcapFilePath));
            }

            FilePath = PathHelper.GetPataFilePath(pcapFilePath, timestamp);
            DisposeStreams();

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _FileStream = StreamHelper.CreateFileStream(
                FilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);

            WriteHeader(PataFileHeader.Create(0));
        }

        /// <summary>
        /// 释放文件流和二进制写入器资源
        /// </summary>
        private void DisposeStreams()
        {
            if (_BinaryWriter != null)
            {
                _BinaryWriter.Dispose();
                _BinaryWriter = null;
            }

            if (_FileStream != null)
            {
                _FileStream.Dispose();
                _FileStream = null;
            }
        }

        #endregion
    }
}
