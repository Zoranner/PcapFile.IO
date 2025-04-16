# PCAP文件验证工具

这是一个用于验证PCAP数据文件是否符合协议规范的工具，能够分析并显示PCAP文件中的详细信息。

## 功能特点

- 验证PCAP文件头格式是否符合规范
- 验证数据包的校验和是否正确
- 提供全面的数据包统计信息
- 能够显示所有数据包的详细信息
- 支持数据包内容的十六进制预览
- 支持验证目录中的所有PCAP文件
- 输出详细的错误信息与分析结果

## 使用方法

### 基本使用

验证单个文件:

```bash
PcapFileValidator.exe <PCAP文件路径>
```

验证目录下所有PCAP文件:

```bash
PcapFileValidator.exe <PCAP目录路径>
```

### 高级选项

显示所有数据包的详细信息:

```bash
PcapFileValidator.exe <PCAP文件路径> --all
```

显示数据包内容预览:

```bash
PcapFileValidator.exe <PCAP文件路径> --preview
```

同时使用多个选项:

```bash
PcapFileValidator.exe <PCAP文件路径> --all --preview
```

## 输出信息说明

### 基本信息
- 文件格式信息（魔术数字、版本号、字节序等）
- 数据包总数
- 校验结果统计
- 数据包大小统计
- 时间戳范围

### 详细模式 (--all)
- 每个数据包的序号
- 精确的时间戳
- 数据包大小
- 校验和信息
- 校验结果

### 预览模式 (--preview)
- 数据包内容的十六进制与ASCII双格式显示
- 最多显示前64字节数据

## 构建方法

```bash
dotnet build
```

## 依赖项

- .NET 6.0或.NET 8.0
- PcapFile.IO库

## 使用场景

- 验证数据采集软件生成的PCAP文件是否正确
- 分析PCAP文件中的数据包格式
- 检查时间戳是否连续
- 验证校验和算法是否正确
- 调试PCAP写入功能 