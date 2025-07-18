namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// PCAP格式常量定义
    /// </summary>
    public static class PcapConstants
    {
        /// <summary>
        /// PCAP文件标识，固定值 0xD4C3B2A1
        /// </summary>
        public const uint PCAP_MAGIC_NUMBER = 0xD4C3B2A1;

        /// <summary>
        /// PROJ文件标识 ("PROJ")
        /// </summary>
        public const uint PROJ_MAGIC_NUMBER = 0xA1B2C3D4;

        /// <summary>
        /// 主版本号，固定值 0x0002
        /// </summary>
        public const ushort MAJOR_VERSION = 2;

        /// <summary>
        /// 次版本号，固定值 0x0004，表示支持纳秒级时间量
        /// </summary>
        public const ushort MINOR_VERSION = 4;

        // /// <summary>
        // /// 默认时间戳精度(纳秒)
        // /// </summary>
        // public const uint DEFAULT_TIMESTAMP_ACCURACY = 1;

        // /// <summary>
        // /// 默认索引间隔(毫秒)
        // /// </summary>
        // public const ushort DEFAULT_INDEX_INTERVAL = 1000;

        /// <summary>
        /// 每个PCAP文件最大数据包数量
        /// </summary>
        public const int DEFAULT_MAX_PACKETS_PER_FILE = 500;

        // /// <summary>
        // /// 默认缓冲区大小(字节)
        // /// </summary>
        // public const int DEFAULT_BUFFER_SIZE = 8192;

        /// <summary>
        /// 最大缓冲区大小(字节)
        /// </summary>
        public const int MAX_BUFFER_SIZE = 50 * 1024 * 1024; // 30MB

        // /// <summary>
        // /// 最小缓冲区大小(字节)
        // /// </summary>
        // public const int MIN_BUFFER_SIZE = 4096;

        // /// <summary>
        // /// 默认索引缓冲区大小(条目数)
        // /// </summary>
        // public const int DEFAULT_INDEX_BUFFER_SIZE = 1000;

        // /// <summary>
        // /// 默认索引刷新间隔(毫秒)
        // /// </summary>
        // public const int DEFAULT_INDEX_FLUSH_INTERVAL = 5000;

        /// <summary>
        /// 默认文件命名格式
        /// </summary>
        public const string DEFAULT_FILE_NAME_FORMAT = "yyMMdd_HHmmss_fffffff";

        /// <summary>
        /// 数据包最大大小(字节)
        /// </summary>
        public const int MAX_PACKET_SIZE = 30 * 1024 * 1024; // 30MB
    }
}
