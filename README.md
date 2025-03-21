# PcapFile.IO - 数据回放库

PcapFile.IO 是一个用于数据记录和回放的 .NET 库，基于自定义的 PCAP 文件格式，支持高效存储和精确时序回放。该库遵循《回放数据格式协议》规范设计，提供统一的数据处理接口。

## 功能特点

- 支持多源异构数据的统一存储和管理
- 提供精确的时间戳和同步机制（微秒级精度）
- 自动创建和维护工程文件，支持快速时间定位
- 灵活的数据类型扩展能力
- 简洁易用的 API 接口
- 跨平台支持（Windows、Linux、macOS）

## 项目结构

- **PcapFile.IO**: 核心库，包含文件格式定义和基本读写功能
- **PcapFile.IO.Test**: 示例项目，确保库的可靠性和正确性

## 文件格式

### 工程文件 (.pcap)
- 标准 PCAP 文件格式
- 文件头：包含魔数、版本号等基本信息
- 索引项：每个数据包记录包含：
  - 时间戳（8字节）
  - 数据文件相对路径（变长）
  - 数据包在文件中的位置（8字节）
  - 数据包大小（4字节）
- 索引间隔：1秒
- 支持快速时间定位和二分查找

### 数据文件 (.pata)
- 位于工程文件同目录下的 `Packet_Data` 子目录
- 文件头：包含魔数、版本号等基本信息
- 数据包：包含时间戳、长度、校验和等元数据，以及实际数据负载
- 支持多文件存储，自动按时间或大小分割

## 目录结构

```
/path/to/
├── project.pcap              # 工程文件
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
// 创建数据写入器
using var writer = new DataFileWriter();

// 创建新工程（会自动创建工程文件和Packet_Data目录）
writer.Create("data/project.pcap");

// 写入数据包
writer.WritePacket(
    BusinessTypes.BasicApplication, // 业务类型
    DataTypes.LogData,             // 数据类型
    0x0101,                        // 数据源ID
    1,                             // 序列号
    Encoding.UTF8.GetBytes("测试数据") // 数据负载
);

// 完成写入
writer.Flush();
```

### 读取数据

```csharp
// 创建数据读取器
using var reader = new DataFileReader();

// 打开工程文件（会自动处理数据文件）
reader.Open("data/project.pcap");

// 读取所有数据包
while (true)
{
    // 读取下一个数据包
    var packet = reader.ReadNextPacket();
    if (packet == null)
        break;

    // 处理数据包
    Console.WriteLine($"时间戳: {packet.GetTimestamp()}");
    Console.WriteLine($"业务类型: 0x{packet.DataHeader.BusinessType:X2}");
    Console.WriteLine($"数据类型: 0x{packet.DataHeader.DataType:X2}");
    Console.WriteLine($"数据源ID: 0x{packet.DataHeader.SourceId:X4}");
    Console.WriteLine($"负载: {Encoding.UTF8.GetString(packet.Payload)}");
}
```

### 时间定位

```csharp
// 创建数据读取器
using var reader = new DataFileReader();

// 打开工程文件
reader.Open("data/project.pcap");

// 跳转到指定时间（使用工程文件快速定位）
DateTime targetTime = DateTime.UtcNow.AddMinutes(-5);
bool success = reader.SeekToTime(targetTime);

if (success)
{
    // 读取该时间点之后的第一个数据包
    var packet = reader.ReadNextPacket();
    // 处理数据包...
}
```

### 文件信息查询

```csharp
// 获取文件信息
var packetCount = reader.GetPacketCount();
var startTime = reader.GetStartTime();
var endTime = reader.GetEndTime();

Console.WriteLine($"数据包数量: {packetCount}");
Console.WriteLine($"开始时间: {startTime}");
Console.WriteLine($"结束时间: {endTime}");
```

## 开发路线图

- [x] 基础文件读写功能
- [x] 时间戳精确定位
- [x] 工程文件的完整实现
- [x] 基于工程文件的快速时间检索
- [ ] 数据压缩支持
- [ ] 多文件管理
- [ ] 更多数据类型支持
- [ ] 性能优化和基准测试
- [ ] 完整的 API 文档

## 技术要求

- .NET 8.0
- C# 12

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
