using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP文件读取器，提供读取PCAP文件的功能
    /// </summary>
    /// <remarks>
    /// 该类负责管理数据文件的读取操作
    /// </remarks>
    public class PcapReader : IPcapReader
    {
        #region 私有字段

        /// <summary>
        /// PCAP文件读取器，负责管理PCAP数据文件的打开、读取和关闭操作
        /// </summary>
        private readonly PcapFileReader _PcapFileReader;

        /// <summary>
        /// 标记对象是否已被释放
        /// </summary>
        private bool _IsDisposed;

        /// <summary>
        /// 基础目录路径
        /// </summary>
        private string _BaseDirectory;

        /// <summary>
        /// 当前文件索引
        /// </summary>
        private int _CurrentFileIndex;

        /// <summary>
        /// 工程目录中的所有PCAP文件列表
        /// </summary>
        private List<string> _ProjectFiles;

        /// <summary>
        /// 所有文件的总数据包数量
        /// </summary>
        private long _TotalPacketCount;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PCAP文件读取器
        /// </summary>
        public PcapReader()
        {
            _PcapFileReader = new PcapFileReader();
            _IsDisposed = false;
            _CurrentFileIndex = -1;
            _ProjectFiles = new List<string>();
        }

        #endregion

        #region 属性

        /// <inheritdoc />
        public long PacketCount { get; private set; }

        /// <inheritdoc />
        public long FileSize
        {
            get
            {
                ThrowIfDisposed();
                return _PcapFileReader.FileSize;
            }
        }

        /// <inheritdoc />
        public string FilePath => _PcapFileReader.FilePath;

        /// <inheritdoc />
        public bool IsOpen => _PcapFileReader.IsOpen;

        /// <inheritdoc />
        public string ProjectName { get; private set; }

        /// <inheritdoc />
        public string InputDirectory { get; private set; }

        /// <inheritdoc />
        public long Position => _PcapFileReader.Position;

        /// <inheritdoc />
        public bool EndOfFile =>
            _PcapFileReader.EndOfFile && _CurrentFileIndex >= _ProjectFiles.Count - 1;

        /// <inheritdoc />
        public long TotalPacketCount
        {
            get
            {
                if (_TotalPacketCount == 0 && _ProjectFiles.Count > 0)
                {
                    CalculateTotalPacketCount();
                }
                return _TotalPacketCount;
            }
        }

        #endregion

        #region 核心方法

        /// <inheritdoc />
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
                ProjectName = projectName;

                // 检查基础目录
                if (!Directory.Exists(baseDirectory))
                {
                    throw new DirectoryNotFoundException($"基础目录不存在: {baseDirectory}");
                }

                // 检查数据工程目录
                InputDirectory = Path.Combine(baseDirectory, projectName);
                if (!Directory.Exists(InputDirectory))
                {
                    throw new DirectoryNotFoundException($"数据工程目录不存在: {InputDirectory}");
                }

                // 扫描工程目录中的PCAP文件
                ScanProjectFiles();

                if (_ProjectFiles.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"在工程目录中未找到任何PCAP文件: {InputDirectory}"
                    );
                }

                // 打开第一个文件
                ResetState();
                return OpenNextFile();
            }
            catch (Exception ex)
            {
                Close();
                throw new IOException($"打开数据工程失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public bool OpenFile(string filePath)
        {
            ThrowIfDisposed();

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("文件路径不能为空", nameof(filePath));
                }

                // 关闭现有文件
                Close();

                // 打开指定文件
                _PcapFileReader.Open(filePath);

                // 设置为单文件模式
                _ProjectFiles = new List<string> { filePath };
                _CurrentFileIndex = 0;
                PacketCount = 0;
                ProjectName = Path.GetFileNameWithoutExtension(filePath);
                InputDirectory = Path.GetDirectoryName(filePath);

                return true;
            }
            catch (Exception ex)
            {
                Close();
                throw new IOException($"打开PCAP文件失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            ThrowIfDisposed();

            // 关闭数据文件
            _PcapFileReader.Close();

            // 重置状态
            ResetState();
        }

        /// <inheritdoc />
        public DataPacket ReadNextPacket()
        {
            ThrowIfDisposed();

            try
            {
                // 尝试从当前文件读取数据包
                var packet = _PcapFileReader.ReadPacket();

                // 如果当前文件已结束，尝试打开下一个文件
                if (packet == null && !EndOfFile)
                {
                    if (OpenNextFile())
                    {
                        packet = _PcapFileReader.ReadPacket();
                    }
                }

                if (packet != null)
                {
                    PacketCount++;
                }

                return packet;
            }
            catch (Exception ex)
            {
                throw new IOException($"读取数据包失败: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public IEnumerable<DataPacket> ReadPackets(int count)
        {
            ThrowIfDisposed();

            if (count <= 0)
            {
                yield break;
            }

            for (int i = 0; i < count; i++)
            {
                var packet = ReadNextPacket();
                if (packet == null)
                {
                    yield break;
                }
                yield return packet;
            }
        }

        /// <inheritdoc />
        public IEnumerable<DataPacket> ReadAllPackets()
        {
            ThrowIfDisposed();

            DataPacket packet;
            while ((packet = ReadNextPacket()) != null)
            {
                yield return packet;
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            ThrowIfDisposed();

            if (_ProjectFiles.Count == 0)
            {
                throw new InvalidOperationException("没有可用的文件");
            }

            // 重置到第一个文件的开始位置
            _CurrentFileIndex = -1;
            PacketCount = 0;
            OpenNextFile();
        }

        /// <inheritdoc />
        public bool SeekToPacket(long packetIndex)
        {
            ThrowIfDisposed();

            if (packetIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packetIndex), "数据包索引不能为负数");
            }

            // 重置到开始位置
            Reset();

            // 跳过指定数量的数据包
            for (long i = 0; i < packetIndex; i++)
            {
                var packet = ReadNextPacket();
                if (packet == null)
                {
                    return false; // 索引超出范围
                }
            }

            return true;
        }

        /// <inheritdoc />
        public IEnumerable<string> GetProjectFiles()
        {
            ThrowIfDisposed();
            return _ProjectFiles.AsReadOnly();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 重置状态
        /// </summary>
        private void ResetState()
        {
            PacketCount = 0;
            _CurrentFileIndex = -1;
            _TotalPacketCount = 0;
            _ProjectFiles.Clear();
        }

        /// <summary>
        /// 扫描工程目录中的PCAP文件
        /// </summary>
        private void ScanProjectFiles()
        {
            _ProjectFiles.Clear();

            if (Directory.Exists(InputDirectory))
            {
                var files = Directory
                    .GetFiles(InputDirectory, "*.pcap", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f)
                    .ToList();

                _ProjectFiles.AddRange(files);
            }
        }

        /// <summary>
        /// 打开下一个文件
        /// </summary>
        private bool OpenNextFile()
        {
            _CurrentFileIndex++;

            if (_CurrentFileIndex >= _ProjectFiles.Count)
            {
                return false; // 没有更多文件
            }

            try
            {
                var filePath = _ProjectFiles[_CurrentFileIndex];
                _PcapFileReader.Open(filePath);
                return true;
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"打开文件失败: {_ProjectFiles[_CurrentFileIndex]}, 错误: {ex.Message}",
                    ex
                );
            }
        }

        /// <summary>
        /// 计算所有文件的总数据包数量
        /// </summary>
        private void CalculateTotalPacketCount()
        {
            _TotalPacketCount = 0;

            // 保存当前状态
            var currentFileIndex = _CurrentFileIndex;
            var currentPacketCount = PacketCount;
            var currentFilePath = FilePath;

            try
            {
                // 遍历所有文件计算总数据包数量
                using var tempReader = new PcapFileReader();
                foreach (var filePath in _ProjectFiles)
                {
                    try
                    {
                        tempReader.Open(filePath);
                        while (tempReader.ReadPacket() != null)
                        {
                            _TotalPacketCount++;
                        }
                        tempReader.Close();
                    }
                    catch (Exception ex)
                    {
                        // 忽略单个文件的错误，继续处理其他文件
                        System.Diagnostics.Debug.WriteLine(
                            $"计算文件 {filePath} 的数据包数量时发生错误: {ex.Message}"
                        );
                    }
                }
            }
            finally
            {
                // 恢复当前状态
                if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
                {
                    try
                    {
                        _PcapFileReader.Open(currentFilePath);
                        // 重新定位到之前的位置
                        for (long i = 0; i < _PcapFileReader.PacketCount; i++)
                        {
                            _PcapFileReader.ReadPacket();
                        }
                    }
                    catch
                    {
                        // 如果恢复失败，重置到开始位置
                        Reset();
                    }
                }
            }
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
                    _PcapFileReader?.Close();
                    _PcapFileReader?.Dispose();
                }

                _IsDisposed = true;
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PcapReader));
            }
        }

        #endregion
    }
}
