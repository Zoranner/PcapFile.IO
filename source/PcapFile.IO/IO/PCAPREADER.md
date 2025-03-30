# PCAP 文件读取详细完整逻辑流程

## 1. 初始化阶段

### 1.1 创建 PcapReader 实例
- 构造函数中初始化内部组件：
  ```csharp
  _PcapFileReader = new PcapFileReader();
  _PataFileReader = new PataFileReader();
  ```
- 初始化状态变量：
  ```csharp
  _IsDisposed = false;
  _CurrentFileId = 0;
  _FileEntries = new List<PataFileEntry>();
  _TimeIndices = new List<PataTimeIndexEntry>();
  _FileIndices = new Dictionary<string, List<PataFileIndexEntry>>();
  ```

### 1.2 调用 Open(filePath) 方法
- 参数检查：
  ```csharp
  if (string.IsNullOrEmpty(filePath))
  {
      throw new ArgumentException("文件路径不能为空", nameof(filePath));
  }
  
  if (!File.Exists(filePath))
  {
      throw new FileNotFoundException("PCAP文件不存在", filePath);
  }
  ```

- 打开PCAP工程文件：
  ```csharp
  _PcapFileReader.Open(filePath);
  ```

- 读取PCAP文件头：
  ```csharp
  var header = _PcapFileReader.ReadHeader();
  _Header = header;
  _IndexInterval = header.IndexInterval;
  PacketCount = header.TotalIndexCount;
  ```

- 加载文件条目表：
  ```csharp
  _FileEntries = _PcapFileReader.ReadAllFileEntries(header.FileCount);
  ```

- 加载时间索引表：
  ```csharp
  _TimeIndices = _PcapFileReader.ReadAllTimeIndices(header.TimeIndexOffset);
  ```

- 初始化内部数据结构：
  ```csharp
  if (_FileEntries.Count > 0)
  {
      var firstEntry = _FileEntries[0];
      var lastEntry = _FileEntries[_FileEntries.Count - 1];
      StartTime = DateTime.FromFileTimeUtc(firstEntry.StartTimestamp);
      EndTime = DateTime.FromFileTimeUtc(lastEntry.EndTimestamp);
  }
  ```

## 2. 数据读取阶段

### 2.1 顺序读取模式

#### 2.1.1 调用 ReadNextPacket() 方法
- 状态检查：
  ```csharp
  ThrowIfDisposed();
  if (!IsOpen) { throw new InvalidOperationException("文件未打开"); }
  ```

- 首次读取检查：
  ```csharp
  if (!_FirstPacketRead)
  {
      // 初始化第一个PATA文件
      if (_FileEntries.Count > 0)
      {
          OpenCurrentFile(0);
          _FirstPacketRead = true;
      }
      else
      {
          return null; // 没有数据文件
      }
  }
  ```

- 从当前PATA文件读取数据包：
  ```csharp
  var packet = _PataFileReader.ReadPacket();
  if (packet != null)
  {
      CurrentPosition++;
      return packet;
  }
  ```

- 如果当前文件读取完毕，尝试切换到下一个文件：
  ```csharp
  if (_CurrentFileId < _FileEntries.Count - 1)
  {
      // 关闭当前文件
      _PataFileReader.Close();
      
      // 打开下一个文件
      OpenCurrentFile(_CurrentFileId + 1);
      
      // 继续读取
      return ReadNextPacket();
  }
  ```

- 如果已经是最后一个文件，返回null表示读取结束：
  ```csharp
  return null;
  ```

#### 2.1.2 调用 ReadNextPacketAsync() 方法
- 异步版本实现与同步类似，但使用异步IO操作：
  ```csharp
  var packet = await _PataFileReader.ReadPacketAsync(cancellationToken);
  if (packet != null)
  {
      CurrentPosition++;
      return packet;
  }
  
  if (_CurrentFileId < _FileEntries.Count - 1)
  {
      // 关闭当前文件
      await _PataFileReader.CloseAsync();
      
      // 打开下一个文件
      await OpenCurrentFileAsync(_CurrentFileId + 1, cancellationToken);
      
      // 继续读取
      return await ReadNextPacketAsync(cancellationToken);
  }
  ```

### 2.2 随机访问模式

#### 2.2.1 调用 SeekToTime(timestamp) 方法
- 检查时间是否在文件范围内：
  ```csharp
  if (timestamp < StartTime || timestamp > EndTime)
  {
      return false;
  }
  ```

- 通过时间索引表找到目标文件：
  ```csharp
  var fileId = FindFileIdByTime(timestamp);
  ```

- 切换到目标文件：
  ```csharp
  if (_CurrentFileId != fileId)
  {
      _PataFileReader.Close();
      OpenCurrentFile(fileId);
  }
  ```

- 在目标文件中查找并定位到接近时间戳的索引位置：
  ```csharp
  var targetIndex = FindIndexInCurrentFile(timestamp);
  if (targetIndex >= 0)
  {
      _PataFileReader.Seek(targetIndex.FileOffset);
      CurrentPosition = CalculateGlobalPosition(fileId, targetIndex);
      return true;
  }
  ```

#### 2.2.2 调用 SeekToPosition(position) 方法
- 检查位置是否在有效范围内：
  ```csharp
  if (position < 0 || position >= PacketCount)
  {
      return false;
  }
  ```

- 计算目标文件ID和文件内偏移：
  ```csharp
  var (fileId, localIndex) = CalculateFileAndIndex(position);
  ```

- 切换到目标文件：
  ```csharp
  if (_CurrentFileId != fileId)
  {
      _PataFileReader.Close();
      OpenCurrentFile(fileId);
  }
  ```

- 在目标文件中定位到指定索引位置：
  ```csharp
  var indexEntry = _FileIndices[_FileEntries[fileId].RelativePath][localIndex];
  _PataFileReader.Seek(indexEntry.FileOffset);
  CurrentPosition = position;
  return true;
  ```

#### 2.2.3 调用 ReadPacketAt(position) 方法
- 组合SeekToPosition和ReadNextPacket的功能：
  ```csharp
  if (SeekToPosition(position))
  {
      return ReadNextPacket();
  }
  return null;
  ```

### 2.3 批量读取模式

#### 2.3.1 调用 ReadPackets(count) 方法
- 参数检查：
  ```csharp
  if (count <= 0)
  {
      throw new ArgumentOutOfRangeException(nameof(count), "数量必须大于0");
  }
  ```

- 读取指定数量的数据包：
  ```csharp
  var packets = new List<DataPacket>();
  for (int i = 0; i < count; i++)
  {
      var packet = ReadNextPacket();
      if (packet == null)
      {
          break;
      }
      packets.Add(packet);
  }
  return packets;
  ```

#### 2.3.2 调用 ReadPacketsAsync(count, cancellationToken) 方法
- 异步版本的批量读取：
  ```csharp
  var packets = new List<DataPacket>();
  for (int i = 0; i < count; i++)
  {
      var packet = await ReadNextPacketAsync(cancellationToken);
      if (packet == null || cancellationToken.IsCancellationRequested)
      {
          break;
      }
      packets.Add(packet);
  }
  return packets;
  ```

## 3. 索引加载与管理

### 3.1 索引懒加载策略
- 基本思路：不一次性加载所有文件的索引，而是在需要时按需加载
  ```csharp
  private List<PataFileIndexEntry> GetFileIndices(uint fileId)
  {
      var entry = _FileEntries[(int)fileId - 1];
      
      // 检查索引是否已加载
      if (_FileIndices.TryGetValue(entry.RelativePath, out var indices))
      {
          return indices;
      }
      
      // 按需加载索引
      indices = _PcapFileReader.ReadFileIndices(
          CalculateFileIndexOffset(fileId),
          entry.IndexCount
      );
      
      // 缓存索引
      _FileIndices[entry.RelativePath] = indices;
      
      return indices;
  }
  ```

### 3.2 索引搜索算法
- 二分查找时间戳最接近的索引：
  ```csharp
  private int FindNearestTimeIndex(long timestamp)
  {
      int low = 0, high = _TimeIndices.Count - 1;
      
      while (low <= high)
      {
          int mid = (low + high) / 2;
          var midTs = _TimeIndices[mid].Timestamp;
          
          if (midTs == timestamp)
          {
              return mid;
          }
          
          if (midTs < timestamp)
          {
              low = mid + 1;
          }
          else
          {
              high = mid - 1;
          }
      }
      
      // 返回最接近但不超过目标时间的索引
      return high >= 0 ? high : 0;
  }
  ```

### 3.3 文件切换管理
- 打开PATA数据文件：
  ```csharp
  private void OpenCurrentFile(int fileId)
  {
      if (fileId < 0 || fileId >= _FileEntries.Count)
      {
          throw new ArgumentOutOfRangeException(nameof(fileId));
      }
      
      _CurrentFileId = fileId;
      var entry = _FileEntries[fileId];
      var pataFilePath = PathHelper.GetFullPataFilePath(_PcapFileReader.FilePath, entry.RelativePath);
      
      _PataFileReader.Open(pataFilePath);
      
      // 预加载当前文件的索引
      GetFileIndices(entry.FileId);
  }
  ```

## 4. 文件关闭与资源释放

### 4.1 调用 Close() 方法
- 关闭所有打开的文件：
  ```csharp
  public void Close()
  {
      ThrowIfDisposed();
      
      try
      {
          // 关闭PATA数据文件
          _PataFileReader?.Close();
          
          // 关闭PCAP工程文件
          _PcapFileReader?.Close();
          
          // 清空状态
          ResetState();
      }
      catch (Exception ex)
      {
          throw new IOException($"关闭文件失败: {ex.Message}", ex);
      }
  }
  ```

### 4.2 调用 Dispose() 方法
- 释放所有资源：
  ```csharp
  public void Dispose()
  {
      if (!_IsDisposed)
      {
          try
          {
              if (IsOpen)
              {
                  Close();
              }
          }
          finally
          {
              DisposeStreams();
              _IsDisposed = true;
          }
      }
  }
  ```

### 4.3 重置状态 ResetState() 方法
- 重置所有内部状态：
  ```csharp
  private void ResetState()
  {
      _FileEntries.Clear();
      _TimeIndices.Clear();
      _FileIndices.Clear();
      
      _CurrentFileId = -1;
      CurrentPosition = 0;
      _FirstPacketRead = false;
  }
  ```

## 5. 错误处理

### 5.1 文件不存在或格式错误
```csharp
try
{
    // 打开文件操作
}
catch (FileNotFoundException ex)
{
    // 记录错误并向上传播
    throw new FileNotFoundException("PCAP文件不存在", ex);
}
catch (FormatException ex)
{
    // 处理文件格式错误
    throw new FormatException("PCAP文件格式无效", ex);
}
```

### 5.2 数据文件缺失
```csharp
private void OpenCurrentFile(int fileId)
{
    // ...
    var pataFilePath = PathHelper.GetFullPataFilePath(_PcapFileReader.FilePath, entry.RelativePath);
    
    if (!File.Exists(pataFilePath))
    {
        throw new FileNotFoundException($"PATA数据文件不存在: {entry.RelativePath}", pataFilePath);
    }
    
    try
    {
        _PataFileReader.Open(pataFilePath);
    }
    catch (Exception ex)
    {
        // 提供更详细的错误信息
        throw new IOException($"打开PATA数据文件失败: {entry.RelativePath}", ex);
    }
}
```

### 5.3 数据完整性错误
```csharp
private DataPacket ReadPacketWithIntegrityCheck()
{
    var packet = _PataFileReader.ReadPacket();
    
    if (packet != null)
    {
        // 验证校验和
        var calculatedChecksum = ChecksumCalculator.CalculateCrc32(packet.Data);
        if (calculatedChecksum != packet.Header.Checksum)
        {
            // 校验和不匹配时的处理策略（抛出异常或记录警告）
            if (_StrictIntegrityCheck)
            {
                throw new InvalidDataException("数据包校验和错误，数据可能已损坏");
            }
            else
            {
                // 记录警告但继续返回数据包
                _CorruptedPacketsCount++;
            }
        }
    }
    
    return packet;
}
```

## 6. 优化策略

### 6.1 索引缓存
```csharp
// 基于LRU策略的索引缓存
private readonly Dictionary<string, List<PataFileIndexEntry>> _IndexCache = new();
private readonly List<string> _IndexCacheOrder = new();
private const int MAX_CACHED_INDICES = 3;  // 最大缓存文件数

private List<PataFileIndexEntry> GetFileIndicesWithCache(uint fileId)
{
    var entry = _FileEntries[(int)fileId - 1];
    
    // 缓存命中
    if (_IndexCache.TryGetValue(entry.RelativePath, out var indices))
    {
        // 更新访问顺序
        _IndexCacheOrder.Remove(entry.RelativePath);
        _IndexCacheOrder.Add(entry.RelativePath);
        return indices;
    }
    
    // 缓存未命中，加载索引
    indices = _PcapFileReader.ReadFileIndices(
        CalculateFileIndexOffset(fileId),
        entry.IndexCount
    );
    
    // 管理缓存大小
    if (_IndexCache.Count >= MAX_CACHED_INDICES)
    {
        // 移除最久未使用的索引
        var oldest = _IndexCacheOrder[0];
        _IndexCache.Remove(oldest);
        _IndexCacheOrder.RemoveAt(0);
    }
    
    // 添加到缓存
    _IndexCache[entry.RelativePath] = indices;
    _IndexCacheOrder.Add(entry.RelativePath);
    
    return indices;
}
```

### 6.2 数据预读取
```csharp
private readonly Queue<DataPacket> _ReadAheadBuffer = new();
private const int READ_AHEAD_COUNT = 10;

private async Task FillReadAheadBufferAsync(CancellationToken cancellationToken)
{
    // 如果缓冲区已满，不需要预读
    if (_ReadAheadBuffer.Count >= READ_AHEAD_COUNT)
    {
        return;
    }
    
    // 记录当前位置
    var currentPosition = _PataFileReader.Position;
    
    // 预读取数据包
    while (_ReadAheadBuffer.Count < READ_AHEAD_COUNT && !cancellationToken.IsCancellationRequested)
    {
        var packet = await _PataFileReader.ReadPacketRawAsync(cancellationToken);
        if (packet == null)
        {
            if (_CurrentFileId < _FileEntries.Count - 1)
            {
                // 预读到下一个文件
                var nextFileId = _CurrentFileId + 1;
                var entry = _FileEntries[nextFileId];
                var pataFilePath = PathHelper.GetFullPataFilePath(
                    _PcapFileReader.FilePath,
                    entry.RelativePath
                );
                
                using var nextReader = new PataFileReader();
                await nextReader.OpenAsync(pataFilePath, cancellationToken);
                
                for (int i = 0; i < READ_AHEAD_COUNT - _ReadAheadBuffer.Count; i++)
                {
                    var nextPacket = await nextReader.ReadPacketAsync(cancellationToken);
                    if (nextPacket == null) break;
                    _ReadAheadBuffer.Enqueue(nextPacket);
                }
            }
            break;
        }
        _ReadAheadBuffer.Enqueue(packet);
    }
    
    // 恢复原始位置
    _PataFileReader.Seek(currentPosition);
}
```

### 6.3 并行文件操作
```csharp
private async Task<List<DataPacket>> ReadPacketsParallelAsync(
    DateTime startTime,
    DateTime endTime,
    CancellationToken cancellationToken
)
{
    var result = new List<DataPacket>();
    var startTimestamp = startTime.ToFileTimeUtc();
    var endTimestamp = endTime.ToFileTimeUtc();
    
    // 找出时间范围内的所有文件ID
    var fileIds = FindFilesInTimeRange(startTimestamp, endTimestamp);
    
    // 并行读取任务
    var tasks = new List<Task<List<DataPacket>>>();
    
    foreach (var fileId in fileIds)
    {
        tasks.Add(Task.Run(async () =>
        {
            var fileEntries = new List<DataPacket>();
            var entry = _FileEntries[fileId];
            var pataFilePath = PathHelper.GetFullPataFilePath(
                _PcapFileReader.FilePath,
                entry.RelativePath
            );
            
            using var reader = new PataFileReader();
            await reader.OpenAsync(pataFilePath, cancellationToken);
            
            // 获取该文件的索引
            var indices = GetFileIndicesWithCache((uint)fileId + 1);
            
            // 找出时间范围内的索引
            var relevantIndices = indices.Where(idx => 
                idx.Timestamp >= startTimestamp && idx.Timestamp <= endTimestamp
            ).ToList();
            
            // 读取相关数据包
            foreach (var idx in relevantIndices)
            {
                reader.Seek(idx.FileOffset);
                var packet = await reader.ReadPacketAsync(cancellationToken);
                if (packet != null)
                {
                    fileEntries.Add(packet);
                }
            }
            
            return fileEntries;
        }, cancellationToken));
    }
    
    // 等待所有任务完成并合并结果
    var results = await Task.WhenAll(tasks);
    foreach (var packets in results)
    {
        result.AddRange(packets);
    }
    
    // 按时间戳排序
    return result.OrderBy(p => p.Header.Timestamp).ToList();
}
```
