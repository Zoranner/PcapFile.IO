# PCAP 文件格式协议说明

## 目录

- [简介](#简介)
   - [概述](#概述)
   - [关键特性](#关键特性)
   - [版本历史](#版本历史)
   - [适用场景](#适用场景)

- [协议设计](#协议设计)
   - [核心设计理念](#核心设计理念)
   - [主要特点](#主要特点)
   - [目录结构设计](#目录结构设计)

- [文件格式详解](#文件格式详解)
   - [PCAP 数据文件格式](#pcap-数据文件格式)

- [数据规范](#数据规范)
   - [时间同步要求](#时间同步要求)
   - [数据格式要求](#数据格式要求)
   - [数据对齐规范](#数据对齐规范)

- [实现指南](#实现指南)
   - [数据写入最佳实践](#数据写入最佳实践)
   - [性能考量](#性能考量)

- [应用场景](#应用场景)
   - [数据采集](#数据采集)

- [开发指南](#开发指南)
   - [基础工具集](#基础工具集)
   - [数据写入示例](#数据写入示例)

- [安全与故障处理](#安全与故障处理)
   - [数据完整性](#数据完整性)

- [常见问题解答](#常见问题解答)
   - [文件组织与路径](#文件组织与路径)
   - [性能与优化](#性能与优化)
   - [实现与兼容性](#实现与兼容性)

## 简介

### 概述

PCAP 文件格式是为高性能数据采集设计的专用格式，基于标准 PCAP 格式扩展。本协议定义了数据的存储格式，用于存储实际采集的数据包内容。

### 关键特性

- 支持纳秒级时间精度和同步
- 支持多源异构数据统一管理
- 优化的目录结构设计
- 支持通过工程名称组织数据

#### 基本使用流程

1. **创建数据目录**：指定基础目录和工程名称创建数据工程目录
2. **数据写入**：将采集数据写入PCAP数据文件

### 版本历史

| 版本号 | 日期       | 变更说明                                                                     |
| ------ | ---------- | ---------------------------------------------------------------------------- |
| 0.0.1  | 2023-12-26 | 初始版本                                                                     |
| 0.0.2  | 2024-03-19 | 简化数据格式，移除 PacketDataHeader 层，允许用户直接存储任意字节数组         |
| 0.0.3  | 2024-03-20 | 补充文件格式详细说明 |
| 0.0.4  | 2024-03-21 | 更新目录结构设计和文件命名规则，采用时间戳命名格式 |
| 0.0.5  | 2024-04-06 | 移除工程文件格式，简化为仅使用PCAP数据文件 |
| 0.0.6  | 2024-04-06 | 添加数据工程名称支持，用于组织数据文件 |
| 0.0.7  | 2024-04-07 | 移除读取功能，仅保留写入功能 |
| 0.0.8  | 2024-04-07 | 升级时间精度支持，从毫秒精度提升至纳秒精度 |

### 适用场景

- **仿真系统**：复杂系统仿真过程中的数据记录
- **实时系统**：支持对实时系统运行状态的完整记录
- **分布式应用**：为分布式系统提供统一的数据管理机制
- **系统调试与分析**：提供详细的系统行为记录，便于问题定位和性能优化

## 协议设计

### 核心设计理念

PCAP文件格式基于以下核心设计理念：

1. **高效数据组织**：优化的文件结构，支持高效数据访问
2. **灵活数据存储**：支持任意格式数据封装，适应多种应用场景
3. **可扩展架构**：版本化设计确保协议可持续演进
4. **跨平台兼容**：统一的数据表示确保不同系统间数据交换

### 主要特点

- **统一数据管理**：通过标准化封装格式和接口，实现对异构数据的一致性管理
- **精确时间同步**：支持纳秒级时间精度，确保多源数据的时序一致性
- **优化的存储策略**：可选的压缩和加密机制，平衡存储效率和安全性

#### 设计考虑

1. **性能优化**
   - 高效数据组织结构，最小化存取开销
   - 支持并行处理和异步IO，提高数据处理效率
   - 智能缓存机制，优化频繁访问场景

2. **可靠性保证**
   - 多重校验机制确保数据完整性
   - 完善的错误处理和恢复机制

3. **兼容性考虑**
   - 保持与标准PCAP格式的兼容性
   - 支持跨平台数据交换
   - 提供版本升级路径

### 目录结构设计

项目采用的目录结构组织遵循以下规则：

1. 用户需指定基础目录和数据工程名称
2. 系统会在基础目录下创建以数据工程名称命名的子目录
3. PCAP 数据文件存放在该工程子目录中
4. PCAP 数据文件采用 `data_yyMMdd_HHmmss_fffffff.pcap` 命名格式，其中时间戳部分根据创建时间自动生成

目录结构示例：
```
/path/to/base-directory/
└── project-name/                  # 数据工程目录（由工程名称指定）
    ├── data_240321_153045_1234567.pcap  # 数据文件1
    ├── data_240321_154012_4567890.pcap  # 数据文件2
    └── data_240321_155130_7890123.pcap  # 数据文件3
```

这种目录结构设计有以下几个优点：
1. 通过工程名称组织数据，便于管理不同的数据集
2. 通过时间戳命名的数据文件便于按时间顺序组织和查找
3. 简单直观的文件组织方式，易于管理和移动
4. 支持多个数据工程并行工作而不会相互干扰

## 文件格式详解

本协议定义了PCAP 数据文件，用于存储实际的数据包内容。

### PCAP 数据文件格式

数据文件采用标准 PCAP 格式的扩展，用于存储实际的数据内容。

#### 文件头部（16 字节）

| 偏移量 | 长度(字节) | 名称                | 描述                         |
| ------ | ---------- | ------------------- | ---------------------------- |
| 0      | 4          | Magic Number        | 固定值 0xD4C3B2A1            |
| 4      | 2          | Major Version       | 主版本号，当前为 0x0002      |
| 6      | 2          | Minor Version       | 次版本号，当前为 0x0004      |
| 8      | 4          | Timezone Offset     | 时区偏移量，通常为 0         |
| 12     | 4          | Timestamp Accuracy  | 时间戳精度，固定为 0         |

#### 数据包（可变长度）

每个数据包由一个头部和实际数据组成：

##### 数据包头部（16 字节）

| 偏移量 | 长度(字节) | 名称                  | 描述                       |
| ------ | ---------- | --------------------- | -------------------------- |
| 0      | 4          | Timestamp Seconds     | 时间戳秒部分 (UTC)         |
| 4      | 4          | Timestamp Nanoseconds | 时间戳纳秒部分 (UTC)       |
| 8      | 4          | Packet Length         | 数据包长度（字节）         |
| 12     | 4          | Checksum              | 数据包校验和（CRC32）      |

##### 数据包数据（可变长度）

紧随数据包头部之后，存储实际的数据内容。数据长度由数据包头部中的 `Packet Length` 字段指定。

## 数据规范

### 时间同步要求

- 所有数据包必须包含纳秒级精度的时间戳
- 时间戳采用 UTC 时间，避免时区问题
- 多源数据采集时，必须确保时间同步

### 数据格式要求

- 支持任意格式的二进制数据
- 单个数据包大小限制为 10MB 以内
- 文件命名必须遵循 `data_yyMMdd_HHmmss_fffffff.pcap` 格式
- 工程目录使用指定的工程名称创建

### 数据对齐规范

- 所有多字节字段采用小端字节序（Little-Endian）
- 数据结构应按 4 字节对齐，提高访问效率
- 保留字段必须初始化为零值

## 实现指南

### 数据写入最佳实践

- 使用 `PcapWriter` 类创建和写入 PCAP 文件
- 通过 `Create` 方法指定基础目录和工程名称
- 使用 `WritePacket` 或 `WritePackets` 方法写入数据
- 定期调用 `Flush` 方法将缓冲数据写入磁盘
- 使用 `Close` 方法关闭文件
- 使用 `using` 语句确保资源正确释放

#### 数据写入代码示例

```csharp
using KimoTech.PcapFile.IO;

// 创建数据写入器
using var writer = new PcapWriter();

// 创建新数据工程目录 - 数据将存储在 "/path/to/data/my_project" 目录中
writer.Create("/path/to/data", "my_project");

// 创建数据包
var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
// 直接使用DateTime对象创建数据包，内部会自动转换为秒和纳秒部分
var packet = new DataPacket(DateTime.Now, data);

// 写入数据包
writer.WritePacket(packet);

// 关闭文件
writer.Close();
```

### 性能考量

- 通过批量写入减少IO开销
- 合理设置缓冲区大小，平衡内存使用和IO效率
- 当写入大量数据时，定期调用 `Flush` 方法
- 当写入频率较高时，可将 `AutoFlush` 设置为 `false`
- 合理控制数据包大小，避免过大的数据包
- 预先分配数据缓冲区，减少内存分配开销

## 应用场景

### 数据采集

PCAP文件格式特别适合以下数据采集场景：

1. **实时数据采集**：工业控制系统、物联网设备等实时数据的采集
2. **多源数据采集**：多个传感器、多路数据通道的统一采集
3. **高频数据采集**：高频采样数据的记录存储
4. **长时间数据采集**：支持长时间连续数据采集，自动分割文件

## 开发指南

### 基础工具集

PcapFile.IO库提供以下核心类用于文件操作：

- **PcapWriter**：PCAP文件写入器
- **DataPacket**：数据包类，包含时间戳和数据内容
- **DataPacketHeader**：数据包头部结构

### 数据写入示例

以下是一个完整的数据写入示例：

```csharp
using System;
using System.Text;
using KimoTech.PcapFile.IO;

// 创建数据写入器
using var writer = new PcapWriter();

// 创建新数据工程目录
writer.Create("data", "test_project");

// 创建并写入单个数据包 - 使用DateTime直接创建数据包
var data = Encoding.UTF8.GetBytes("Hello, PCAP!");
var packet = new DataPacket(DateTime.Now, data);
writer.WritePacket(packet);

// 批量写入多个数据包
for (int i = 0; i < 10; i++)
{
    var time = DateTime.Now.AddMilliseconds(i * 100);
    var packetData = Encoding.UTF8.GetBytes($"数据包 #{i}");
    writer.WritePacket(new DataPacket(time, packetData));
}

// 刷新缓冲区并关闭文件
writer.Flush();
writer.Close();
```

#### MATLAB数据写入示例

MATLAB用户可以通过.NET Assembly调用PcapFile.IO库，下面是MATLAB中使用PcapFile.IO的示例：

```matlab
% 加载PcapFile.IO程序集
NET.addAssembly('D:\path\to\KimoTech.PcapFile.IO.dll');

% 引用所需的类
import KimoTech.PcapFile.IO.*;
import System.Text.*;
import System.*;

% 创建数据写入器
writer = PcapWriter();

% 创建新数据工程目录
writer.Create('data', 'matlab_project');

% 创建单个数据包并写入 - 使用MATLAB的当前时间
message = 'Hello from MATLAB!';
data = uint8(Encoding.UTF8.GetBytes(message));
% 使用 .NET DateTime 获取当前时间
currentTime = DateTime.Now;
packet = DataPacket(currentTime, data);
writer.WritePacket(packet);

% 批量写入模拟数据
for i = 1:100
    % 创建模拟传感器数据
    sensorData = single([i, sin(i/10), cos(i/10), i^2/100]);
    
    % 将MATLAB数组转换为字节数组
    bytes = typecast(sensorData, 'uint8');
    
    % 创建时间戳（每100毫秒一个样本）
    timeStamp = DateTime.Now.AddMilliseconds(i * 100);
    
    % 创建数据包并写入
    writer.WritePacket(DataPacket(timeStamp, bytes));
end

% 写入MATLAB矩阵数据
matrix = rand(10, 5);
matrixBytes = typecast(single(matrix(:)), 'uint8');
writer.WritePacket(DataPacket(DateTime.Now, matrixBytes));

% 向文件中添加元数据（作为特殊格式的数据包）
metadata = struct('SampleRate', 10, 'SensorType', 'Accelerometer', 'Units', 'm/s^2');
metadataJson = jsonencode(metadata);
metadataBytes = uint8(Encoding.UTF8.GetBytes(metadataJson));
writer.WritePacket(DataPacket(DateTime.Now, metadataBytes));

% 刷新缓冲区并关闭文件
writer.Flush();
writer.Close();
writer.Dispose();

disp('数据已成功写入PCAP文件！');
```

##### 使用MATLAB时间戳（秒+纳秒）

MATLAB用户还可以使用自己的时间戳（秒+纳秒）来创建数据包：

```matlab
% 加载PcapFile.IO程序集
NET.addAssembly('D:\path\to\KimoTech.PcapFile.IO.dll');

% 引用所需的类
import KimoTech.PcapFile.IO.*;
import System.Text.*;
import System.*;

% 创建数据写入器
writer = PcapWriter();

% 创建新数据工程目录
writer.Create('data', 'matlab_timestamp_project');

% 获取当前MATLAB时间并转换为秒和纳秒
current = posixtime(datetime('now', 'TimeZone', 'UTC')); % 获取UTC POSIX时间（秒）
secondsPart = uint32(floor(current)); % 整数秒部分（转换为uint32）
nanosecondsPart = uint32((current - floor(current)) * 1e9); % 纳秒部分（转换为uint32）

% 创建示例数据
sensorData = single([1.1, 2.2, 3.3, 4.4]);
dataBytes = typecast(sensorData, 'uint8');

% 使用秒和纳秒直接构造DataPacket
packet = DataPacket(secondsPart, nanosecondsPart, dataBytes);
writer.WritePacket(packet);

% 批量写入带有时间戳的数据
for i = 1:10
    % 模拟每100毫秒一个样本的数据
    timestampSec = secondsPart;
    timestampNsec = nanosecondsPart + uint32(i * 100000000); % 增加100毫秒（100000000纳秒）
    
    % 处理纳秒溢出情况
    if timestampNsec >= 1000000000
        timestampSec = timestampSec + uint32(floor(double(timestampNsec) / 1000000000));
        timestampNsec = mod(timestampNsec, 1000000000);
    end
    
    % 创建样本数据
    sample = single([i, sin(i/10), cos(i/10)]);
    sampleBytes = typecast(sample, 'uint8');
    
    % 使用秒和纳秒构造DataPacket
    packet = DataPacket(timestampSec, timestampNsec, sampleBytes);
    writer.WritePacket(packet);
end

% 使用MATLAB的高精度计时器记录时间戳
tic;
for i = 1:5
    % 每次使用当前经过的时间创建时间戳
    elapsedTime = toc;
    timestampSec = uint32(secondsPart + floor(elapsedTime));
    timestampNsec = uint32(((elapsedTime - floor(elapsedTime)) * 1e9));
    
    % 记录数据
    data = uint8(Encoding.UTF8.GetBytes(['Sample #', num2str(i)]));
    packet = DataPacket(timestampSec, timestampNsec, data);
    writer.WritePacket(packet);
    
    % 暂停一小段时间
    pause(0.5);
end

% 关闭文件
writer.Flush();
writer.Close();
writer.Dispose();

disp('使用MATLAB时间戳的数据已成功写入PCAP文件！');
```

使用上述示例代码时的注意事项：

1. 确保已正确设置PcapFile.IO.dll的路径
2. MATLAB中的数值数组需转换为字节数组才能写入
3. 调用结束后显式调用Dispose()方法释放资源
4. 可以使用typecast函数在MATLAB数据类型和字节数组间转换
5. 使用jsonencode函数可以将结构体序列化为JSON格式存储
6. 使用posixtime函数可以获取标准的UTC POSIX时间戳
7. 当使用秒+纳秒方式构造DataPacket时，需确保纳秒部分不超过10亿（1秒）

## 安全与故障处理

### 数据完整性

- 每个数据包都包含CRC32校验和
- 写入数据时自动计算并存储校验和
- 文件结构设计支持在部分损坏情况下恢复数据
- 定期调用 `Flush` 方法确保数据写入磁盘，防止系统崩溃导致数据丢失

## 常见问题解答

### 文件组织与路径

**Q: 如何管理大量数据文件？**

A: 使用数据工程名称将文件组织在不同的目录中，同时可以根据时间或数据类型创建不同的工程目录。

**Q: 文件路径是否有长度限制？**

A: 文件路径受操作系统限制，建议保持路径总长度不超过256个字符。

### 性能与优化

**Q: 如何提高写入性能？**

A: 使用 `WritePackets` 方法进行批量写入，减少IO操作次数；将 `AutoFlush` 设置为 `false`，减少磁盘写入频率；预先分配较大的数据缓冲区。

**Q: 数据文件会自动分割吗？**

A: 是的，当文件中数据包数量超过最大限制（默认500个数据包）时，会自动创建新文件继续写入。您也可以手动调用 `Close` 然后重新 `Create` 来创建新文件。

### 实现与兼容性

**Q: 文件大小有限制吗？**

A: 单个数据包大小限制为10MB，PCAP 文件中数据包数量默认限制为500个，超过限制会自动创建新文件。

**Q: 如何在不同操作系统间移动数据文件？**

A: PCAP文件使用标准的二进制格式，可以在不同操作系统间直接迁移使用。注意不同操作系统的路径分隔符差异。

**Q: 时间戳精度如何？**

A: 系统支持纳秒级时间精度，可以准确记录纳秒级别的事件差异。在存储时，时间戳分为秒部分和纳秒部分分别存储。

**Q: 如何处理大量数据的写入？**

A: 对于大量数据写入，建议将 `AutoFlush` 设置为 `false`，使用 `WritePackets` 方法批量写入，并定期手动调用 `Flush` 方法刷新数据。写入完成后务必调用 `Close` 方法正确关闭文件。

