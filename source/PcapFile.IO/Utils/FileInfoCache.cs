using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 文件信息缓存项
    /// </summary>
    public class FileInfoCacheItem
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 文件最后修改时间
        /// </summary>
        public DateTime LastWriteTime { get; set; }

        /// <summary>
        /// 数据包数量
        /// </summary>
        public long PacketCount { get; set; }

        /// <summary>
        /// 缓存时间
        /// </summary>
        public DateTime CacheTime { get; set; }

        /// <summary>
        /// 是否有效（用于判断文件是否已修改）
        /// </summary>
        public bool IsValid(FileInfo fileInfo)
        {
            return fileInfo.Exists
                && fileInfo.Length == FileSize
                && fileInfo.LastWriteTime == LastWriteTime;
        }

        /// <summary>
        /// 从文件信息创建缓存项
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <param name="packetCount">数据包数量</param>
        /// <returns>缓存项</returns>
        public static FileInfoCacheItem FromFileInfo(FileInfo fileInfo, long packetCount)
        {
            return new FileInfoCacheItem
            {
                FilePath = fileInfo.FullName,
                FileSize = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime,
                PacketCount = packetCount,
                CacheTime = DateTime.Now,
            };
        }
    }

    /// <summary>
    /// 文件信息缓存，用于缓存文件的数据包数量等信息
    /// </summary>
    public class FileInfoCache
    {
        private readonly ConcurrentDictionary<string, FileInfoCacheItem> _Cache;
        private readonly PcapConfiguration _Configuration;
        private readonly object _CleanupLock = new object();
        private DateTime _LastCleanup = DateTime.Now;

        /// <summary>
        /// 缓存过期时间（分钟）
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 最大缓存条目数
        /// </summary>
        public int MaxCacheEntries { get; set; } = 1000;

        /// <summary>
        /// 自动清理间隔（分钟）
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="configuration">配置信息</param>
        public FileInfoCache(PcapConfiguration configuration = null)
        {
            _Cache = new ConcurrentDictionary<string, FileInfoCacheItem>();
            _Configuration = configuration ?? PcapConfiguration.Default;

            if (_Configuration.EnableIndexCache)
            {
                MaxCacheEntries = _Configuration.IndexCacheSize;
            }
        }

        /// <summary>
        /// 获取文件的数据包数量（优先从缓存获取）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>数据包数量，如果无法获取返回-1</returns>
        public PcapResult<long> GetPacketCount(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return PcapResult<long>.Failure("文件路径不能为空", PcapErrorCode.InvalidArgument);
                }

                if (!File.Exists(filePath))
                {
                    return PcapResult<long>.Failure(
                        $"文件不存在: {filePath}",
                        PcapErrorCode.FileNotFound
                    );
                }

                var fileInfo = new FileInfo(filePath);
                var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();

                // 尝试从缓存获取
                if (
                    _Configuration.EnableIndexCache
                    && _Cache.TryGetValue(normalizedPath, out var cachedItem)
                )
                {
                    if (cachedItem.IsValid(fileInfo))
                    {
                        return PcapResult<long>.Success(cachedItem.PacketCount);
                    }
                    else
                    {
                        // 缓存失效，移除过期项
                        _Cache.TryRemove(normalizedPath, out _);
                    }
                }

                // 缓存未命中或失效，重新计算
                var countResult = FileInfoCache.CalculatePacketCountFromFile(filePath);
                if (countResult.IsFailure)
                {
                    return countResult;
                }

                // 更新缓存
                if (_Configuration.EnableIndexCache)
                {
                    var cacheItem = FileInfoCacheItem.FromFileInfo(fileInfo, countResult.Value);
                    _Cache.AddOrUpdate(normalizedPath, cacheItem, (key, oldValue) => cacheItem);

                    // 定期清理缓存
                    PerformPeriodicCleanup();
                }

                return countResult;
            }
            catch (Exception ex)
            {
                return PcapResult<long>.Failure($"获取文件数据包数量失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 批量获取多个文件的数据包数量
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <returns>文件路径和数据包数量的字典</returns>
        public PcapResult<Dictionary<string, long>> GetPacketCounts(IEnumerable<string> filePaths)
        {
            try
            {
                var results = new Dictionary<string, long>();
                var errors = new List<string>();

                foreach (var filePath in filePaths)
                {
                    var result = GetPacketCount(filePath);
                    if (result.IsSuccess)
                    {
                        results[filePath] = result.Value;
                    }
                    else
                    {
                        errors.Add($"{filePath}: {result.ErrorMessage}");
                    }
                }

                if (errors.Any())
                {
                    var errorMessage = $"部分文件处理失败: {string.Join("; ", errors)}";
                    return PcapResult<Dictionary<string, long>>.Failure(errorMessage);
                }

                return PcapResult<Dictionary<string, long>>.Success(results);
            }
            catch (Exception ex)
            {
                return PcapResult<Dictionary<string, long>>.Failure(
                    $"批量获取数据包数量失败: {ex.Message}",
                    ex
                );
            }
        }

        /// <summary>
        /// 计算文件中的数据包数量
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>数据包数量</returns>
        private static PcapResult<long> CalculatePacketCountFromFile(string filePath)
        {
            try
            {
                using var reader = new PcapFileReader();
                reader.Open(filePath);

                if (!reader.IsOpen)
                {
                    return PcapResult<long>.Failure(
                        $"无法打开文件: {filePath}",
                        PcapErrorCode.InvalidFormat
                    );
                }

                long count = 0;
                while (reader.ReadPacket() != null)
                {
                    count++;

                    // 避免无限循环的安全检查
                    if (count > int.MaxValue)
                    {
                        return PcapResult<long>.Failure(
                            $"文件数据包数量超出限制: {filePath}",
                            PcapErrorCode.BufferOverflow
                        );
                    }
                }

                return PcapResult<long>.Success(count);
            }
            catch (Exception ex)
            {
                return PcapResult<long>.Failure($"计算文件数据包数量时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 使缓存中的文件项失效
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void InvalidateFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            _Cache.TryRemove(normalizedPath, out _);
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void Clear()
        {
            _Cache.Clear();
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计</returns>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                TotalEntries = _Cache.Count,
                MaxEntries = MaxCacheEntries,
                ExpiredEntries = _Cache.Values.Count(item =>
                    DateTime.Now - item.CacheTime > CacheExpiration
                ),
                LastCleanupTime = _LastCleanup,
            };
        }

        /// <summary>
        /// 执行定期清理
        /// </summary>
        private void PerformPeriodicCleanup()
        {
            if (DateTime.Now - _LastCleanup < CleanupInterval)
            {
                return;
            }

            lock (_CleanupLock)
            {
                if (DateTime.Now - _LastCleanup < CleanupInterval)
                {
                    return;
                }

                CleanupExpiredEntries();
                _LastCleanup = DateTime.Now;
            }
        }

        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        private void CleanupExpiredEntries()
        {
            var now = DateTime.Now;
            var expiredKeys = _Cache
                .Where(kvp => now - kvp.Value.CacheTime > CacheExpiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _Cache.TryRemove(key, out _);
            }

            // 如果缓存仍然太大，移除最旧的条目
            if (_Cache.Count > MaxCacheEntries)
            {
                var oldestKeys = _Cache
                    .OrderBy(kvp => kvp.Value.CacheTime)
                    .Take(_Cache.Count - MaxCacheEntries)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestKeys)
                {
                    _Cache.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// 强制执行清理
        /// </summary>
        public void ForceCleanup()
        {
            lock (_CleanupLock)
            {
                CleanupExpiredEntries();
                _LastCleanup = DateTime.Now;
            }
        }
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// 总条目数
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// 最大条目数
        /// </summary>
        public int MaxEntries { get; set; }

        /// <summary>
        /// 过期条目数
        /// </summary>
        public int ExpiredEntries { get; set; }

        /// <summary>
        /// 上次清理时间
        /// </summary>
        public DateTime LastCleanupTime { get; set; }

        /// <summary>
        /// 缓存使用率
        /// </summary>
        public double UsagePercentage =>
            MaxEntries > 0 ? (double)TotalEntries / MaxEntries * 100 : 0;

        /// <summary>
        /// 返回统计信息的字符串表示
        /// </summary>
        /// <returns>统计信息</returns>
        public override string ToString()
        {
            return $"Cache Statistics: {TotalEntries}/{MaxEntries} entries ({UsagePercentage:F1}%), "
                + $"{ExpiredEntries} expired, last cleanup: {LastCleanupTime:HH:mm:ss}";
        }
    }
}
