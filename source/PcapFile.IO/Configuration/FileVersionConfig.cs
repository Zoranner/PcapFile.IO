namespace KimoTech.PcapFile.IO.Configuration
{
    /// <summary>
    /// 文件版本配置
    /// </summary>
    public static class FileVersionConfig
    {
        /// <summary>
        /// PATA文件标识 ("PATA")
        /// </summary>
        public const uint PATA_MAGIC_NUMBER = 0x50415441;

        /// <summary>
        /// PCAP文件标识 ("PCAP")
        /// </summary>
        public const uint PCAP_MAGIC_NUMBER = 0xA1B2C3D4;

        /// <summary>
        /// 主版本号
        /// </summary>
        public const ushort MAJOR_VERSION = 1;

        /// <summary>
        /// 次版本号
        /// </summary>
        public const ushort MINOR_VERSION = 0;

        /// <summary>
        /// 默认时间戳精度(毫秒)
        /// </summary>
        public const uint DEFAULT_TIMESTAMP_ACCURACY = 1000;

        /// <summary>
        /// 默认索引间隔(毫秒)
        /// </summary>
        public const ushort DEFAULT_INDEX_INTERVAL = 1000;

        /// <summary>
        /// 每个PATA文件最大数据包数量
        /// </summary>
        public const int MAX_PACKETS_PER_FILE = 20;
    }
}
