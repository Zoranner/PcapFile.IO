# PcapFile.IO - 数据回放库

PcapFile.IO 是一个用于数据记录和回放的 .NET 库，基于自定义的 PCAP 文件格式，支持高效存储和精确时序回放。该库遵循《PCAP文件格式协议》规范设计，提供统一的数据处理接口。

## 功能特点

- 支持多源异构数据的统一存储和管理
- 提供精确的时间戳和同步机制（毫秒级精度）
- 自动创建和维护工程文件，支持快速时间定位
- 灵活的数据格式，支持任意字节数组存储
- 简洁易用的 API 接口
- 跨平台支持（Windows、Linux、macOS）
- 多目标框架支持(.NET Standard 2.1、.NET 6.0、.NET 8.0)

## 项目结构

- **PcapFile.IO**: 核心库，包含文件格式定义和基本读写功能
- **PcapFile.IO.Example**: 示例项目，展示库的基本使用方法

## 文件格式

### 工程文件 (.proj)
- 自定义 PCAP 文件格式
- 文件头：包含魔数、版本号等基本信息
- 文件条目表：每个数据文件的记录信息
- 时间范围索引表：支持快速定位特定时间点
- 文件内索引表：数据包在文件中的精确位置
- 可配置的索引间隔
- 支持快速时间定位和二分查找

### 数据文件 (.pata)
- 位于与工程文件同名的目录中
- 文件命名格式：data_yyMMdd_HHmmss_fff.pata
- 文件头：包含魔数、版本号等基本信息
- 数据包：包含时间戳、长度、校验和等元数据，以及实际数据负载
- 支持多文件存储，自动按时间或大小分割

## 目录结构

```
/path/to/
├── project.proj              # 工程文件
└── project/                  # 数据目录（与工程文件同名）
    ├── data_240321_153045_123.pata  # 数据文件1
    ├── data_240321_154012_456.pata  # 数据文件2
    └── data_240321_155130_789.pata  # 数据文件3
```

## 快速开始

### 安装

通过 NuGet 包管理器安装：

```bash
dotnet add package PcapFile.IO
```

### 写入数据

```csharp
using KimoTech.PcapFile.IO;
using KimoTech.PcapFile.IO.Structures;
using KimoTech.PcapFile.IO.Extensions;

// 创建数据写入器
using var writer = new PcapWriter();

// 创建新工程文件
writer.Create("data/project.proj");

// 创建数据包
var data = Encoding.UTF8.GetBytes("测试数据");
// 使用时间戳而不是DateTime
var timestamp = DateTime.Now.ToUnixTimeMilliseconds();
var packet = new DataPacket(timestamp, data);

// 写入数据包
writer.WritePacket(packet);

// 批量写入数据包
var packets = new List<DataPacket>();
for (int i = 0; i < 10; i++)
{
    var currentTime = DateTime.Now.AddMilliseconds(i * 100);
    var currentTimestamp = currentTime.ToUnixTimeMilliseconds();
    var packetData = Encoding.UTF8.GetBytes($"数据包 #{i}");
    packets.Add(new DataPacket(currentTimestamp, packetData));
}
writer.WritePackets(packets);

// 刷新缓冲区确保数据写入磁盘
writer.Flush();

// 关闭文件
writer.Close();
```

### 读取数据

```csharp
using KimoTech.PcapFile.IO;
using KimoTech.PcapFile.IO.Structures;

// 创建数据读取器
using var reader = new PcapReader();

// 打开工程文件
reader.Open("data/project.proj");

// 显示文件信息
Console.WriteLine($"数据包数量: {reader.PacketCount}");
Console.WriteLine($"时间范围: {reader.StartTime} - {reader.EndTime}");

// 顺序读取所有数据包
reader.Reset(); // 确保从头开始读取
DataPacket packet;
while ((packet = reader.ReadNextPacket()) != null)
{
    // 处理数据包
    Console.WriteLine($"时间戳: {packet.CaptureTime}");
    Console.WriteLine($"数据大小: {packet.PacketLength} 字节");
    Console.WriteLine($"数据内容: {Encoding.UTF8.GetString(packet.Data)}");
}
```

### 时间定位

```csharp
using KimoTech.PcapFile.IO;

// 创建数据读取器
using var reader = new PcapReader();

// 打开工程文件
reader.Open("data/project.proj");

// 跳转到指定时间
DateTime targetTime = DateTime.Now.AddMinutes(-5);
bool success = reader.SeekToTime(targetTime);

if (success)
{
    // 读取该时间点之后的第一个数据包
    var packet = reader.ReadNextPacket();
    if (packet != null)
    {
        Console.WriteLine($"找到时间点附近的数据包: {packet.CaptureTime}");
        // 处理数据包...
    }
}
```

### 批量读取和异步操作

```csharp
using KimoTech.PcapFile.IO;

// 批量读取
var packets = reader.ReadPackets(100); // 一次读取100个数据包
Console.WriteLine($"读取了 {packets.Count} 个数据包");

// 异步读取
async Task ReadDataAsync()
{
    // 创建取消令牌
    using var cts = new CancellationTokenSource();
    
    // 异步读取数据包
    var packets = await reader.ReadPacketsAsync(50, cts.Token);
    Console.WriteLine($"异步读取了 {packets.Count} 个数据包");
}
```

### MATLAB 集成

PcapFile.IO 库可以通过 .NET 互操作性在 MATLAB 中使用。以下是在 MATLAB 中使用的基本步骤：

```matlab
% 加载 .NET 程序集
assembly = NET.addAssembly('path/to/PcapFile.IO.dll');

% 创建 PcapReader 实例
import KimoTech.PcapFile.IO.*;
reader = PcapReader();

% 打开 PCAP 文件
reader.Open('data/project.proj');

% 读取数据包
packet = reader.ReadNextPacket();

% 获取数据
if ~isempty(packet)
    timestamp = packet.CaptureTime;
    data = uint8(packet.Data);
    
    % 将数据转换为 MATLAB 格式处理
    dataStr = char(data');
    disp(['时间戳: ', char(timestamp)]);
    disp(['数据: ', dataStr]);
end

% 关闭文件
reader.Close();
```

更多 MATLAB 示例请参考[MATLAB 集成文档](#matlab-集成)。

## 开发路线图

- [x] 基础文件读写功能
- [x] 时间戳精确定位
- [x] 工程文件的完整实现
- [x] 基于工程文件的快速时间检索
- [x] 异步读写支持
- [ ] 数据压缩支持
- [ ] 加密功能
- [ ] 分布式数据同步
- [ ] 性能优化和基准测试
- [ ] 完整的 API 文档

## 技术要求

- .NET Standard 2.1+/.NET 6.0+/.NET 8.0+
- C# 8.0+

## 文档

- [协议规范](./PROTOCOL.md)：完整的数据格式协议文档
- API 文档：[即将推出]

## 贡献指南

欢迎提交 Issue 和 Pull Request 来帮助改进这个项目。在提交代码前，请确保：

1. 代码符合项目的编码规范
2. 添加了适当的单元测试
3. 所有测试都能通过
4. 更新了相关文档

## 许可证

本项目采用 MIT 许可证。详见 [LICENSE](./LICENSE) 文件。
