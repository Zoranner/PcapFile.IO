using System;

namespace KimoTech.PcapFile.IO.UdpTransmitter
{
    /// <summary>
    /// 网络传输模式枚举
    /// </summary>
    public enum NetworkMode
    {
        /// <summary>
        /// 传输模式 - 发送到网络中的所有设备
        /// </summary>
        Broadcast,

        /// <summary>
        /// 组播模式 - 发送到特定组播组的设备
        /// </summary>
        Multicast,

        /// <summary>
        /// 单播模式 - 发送到指定的单个设备
        /// </summary>
        Unicast,
    }
}
