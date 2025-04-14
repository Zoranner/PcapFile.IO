using System;
using System.IO;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件写入器，负责管理PCAP数据文件的创建、打开、写入和关闭操作
    /// </summary>
    /// <remarks>
    /// 注意：此类设计为单线程使用，不支持多线程并发写入。
    /// </remarks>
    internal class PcapFileWriter : IDisposable
    {
        #region 字段

        private FileStream _FileStream;
        private BinaryWriter _BinaryWriter;
        private bool _IsDisposed;
        private readonly string _FileNameFormat;
        private readonly byte[] _WriteBuffer;
        private int _WriteBufferPosition;
        #endregion

        #region 属性

        /// <summary>
        /// 获取当前PCAP文件路径
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

        /// <summary>
        /// 获取当前文件中的数据包数量
        /// </summary>
        public int CurrentPacketCount { get; private set; }

        /// <summary>
        /// 获取最大数据包数量
        /// </summary>
        public int MaxPacketsPerFile { get; }

        /// <summary>
        /// 获取文件头信息
        /// </summary>
        public PcapFileHeader Header { get; private set; }

        #endregion

        #region 构造函数

        public PcapFileWriter(
            int maxPacketsPerFile = FileVersionConfig.DEFAULT_MAX_PACKETS_PER_FILE,
            string fileNameFormat = FileVersionConfig.DEFAULT_FILE_NAME_FORMAT,
            int bufferSize = FileVersionConfig.MAX_BUFFER_SIZE
        )
        {
            MaxPacketsPerFile = maxPacketsPerFile;
            _FileNameFormat = fileNameFormat;
            _WriteBuffer = new byte[bufferSize];
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化PCAP文件写入器
        /// </summary>
        public void Initialize()
        {
            ThrowIfDisposed();

            // 重置状态
            CurrentPacketCount = 0;
            TotalSize = 0;
        }

        /// <summary>
        /// 创建新的PCAP文件
        /// </summary>
        /// <param name="filePath">PCAP文件路径</param>
        public void Create(string filePath)
        {
            ThrowIfDisposed();

            // 关闭现有文件
            DisposeStreams();

            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建新文件
            _FileStream = StreamHelper.CreateFileStream(
                filePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);
            FilePath = filePath;

            // 重置计数器
            CurrentPacketCount = 0;

            // 写入文件头
            WriteHeader(PcapFileHeader.Create(0));
        }

        /// <summary>
        /// 打开现有的PCAP文件
        /// </summary>
        /// <param name="filePath">PCAP文件路径</param>
        public void Open(string filePath)
        {
            ThrowIfDisposed();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            // 关闭现有文件
            DisposeStreams();

            // 打开文件
            FilePath = filePath;
            _FileStream = StreamHelper.CreateFileStream(
                FilePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None
            );
            _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);

            // 读取文件头并更新计数
            StreamHelper.ReadStructure<PcapFileHeader>(_FileStream);
            CurrentPacketCount = (int)(
                (_FileStream.Length - PcapFileHeader.HEADER_SIZE) / DataPacketHeader.HEADER_SIZE
            );
        }

        /// <summary>
        /// 写入PCAP文件头
        /// </summary>
        public void WriteHeader(PcapFileHeader header)
        {
            ThrowIfDisposed();

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            var headerBytes = header.ToBytes();
            WriteToBuffer(headerBytes);
            FlushBuffer();
        }

        /// <summary>
        /// 写入数据包到PCAP文件
        /// </summary>
        /// <returns>数据包在文件中的偏移量</returns>
        public long WritePacket(DataPacket packet)
        {
            ThrowIfDisposed();

            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                // 获取当前位置作为偏移量
                var offset = Position;

                // 写入数据包
                var headerBytes = packet.Header.ToBytes();
                WriteToBuffer(headerBytes);
                WriteToBuffer(packet.Data);

                // 更新计数和大小
                CurrentPacketCount++;
                TotalSize += packet.TotalSize;

                return offset;
            }
            catch (Exception ex)
            {
                throw new IOException("写入数据包时发生错误", ex);
            }
        }

        /// <summary>
        /// 写入数据到缓冲区
        /// </summary>
        private void WriteToBuffer(byte[] data)
        {
            // 如果数据大小超过缓冲区大小，直接写入而不使用缓冲区
            if (data.Length > _WriteBuffer.Length)
            {
                // 先刷新现有缓冲区内容
                FlushBuffer();

                // 大数据直接分块写入，避免一次性加载到内存
                const int chunkSize = 4 * 1024 * 1024; // 4MB 块大小
                if (data.Length > chunkSize)
                {
                    var offset = 0;
                    while (offset < data.Length)
                    {
                        var size = Math.Min(chunkSize, data.Length - offset);
                        _BinaryWriter.Write(data, offset, size);
                        offset += size;
                    }

                    return;
                }
                else
                {
                    // 数据适中，直接写入
                    _BinaryWriter.Write(data);
                    return;
                }
            }

            // 如果当前缓冲区剩余空间不足，先刷新
            if (_WriteBufferPosition + data.Length > _WriteBuffer.Length)
            {
                FlushBuffer();
            }

            Array.Copy(data, 0, _WriteBuffer, _WriteBufferPosition, data.Length);
            _WriteBufferPosition += data.Length;
        }

        /// <summary>
        /// 刷新缓冲区
        /// </summary>
        private void FlushBuffer()
        {
            if (_WriteBufferPosition > 0 && _BinaryWriter != null)
            {
                _BinaryWriter.Write(_WriteBuffer, 0, _WriteBufferPosition);
                _WriteBufferPosition = 0;
            }
        }

        #endregion

        #region IDisposable实现

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
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    DisposeStreams();
                }

                _IsDisposed = true;
            }
        }

        /// <summary>
        /// 关闭文件
        /// </summary>
        public void Close()
        {
            ThrowIfDisposed();

            // 刷新缓冲区
            FlushBuffer();

            // 关闭流
            DisposeStreams();
        }

        /// <summary>
        /// 刷新缓冲区到磁盘
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();

            if (_FileStream != null)
            {
                FlushBuffer();
                _FileStream.Flush();
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileWriter));
            }
        }

        /// <summary>
        /// 释放流资源
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
