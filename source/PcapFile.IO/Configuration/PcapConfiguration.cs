using System;
using System.IO;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP库配置类
    /// </summary>
    public class PcapConfiguration
    {
        #region 默认值常量

        /// <summary>
        /// 默认每个文件最大数据包数量
        /// </summary>
        public const int DEFAULT_MAX_PACKETS_PER_FILE = 500;

        /// <summary>
        /// 默认缓冲区大小
        /// </summary>
        public const int DEFAULT_BUFFER_SIZE = 8192;

        /// <summary>
        /// 默认文件命名格式
        /// </summary>
        public const string DEFAULT_FILE_NAME_FORMAT = "yyMMdd_HHmmss_fffffff";

        /// <summary>
        /// 默认最大数据包大小
        /// </summary>
        public const int DEFAULT_MAX_PACKET_SIZE = 30 * 1024 * 1024; // 30MB

        /// <summary>
        /// 默认索引缓存大小
        /// </summary>
        public const int DEFAULT_INDEX_CACHE_SIZE = 1000;

        #endregion

        #region 私有字段

        private int _MaxPacketsPerFile = DEFAULT_MAX_PACKETS_PER_FILE;
        private int _BufferSize = DEFAULT_BUFFER_SIZE;
        private int _MaxPacketSize = DEFAULT_MAX_PACKET_SIZE;
        private int _IndexCacheSize = DEFAULT_INDEX_CACHE_SIZE;
        private string _FileNameFormat = DEFAULT_FILE_NAME_FORMAT;

        #endregion

        #region 属性

        /// <summary>
        /// 每个PCAP文件最大数据包数量
        /// </summary>
        public int MaxPacketsPerFile
        {
            get => _MaxPacketsPerFile;
            set
            {
                if (value <= 0)
                {
                    throw new PcapConfigurationException("每个文件最大数据包数量必须大于0");
                }

                _MaxPacketsPerFile = value;
            }
        }

        /// <summary>
        /// 缓冲区大小（字节）
        /// </summary>
        public int BufferSize
        {
            get => _BufferSize;
            set
            {
                if (value < 1024)
                {
                    throw new PcapConfigurationException("缓冲区大小不能小于1024字节");
                }

                if (value > PcapConstants.MAX_BUFFER_SIZE)
                {
                    throw new PcapConfigurationException(
                        $"缓冲区大小不能超过{PcapConstants.MAX_BUFFER_SIZE}字节"
                    );
                }

                _BufferSize = value;
            }
        }

        /// <summary>
        /// 最大数据包大小（字节）
        /// </summary>
        public int MaxPacketSize
        {
            get => _MaxPacketSize;
            set
            {
                if (value <= 0)
                {
                    throw new PcapConfigurationException("最大数据包大小必须大于0");
                }

                if (value > PcapConstants.MAX_PACKET_SIZE)
                {
                    throw new PcapConfigurationException(
                        $"最大数据包大小不能超过{PcapConstants.MAX_PACKET_SIZE}字节"
                    );
                }

                _MaxPacketSize = value;
            }
        }

        /// <summary>
        /// 文件命名格式
        /// </summary>
        public string FileNameFormat
        {
            get => _FileNameFormat;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new PcapConfigurationException("文件命名格式不能为空");
                }

                // 验证格式字符串是否有效
                try
                {
                    DateTime.Now.ToString(value);
                }
                catch (FormatException ex)
                {
                    throw new PcapConfigurationException($"无效的文件命名格式: {value}", ex);
                }

                _FileNameFormat = value;
            }
        }

        /// <summary>
        /// 是否启用自动刷新
        /// </summary>
        public bool AutoFlush { get; set; } = true;

        /// <summary>
        /// 是否启用数据验证
        /// </summary>
        public bool EnableValidation { get; set; } = true;

        /// <summary>
        /// 是否启用压缩
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// 索引缓存大小（条目数）
        /// </summary>
        public int IndexCacheSize
        {
            get => _IndexCacheSize;
            set
            {
                if (value <= 0)
                {
                    throw new PcapConfigurationException("索引缓存大小必须大于0");
                }

                _IndexCacheSize = value;
            }
        }

        /// <summary>
        /// 是否启用文件索引缓存
        /// </summary>
        public bool EnableIndexCache { get; set; } = true;

        /// <summary>
        /// 索引刷新间隔（毫秒）
        /// </summary>
        public int IndexFlushInterval { get; set; } = 5000;

        /// <summary>
        /// 读取超时时间（毫秒）
        /// </summary>
        public int ReadTimeout { get; set; } = 30000;

        /// <summary>
        /// 写入超时时间（毫秒）
        /// </summary>
        public int WriteTimeout { get; set; } = 30000;

        /// <summary>
        /// 临时目录路径
        /// </summary>
        public string TempDirectory { get; set; } = Path.GetTempPath();

        #endregion

        #region 构造函数

        /// <summary>
        /// 使用默认配置初始化
        /// </summary>
        public PcapConfiguration() { }

        /// <summary>
        /// 复制构造函数
        /// </summary>
        /// <param name="source">源配置</param>
        public PcapConfiguration(PcapConfiguration source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            MaxPacketsPerFile = source.MaxPacketsPerFile;
            BufferSize = source.BufferSize;
            MaxPacketSize = source.MaxPacketSize;
            FileNameFormat = source.FileNameFormat;
            AutoFlush = source.AutoFlush;
            EnableValidation = source.EnableValidation;
            EnableCompression = source.EnableCompression;
            IndexCacheSize = source.IndexCacheSize;
            EnableIndexCache = source.EnableIndexCache;
            IndexFlushInterval = source.IndexFlushInterval;
            ReadTimeout = source.ReadTimeout;
            WriteTimeout = source.WriteTimeout;
            TempDirectory = source.TempDirectory;
        }

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 获取默认配置
        /// </summary>
        /// <returns>默认配置实例</returns>
        public static PcapConfiguration Default => new PcapConfiguration();

        /// <summary>
        /// 获取高性能配置（适用于大量数据处理）
        /// </summary>
        /// <returns>高性能配置实例</returns>
        public static PcapConfiguration HighPerformance =>
            new PcapConfiguration
            {
                MaxPacketsPerFile = 2000,
                BufferSize = 64 * 1024, // 64KB
                AutoFlush = false,
                IndexCacheSize = 5000,
                EnableIndexCache = true,
            };

        /// <summary>
        /// 获取低内存配置（适用于内存受限环境）
        /// </summary>
        /// <returns>低内存配置实例</returns>
        public static PcapConfiguration LowMemory =>
            new PcapConfiguration
            {
                MaxPacketsPerFile = 100,
                BufferSize = 2048, // 2KB
                AutoFlush = true,
                IndexCacheSize = 100,
                EnableIndexCache = false,
            };

        /// <summary>
        /// 获取调试配置（启用所有验证和详细日志）
        /// </summary>
        /// <returns>调试配置实例</returns>
        public static PcapConfiguration Debug =>
            new PcapConfiguration
            {
                MaxPacketsPerFile = 50,
                BufferSize = 4096,
                AutoFlush = true,
                EnableValidation = true,
                IndexCacheSize = 50,
                EnableIndexCache = true,
            };

        #endregion

        #region 方法

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>验证结果</returns>
        public Result Validate()
        {
            try
            {
                // 验证数值范围
                if (MaxPacketsPerFile <= 0)
                {
                    return Result.Failure(
                        "每个文件最大数据包数量必须大于0",
                        PcapErrorCode.InvalidArgument
                    );
                }

                if (BufferSize < 1024)
                {
                    return Result.Failure(
                        "缓冲区大小不能小于1024字节",
                        PcapErrorCode.InvalidArgument
                    );
                }

                if (MaxPacketSize <= 0)
                {
                    return Result.Failure("最大数据包大小必须大于0", PcapErrorCode.InvalidArgument);
                }

                if (IndexCacheSize <= 0)
                {
                    return Result.Failure("索引缓存大小必须大于0", PcapErrorCode.InvalidArgument);
                }

                // 验证字符串格式
                if (string.IsNullOrWhiteSpace(FileNameFormat))
                {
                    return Result.Failure("文件命名格式不能为空", PcapErrorCode.InvalidArgument);
                }

                // 验证目录
                if (string.IsNullOrWhiteSpace(TempDirectory) || !Directory.Exists(TempDirectory))
                {
                    return Result.Failure("临时目录不存在", PcapErrorCode.DirectoryNotFound);
                }

                // 验证时间格式
                try
                {
                    DateTime.Now.ToString(FileNameFormat);
                }
                catch (FormatException)
                {
                    return Result.Failure(
                        $"无效的文件命名格式: {FileNameFormat}",
                        PcapErrorCode.InvalidFormat
                    );
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"配置验证失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        /// <returns>配置的副本</returns>
        public PcapConfiguration Clone()
        {
            return new PcapConfiguration(this);
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void Reset()
        {
            MaxPacketsPerFile = DEFAULT_MAX_PACKETS_PER_FILE;
            BufferSize = DEFAULT_BUFFER_SIZE;
            MaxPacketSize = DEFAULT_MAX_PACKET_SIZE;
            FileNameFormat = DEFAULT_FILE_NAME_FORMAT;
            AutoFlush = true;
            EnableValidation = true;
            EnableCompression = false;
            IndexCacheSize = DEFAULT_INDEX_CACHE_SIZE;
            EnableIndexCache = true;
            IndexFlushInterval = 5000;
            ReadTimeout = 30000;
            WriteTimeout = 30000;
            TempDirectory = Path.GetTempPath();
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 返回配置的字符串表示
        /// </summary>
        /// <returns>配置信息</returns>
        public override string ToString()
        {
            return $"PcapConfiguration {{ "
                + $"MaxPacketsPerFile: {MaxPacketsPerFile}, "
                + $"BufferSize: {BufferSize}, "
                + $"MaxPacketSize: {MaxPacketSize}, "
                + $"FileNameFormat: '{FileNameFormat}', "
                + $"AutoFlush: {AutoFlush}, "
                + $"EnableValidation: {EnableValidation}, "
                + $"EnableIndexCache: {EnableIndexCache} "
                + $"}}";
        }

        #endregion
    }
}
