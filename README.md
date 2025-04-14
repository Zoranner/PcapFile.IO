# PcapFile.IO - 数据写入库

PcapFile.IO 是一个用于数据记录的 .NET 库，基于自定义的 PCAP 文件格式，支持高效存储。该库采用简化设计，提供统一的数据写入接口。

## 功能特点

- 支持多源异构数据的统一存储和管理
- 提供精确的时间戳机制（纳秒级精度）
- 自动创建和管理数据文件，支持时间戳命名
- 根据数据工程名称组织数据文件
- 灵活的数据格式，支持任意字节数组存储
- 简洁易用的 API 接口
- 跨平台支持（Windows、Linux、macOS）
- 多目标框架支持(.NET Standard 2.1、.NET 6.0、.NET 8.0)

## 项目结构

- **PcapFile.IO**: 核心库，包含文件格式定义和基本写入功能
- **PcapFile.IO.Example**: 示例项目，展示库的基本使用方法

## 文件格式

### 数据文件 (.pcap)
- 位于指定的数据工程目录中
- 文件命名格式：data_yyMMdd_HHmmss_fffffff.pcap
- 文件头：包含魔数、版本号等基本信息
- 数据包：包含时间戳、长度、校验和等元数据，以及实际数据负载
- 支持多文件存储，自动按时间或大小分割

## 目录结构

```
/path/to/base-directory/
└── project-name/                  # 数据工程目录（由工程名称指定）
    ├── data_240321_153045_123456789.pcap  # 数据文件1
    ├── data_240321_154012_456789012.pcap  # 数据文件2
    └── data_240321_155130_789012345.pcap  # 数据文件3
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

// 创建新数据工程目录 - 数据将存储在 "data/my_project" 目录中
writer.Create("data", "my_project");

// 创建数据包
var data = Encoding.UTF8.GetBytes("测试数据");
// 直接使用DateTime对象创建数据包
var packet = new DataPacket(DateTime.Now, data);

// 写入数据包
writer.WritePacket(packet);

// 批量写入数据包
var packets = new List<DataPacket>();
for (int i = 0; i < 10; i++)
{
    var currentTime = DateTime.Now.AddMilliseconds(i * 100);
    var packetData = Encoding.UTF8.GetBytes($"数据包 #{i}");
    packets.Add(new DataPacket(currentTime, packetData));
}
writer.WritePackets(packets);

// 刷新缓冲区确保数据写入磁盘
writer.Flush();

// 关闭文件
writer.Close();
```

## 开发路线图

- [x] 基础文件写入功能
- [x] 时间戳精确记录
- [x] 按工程名组织数据文件
- [x] 纳秒级时间精度支持

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
