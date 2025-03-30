using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Utils;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PATA文件读取器，负责管理PATA数据文件的打开、读取和关闭操作
    /// </summary>
    /// <remarks>
    /// 该类封装了PATA数据文件的底层操作，提供读取数据包的功能。
    /// </remarks>
    internal class PataFileReader : IDisposable
    {
        #region 字段

        private FileStream _FileStream;
        private BinaryReader _BinaryReader;
        private bool _IsDisposed;
        private readonly byte[] _ReadBuffer;
        private int _ReadBufferPosition;
        private int _ReadBufferLength;

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
        /// 获取文件是否已打开且未释放
        /// </summary>
        public bool IsOpen => _FileStream != null && !_IsDisposed;

        /// <summary>
        /// 获取文件头
        /// </summary>
        public PataFileHeader Header { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PATA文件读取器
        /// </summary>
        /// <param name="bufferSize">读取缓冲区大小</param>
        public PataFileReader(int bufferSize = 4096)
        {
            _ReadBuffer = new byte[bufferSize];
            _ReadBufferPosition = 0;
            _ReadBufferLength = 0;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 打开PATA文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <exception cref="ArgumentException">文件路径为空或无效</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">打开文件时发生错误</exception>
        public void Open(string filePath)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PATA文件不存在", filePath);
            }

            try
            {
                DisposeStreams();

                FilePath = filePath;
                _FileStream = StreamHelper.CreateFileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                _BinaryReader = StreamHelper.CreateBinaryReader(_FileStream);

                // 读取文件头
                Header = StreamHelper.ReadStructure<PataFileHeader>(_FileStream);

                // 验证魔术数
                if (Header.MagicNumber != Configuration.FileVersionConfig.PATA_MAGIC_NUMBER)
                {
                    throw new FormatException("无效的PATA文件格式，魔术数不匹配");
                }

                // 重置缓冲区
                _ReadBufferPosition = 0;
                _ReadBufferLength = 0;
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"打开PATA文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步打开PATA文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <exception cref="ArgumentException">文件路径为空或无效</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">打开文件时发生错误</exception>
        public async Task OpenAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PATA文件不存在", filePath);
            }

            try
            {
                DisposeStreams();

                FilePath = filePath;
                _FileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    true
                );
                _BinaryReader = new BinaryReader(_FileStream);

                // 读取文件头
                var headerBuffer = new byte[PataFileHeader.HEADER_SIZE];
                await _FileStream.ReadAsync(
                    headerBuffer,
                    0,
                    headerBuffer.Length,
                    cancellationToken
                );
                Header = PataFileHeader.FromBytes(headerBuffer);

                // 验证魔术数
                if (Header.MagicNumber != Configuration.FileVersionConfig.PATA_MAGIC_NUMBER)
                {
                    throw new FormatException("无效的PATA文件格式，魔术数不匹配");
                }

                // 重置缓冲区
                _ReadBufferPosition = 0;
                _ReadBufferLength = 0;
            }
            catch (Exception ex)
            {
                DisposeStreams();
                throw new IOException($"打开PATA文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 设置文件位置
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">设置文件位置失败</exception>
        public void Seek(long position)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                // 检查是否可以在缓冲区内定位
                long bufferStartPosition = _FileStream.Position - _ReadBufferLength;
                long bufferEndPosition = _FileStream.Position;

                if (position >= bufferStartPosition && position < bufferEndPosition)
                {
                    // 目标位置在缓冲区内，调整缓冲区位置
                    _ReadBufferPosition = (int)(position - bufferStartPosition);
                }
                else
                {
                    // 重新定位文件流，清空缓冲区
                    _FileStream.Position = position;
                    _ReadBufferPosition = 0;
                    _ReadBufferLength = 0;
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"设置文件位置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取数据包
        /// </summary>
        /// <returns>数据包，如果到达文件末尾则返回null</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">读取数据包失败</exception>
        public DataPacket ReadPacket()
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                // 检查是否到达文件末尾
                if (
                    _FileStream.Position >= _FileStream.Length
                    && _ReadBufferPosition >= _ReadBufferLength
                )
                {
                    return null;
                }

                // 读取数据包头
                var headerBytes = new byte[DataPacketHeader.HEADER_SIZE];
                if (!ReadFromBufferOrFile(headerBytes, 0, headerBytes.Length))
                {
                    return null;
                }

                var header = DataPacketHeader.FromBytes(headerBytes);

                // 读取数据包内容
                var data = new byte[header.PacketLength];
                if (!ReadFromBufferOrFile(data, 0, data.Length))
                {
                    throw new IOException("读取数据包内容失败");
                }

                // 创建数据包对象
                return new DataPacket(header, data);
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            catch (Exception ex)
            {
                throw new IOException($"读取数据包失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步读取数据包
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据包，如果到达文件末尾则返回null</returns>
        /// <exception cref="InvalidOperationException">文件未打开</exception>
        /// <exception cref="IOException">读取数据包失败</exception>
        public async Task<DataPacket> ReadPacketAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_FileStream == null)
            {
                throw new InvalidOperationException("文件未打开");
            }

            try
            {
                // 检查是否到达文件末尾
                if (
                    _FileStream.Position >= _FileStream.Length
                    && _ReadBufferPosition >= _ReadBufferLength
                )
                {
                    return null;
                }

                // 读取数据包头
                var headerBytes = new byte[DataPacketHeader.HEADER_SIZE];
                if (
                    !await ReadFromBufferOrFileAsync(
                        headerBytes,
                        0,
                        headerBytes.Length,
                        cancellationToken
                    )
                )
                {
                    return null;
                }

                var header = DataPacketHeader.FromBytes(headerBytes);

                // 读取数据包内容
                var data = new byte[header.PacketLength];
                if (!await ReadFromBufferOrFileAsync(data, 0, data.Length, cancellationToken))
                {
                    throw new IOException("读取数据包内容失败");
                }

                // 创建数据包对象
                return new DataPacket(header, data);
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new IOException($"读取数据包失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 关闭文件
        /// </summary>
        public void Close()
        {
            DisposeStreams();
            FilePath = null;
        }

        /// <summary>
        /// 异步关闭文件
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            Close();
            return Task.CompletedTask;
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
        /// 释放流资源
        /// </summary>
        private void DisposeStreams()
        {
            if (_BinaryReader != null)
            {
                _BinaryReader.Dispose();
                _BinaryReader = null;
            }

            if (_FileStream != null)
            {
                _FileStream.Dispose();
                _FileStream = null;
            }
        }

        /// <summary>
        /// 从缓冲区或文件中读取数据
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">目标缓冲区的偏移量</param>
        /// <param name="count">要读取的字节数</param>
        /// <returns>是否成功读取所有字节</returns>
        private bool ReadFromBufferOrFile(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;

            // 先从缓冲区读取
            if (_ReadBufferPosition < _ReadBufferLength)
            {
                int bytesAvailable = _ReadBufferLength - _ReadBufferPosition;
                int bytesToCopy = Math.Min(bytesAvailable, count);

                Array.Copy(_ReadBuffer, _ReadBufferPosition, buffer, offset, bytesToCopy);

                _ReadBufferPosition += bytesToCopy;
                bytesRead += bytesToCopy;
            }

            // 如果还需要更多数据，从文件读取
            if (bytesRead < count)
            {
                // 大数据直接读取，不使用缓冲区
                if (count - bytesRead > _ReadBuffer.Length)
                {
                    int read = _FileStream.Read(buffer, offset + bytesRead, count - bytesRead);
                    if (read == 0)
                    {
                        return false; // 文件结束
                    }

                    bytesRead += read;
                }
                else
                {
                    // 填充缓冲区
                    _ReadBufferLength = _FileStream.Read(_ReadBuffer, 0, _ReadBuffer.Length);
                    if (_ReadBufferLength == 0)
                    {
                        return false; // 文件结束
                    }

                    _ReadBufferPosition = 0;

                    // 从缓冲区复制数据
                    int bytesToCopy = Math.Min(_ReadBufferLength, count - bytesRead);
                    Array.Copy(_ReadBuffer, 0, buffer, offset + bytesRead, bytesToCopy);

                    _ReadBufferPosition += bytesToCopy;
                    bytesRead += bytesToCopy;
                }
            }

            return bytesRead == count;
        }

        /// <summary>
        /// 从缓冲区或文件中异步读取数据
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">目标缓冲区的偏移量</param>
        /// <param name="count">要读取的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功读取所有字节</returns>
        private async Task<bool> ReadFromBufferOrFileAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            int bytesRead = 0;

            // 先从缓冲区读取
            if (_ReadBufferPosition < _ReadBufferLength)
            {
                int bytesAvailable = _ReadBufferLength - _ReadBufferPosition;
                int bytesToCopy = Math.Min(bytesAvailable, count);

                Array.Copy(_ReadBuffer, _ReadBufferPosition, buffer, offset, bytesToCopy);

                _ReadBufferPosition += bytesToCopy;
                bytesRead += bytesToCopy;
            }

            // 如果还需要更多数据，从文件读取
            if (bytesRead < count)
            {
                // 大数据直接读取，不使用缓冲区
                if (count - bytesRead > _ReadBuffer.Length)
                {
                    int read = await _FileStream.ReadAsync(
                        buffer,
                        offset + bytesRead,
                        count - bytesRead,
                        cancellationToken
                    );

                    if (read == 0)
                    {
                        return false; // 文件结束
                    }

                    bytesRead += read;
                }
                else
                {
                    // 填充缓冲区
                    _ReadBufferLength = await _FileStream.ReadAsync(
                        _ReadBuffer,
                        0,
                        _ReadBuffer.Length,
                        cancellationToken
                    );

                    if (_ReadBufferLength == 0)
                    {
                        return false; // 文件结束
                    }

                    _ReadBufferPosition = 0;

                    // 从缓冲区复制数据
                    int bytesToCopy = Math.Min(_ReadBufferLength, count - bytesRead);
                    Array.Copy(_ReadBuffer, 0, buffer, offset + bytesRead, bytesToCopy);

                    _ReadBufferPosition += bytesToCopy;
                    bytesRead += bytesToCopy;
                }
            }

            return bytesRead == count;
        }

        /// <summary>
        /// 检查对象是否已释放，如果已释放则抛出异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PataFileReader));
            }
        }

        #endregion
    }
}
