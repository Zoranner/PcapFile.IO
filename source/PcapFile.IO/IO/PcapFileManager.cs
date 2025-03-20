using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件管理器，负责管理PCAP文件的创建、打开、写入和关闭操作
    /// </summary>
    internal class PcapFileManager : IDisposable
    {
        #region 字段

        private FileStream _FileStream;
        private BinaryWriter _BinaryWriter;
        private bool _IsDisposed;

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前PCAP文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 获取当前文件大小
        /// </summary>
        public long FileSize => _FileStream?.Length ?? 0;

        /// <summary>
        /// 获取文件是否已打开且未释放
        /// </summary>
        public bool IsOpen => _FileStream != null && !_IsDisposed;

        #endregion

        #region 公共方法

        /// <summary>
        /// 创建新的PCAP文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <exception cref="ArgumentException">文件路径为空或无效</exception>
        /// <exception cref="IOException">创建文件时发生错误</exception>
        public void Create(string filePath)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileManager));
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            try
            {
                FilePath = filePath;
                _FileStream = StreamHelper.CreateFileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);
            }
            catch (Exception ex)
            {
                throw new IOException($"创建PCAP文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 打开现有的PCAP文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <exception cref="ArgumentException">文件路径为空或无效</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">打开文件时发生错误</exception>
        public void Open(string filePath)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileManager));
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PCAP文件不存在", filePath);
            }

            try
            {
                FilePath = filePath;
                _FileStream = StreamHelper.CreateFileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                _BinaryWriter = StreamHelper.CreateBinaryWriter(_FileStream);
            }
            catch (Exception ex)
            {
                throw new IOException($"打开PCAP文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 写入PCAP文件头
        /// </summary>
        /// <param name="header">文件头信息</param>
        /// <exception cref="ArgumentNullException">header为null</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteHeader(PcapFileHeader header)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileManager));
            }

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(header.ToBytes());
        }

        /// <summary>
        /// 读取PCAP文件头
        /// </summary>
        /// <returns>文件头信息</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="InvalidOperationException">文件流未初始化</exception>
        public PcapFileHeader ReadHeader()
        {
            return _IsDisposed ? throw new ObjectDisposedException(nameof(PcapFileManager))
                : _FileStream == null ? throw new InvalidOperationException("文件流未初始化")
                : StreamHelper.ReadStructure<PcapFileHeader>(_FileStream);
        }

        /// <summary>
        /// 写入索引条目
        /// </summary>
        /// <param name="entry">索引条目</param>
        /// <exception cref="ArgumentNullException">entry为null</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void WriteIndexEntry(PataFileIndexEntry entry)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileManager));
            }

            if (_BinaryWriter == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            _BinaryWriter.Write(entry.ToBytes());
        }

        /// <summary>
        /// 异步写入索引条目
        /// </summary>
        /// <param name="indexBytes">索引字节数组</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <exception cref="ArgumentNullException">indexBytes为null</exception>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public async Task WriteIndexEntryAsync(
            byte[] indexBytes,
            CancellationToken cancellationToken
        )
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileManager));
            }

            if (indexBytes == null)
            {
                throw new ArgumentNullException(nameof(indexBytes));
            }

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件流未初始化");
            }

            await _FileStream.WriteAsync(indexBytes, 0, indexBytes.Length, cancellationToken);
        }

        /// <summary>
        /// 刷新文件缓冲区
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void Flush()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileManager));
            }

            _BinaryWriter?.Flush();
        }

        /// <summary>
        /// 异步刷新文件缓冲区
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapFileManager));
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
