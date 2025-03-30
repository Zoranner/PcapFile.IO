# PCAP 文件写入详细完整逻辑流程

## 1. 初始化阶段

### 1.1 创建 PcapWriter 实例
- 构造函数中初始化内部组件：
  ```csharp
  _PcapFileWriter = new PcapFileWriter();
  _PataFileWriter = new PataFileWriter();
  ```
- 初始化状态变量：
  ```csharp
  _IsDisposed = false;
  _TotalSize = 0;
  ```

### 1.2 调用 Create(filePath) 方法
- 参数检查：
  ```csharp
  if (string.IsNullOrEmpty(filePath))
  {
      throw new ArgumentException("文件路径不能为空", nameof(filePath));
  }
  ```

- 创建目录结构：
  ```csharp
  var directory = Path.GetDirectoryName(filePath);
  if (!string.IsNullOrEmpty(directory))
  {
      Directory.CreateDirectory(directory);
      var pataDirectory = PathHelper.GetPataDirectoryPath(filePath);
      Directory.CreateDirectory(pataDirectory);
  }
  ```

- 创建初始 PCAP 工程文件：
  ```csharp
  _PcapFileWriter.Create(filePath);
  ```

- 初始化 PCAP 文件头：
  ```csharp
  var header = PcapFileHeader.Create(0, 0); // FileCount=0, TotalIndexCount=0
  header.MagicNumber = FileVersionConfig.PCAP_MAGIC_NUMBER;
  header.MajorVersion = FileVersionConfig.MAJOR_VERSION;
  header.MinorVersion = FileVersionConfig.MINOR_VERSION;
  header.FileEntryOffset = PcapFileHeader.HEADER_SIZE;
  header.IndexInterval = FileVersionConfig.DEFAULT_INDEX_INTERVAL;
  header.TimeIndexOffset = 0; // 初始为0，后续更新
  header.Checksum = 0; // 初始为0，关闭时计算
  _PcapFileWriter.WriteHeader(header);
  ```

- 初始化内存数据结构和状态：
  ```csharp
  ResetState(); // 重置所有内部状态
  AutoFlush = true;
  ```

## 2. 数据写入阶段

### 2.1 调用 WritePacket(packet) 方法
- 参数和状态检查：
  ```csharp
  ThrowIfDisposed();
  if (!IsOpen) { throw new InvalidOperationException("文件未打开"); }
  if (packet == null) { throw new ArgumentNullException(nameof(packet)); }
  ```

### 2.2 PATA 文件管理
- 首次写入数据包检查：
  ```csharp
  if (!_FirstPacketWritten)
  {
      // 创建第一个 PATA 文件，使用第一个数据包的时间戳命名
      string pataFilePath = _PataFileWriter.CreateDataFile(packet.Timestamp);
      
      // 创建新的文件条目
      _CurrentFileId = 1;
      var fileEntry = PataFileEntry.Create(
          (uint)_CurrentFileId,
          Path.GetFileName(pataFilePath),
          packet.Header.Timestamp,
          packet.Header.Timestamp,
          0
      );
      
      // 添加到内存中的文件条目列表
      _FileEntries.Add(fileEntry);
      _FirstPacketWritten = true;
      
      // 创建索引字典
      _FileIndices[fileEntry.RelativePath] = new List<PataFileIndexEntry>();
  }
  ```

- 检查是否需要创建新 PATA 文件：
  ```csharp
  if (_PataFileWriter.CurrentPacketCount >= _PataFileWriter.MaxPacketsPerFile)
  {
      // 获取当前文件条目
      var currentEntry = _FileEntries[_CurrentFileId - 1];
      
      // 更新当前文件的结束时间戳
      _FileEntries[_CurrentFileId - 1] = PataFileEntry.Create(
          currentEntry.FileId,
          currentEntry.RelativePath,
          currentEntry.StartTimestamp,
          _LastPacketTimestamp,
          (uint)_FileIndices[currentEntry.RelativePath].Count
      );
      
      // 先关闭当前PATA文件，确保小周期闭环
      _PataFileWriter.Flush();
      _PataFileWriter.Close();
      
      // 创建新文件，使用当前数据包的时间戳命名
      string newPataFilePath = _PataFileWriter.CreateDataFile(packet.Timestamp);
      
      // 创建新的文件条目
      _CurrentFileId++;
      var newFileEntry = PataFileEntry.Create(
          (uint)_CurrentFileId,
          Path.GetFileName(newPataFilePath),
          packet.Header.Timestamp,
          packet.Header.Timestamp,
          0
      );
      
      // 添加到内存中的文件条目列表
      _FileEntries.Add(newFileEntry);
      
      // 创建新文件的索引列表
      _FileIndices[newFileEntry.RelativePath] = new List<PataFileIndexEntry>();
  }
  ```

### 2.3 写入数据包到 PATA 文件
- 将数据包写入当前 PATA 文件：
  ```csharp
  long fileOffset = _PataFileWriter.WritePacket(packet);
  ```

### 2.4 更新内存中的索引和统计信息
- 创建并保存文件索引条目：
  ```csharp
  var currentFileEntry = _FileEntries[_CurrentFileId - 1];
  var indexEntry = PataFileIndexEntry.Create(
      packet.Header.Timestamp,
      fileOffset
  );
  
  // 添加到当前文件的索引列表
  string relativePath = currentFileEntry.RelativePath;
  _FileIndices[relativePath].Add(indexEntry);
  ```

- 更新时间范围索引（基于索引间隔）：
  ```csharp
  ushort indexInterval = _PcapFileWriter.Header.IndexInterval;
  long packetTimestamp = packet.Header.Timestamp;
  
  // 如果时间差超过索引间隔，或者是第一个数据包，则创建时间索引
  if (_LastIndexedTimestamp == 0 || 
      (packetTimestamp - _LastIndexedTimestamp) >= indexInterval)
  {
      var timeIndexEntry = PataTimeIndexEntry.Create(
          (uint)_CurrentFileId,
          packetTimestamp
      );
      _TimeIndices.Add(timeIndexEntry);
      _LastIndexedTimestamp = packetTimestamp;
  }
  ```

- 更新统计信息：
  ```csharp
  _TotalSize += packet.TotalSize;
  PacketCount++;
  _LastPacketTimestamp = packet.Header.Timestamp;
  
  // 更新文件条目的结束时间戳
  var entryToUpdate = _FileEntries[_CurrentFileId - 1];
  _FileEntries[_CurrentFileId - 1] = PataFileEntry.Create(
      entryToUpdate.FileId,
      entryToUpdate.RelativePath,
      entryToUpdate.StartTimestamp,
      packet.Header.Timestamp,
      (uint)_FileIndices[entryToUpdate.RelativePath].Count
  );
  ```

### 2.5 自动刷新检查（如果启用）
- 检查是否需要自动刷新：
  ```csharp
  if (AutoFlush)
  {
      Flush(); // 刷新文件缓冲区
  }
  ```

## 3. 文件刷新和关闭阶段

### 3.1 调用 Flush() 方法（中间刷新）
- 刷新内部缓冲区：
  ```csharp
  ThrowIfDisposed();
  if (!IsOpen) { throw new InvalidOperationException("文件未打开"); }
  
  _PataFileWriter.Flush();
  _PcapFileWriter.Flush();
  ```

### 3.2 调用 Close() 方法（完整关闭和更新）
- 确保当前PATA文件操作完成：
  ```csharp
  // 确保所有PATA文件操作完成
  _PataFileWriter.Flush();
  _PataFileWriter.Close();
  ```

- 更新所有文件条目的索引计数：
  ```csharp
  // 更新所有文件条目的索引计数
  UpdateFileEntryIndexCounts();
  ```

- 一次性写入所有索引数据：
  ```csharp
  // 一次性写入所有索引数据
  _PcapFileWriter.WriteAllIndices(_FileEntries, _TimeIndices, _FileIndices);
  ```

- 关闭PCAP文件：
  ```csharp
  _PcapFileWriter.Close();
  ```

### 3.3 资源释放（Dispose方法）
- 释放所有资源：
  ```csharp
  public void Dispose()
  {
      if (!_IsDisposed)
      {
          try
          {
              // 如果文件仍然打开，先关闭
              if (IsOpen)
              {
                  Close();
              }
          }
          finally
          {
              // 释放流资源
              DisposeStreams();
              _IsDisposed = true;
          }
      }
  }
  ```

## 4. 异常处理

### 4.1 Create/Open 方法中的异常处理
```csharp
try
{
    // 创建或打开文件的逻辑
}
catch (Exception ex)
{
    // 释放已创建的资源
    DisposeStreams();
    // 向上级传递异常
    throw new IOException($"创建/打开文件失败: {ex.Message}", ex);
}
```

### 4.2 WritePacket 方法中的异常处理
```csharp
try
{
    // 写入数据包的逻辑
}
catch (Exception ex)
{
    // 向上级传递异常
    throw new IOException($"写入数据包失败: {ex.Message}", ex);
}
```

### 4.3 Close/Flush 方法中的异常处理
```csharp
try
{
    // 关闭/刷新文件的逻辑
}
catch (Exception ex)
{
    // 尝试释放资源
    try { DisposeStreams(); } catch { /* 忽略释放过程中的异常 */ }
    // 向上级传递异常
    throw new IOException($"关闭/刷新文件失败: {ex.Message}", ex);
}
```

## 5. 内部辅助方法

### 5.1 ThrowIfDisposed 方法
```csharp
private void ThrowIfDisposed()
{
    if (_IsDisposed)
    {
        throw new ObjectDisposedException(nameof(PcapWriter));
    }
}
```

### 5.2 ResetState 方法
```csharp
private void ResetState()
{
    PacketCount = 0;
    _TotalSize = 0;
    _FileEntries = new List<PataFileEntry>();
    _TimeIndices = new List<PataTimeIndexEntry>();
    _FileIndices = new Dictionary<string, List<PataFileIndexEntry>>();
    _FirstPacketWritten = false;
    _LastPacketTimestamp = 0;
    _LastIndexedTimestamp = 0;
    _CurrentFileId = 0;
}
```

### 5.3 UpdateFileEntryIndexCounts 方法
```csharp
private void UpdateFileEntryIndexCounts()
{
    for (var i = 0; i < _FileEntries.Count; i++)
    {
        var entry = _FileEntries[i];
        var indexCount = _FileIndices.TryGetValue(entry.RelativePath, out var indices)
            ? (uint)indices.Count
            : 0;

        _FileEntries[i] = PataFileEntry.Create(
            entry.FileId,
            entry.RelativePath,
            entry.StartTimestamp,
            entry.EndTimestamp,
            indexCount
        );
    }
}
```

### 5.4 DisposeStreams 方法
```csharp
private void DisposeStreams()
{
    _PataFileWriter?.Dispose();
    _PcapFileWriter?.Dispose();
}
```