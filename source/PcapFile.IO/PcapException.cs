using System;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP操作异常基类
    /// </summary>
    public class PcapException : Exception
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public PcapErrorCode ErrorCode { get; }

        /// <summary>
        /// 相关文件路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 文件中的位置
        /// </summary>
        public long Position { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="errorCode">错误代码</param>
        /// <param name="message">错误消息</param>
        /// <param name="filePath">相关文件路径</param>
        /// <param name="position">文件中的位置</param>
        /// <param name="innerException">内部异常</param>
        public PcapException(
            PcapErrorCode errorCode,
            string message,
            string filePath = null,
            long position = -1,
            Exception innerException = null
        )
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            FilePath = filePath;
            Position = position;
        }

        /// <summary>
        /// 获取详细的错误描述
        /// </summary>
        /// <returns>包含错误代码、文件路径和位置的详细信息</returns>
        public string GetDetailedMessage()
        {
            var details = $"错误代码: {ErrorCode}, 消息: {Message}";

            if (!string.IsNullOrEmpty(FilePath))
            {
                details += $", 文件: {FilePath}";
            }

            if (Position >= 0)
            {
                details += $", 位置: {Position}";
            }

            return details;
        }
    }

    /// <summary>
    /// PCAP错误代码枚举
    /// </summary>
    public enum PcapErrorCode
    {
        /// <summary>
        /// 未知错误
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 文件未找到
        /// </summary>
        FileNotFound = 1001,

        /// <summary>
        /// 目录不存在
        /// </summary>
        DirectoryNotFound = 1002,

        /// <summary>
        /// 权限不足
        /// </summary>
        InsufficientPermissions = 1003,

        /// <summary>
        /// 磁盘空间不足
        /// </summary>
        DiskSpaceFull = 1004,

        /// <summary>
        /// 无效的文件格式
        /// </summary>
        InvalidFormat = 2001,

        /// <summary>
        /// 文件头损坏
        /// </summary>
        CorruptedHeader = 2002,

        /// <summary>
        /// 数据包损坏
        /// </summary>
        CorruptedData = 2003,

        /// <summary>
        /// 校验和不匹配
        /// </summary>
        ChecksumMismatch = 2004,

        /// <summary>
        /// 数据包大小无效
        /// </summary>
        InvalidPacketSize = 3001,

        /// <summary>
        /// 参数无效
        /// </summary>
        InvalidArgument = 3002,

        /// <summary>
        /// 操作状态无效
        /// </summary>
        InvalidState = 3003,

        /// <summary>
        /// 缓冲区溢出
        /// </summary>
        BufferOverflow = 4001,

        /// <summary>
        /// 内存不足
        /// </summary>
        OutOfMemory = 4002,
    }

    /// <summary>
    /// 文件操作相关异常
    /// </summary>
    public class PcapFileException : PcapException
    {
        public PcapFileException(
            PcapErrorCode errorCode,
            string message,
            string filePath,
            Exception innerException = null
        )
            : base(errorCode, message, filePath, -1, innerException) { }
    }

    /// <summary>
    /// 数据相关异常
    /// </summary>
    public class PcapDataException : PcapException
    {
        public PcapDataException(
            PcapErrorCode errorCode,
            string message,
            string filePath = null,
            long position = -1,
            Exception innerException = null
        )
            : base(errorCode, message, filePath, position, innerException) { }
    }

    /// <summary>
    /// 配置相关异常
    /// </summary>
    public class PcapConfigurationException : PcapException
    {
        public PcapConfigurationException(string message, Exception innerException = null)
            : base(PcapErrorCode.InvalidArgument, message, null, -1, innerException) { }
    }
}
