using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO;

namespace KimoTech.PcapFile.IO.UdpTransmitter
{
    /// <summary>
    /// UDP网络传输器类，支持传输、组播、单播三种模式
    /// </summary>
    public class UdpTransmitter : IDisposable
    {
        private readonly UdpClient _UdpClient;
        private bool _IsDisposed;

        // UDP包的最大大小限制（约64KB）
        private const int MAX_UDP_PACKET_SIZE = 60000;

        /// <summary>
        /// 获取当前网络模式
        /// </summary>
        public NetworkMode NetworkMode { get; }

        /// <summary>
        /// 获取目标端点
        /// </summary>
        public IPEndPoint EndPoint { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">目标端点</param>
        /// <param name="networkMode">网络传输模式</param>
        public UdpTransmitter(IPEndPoint endPoint, NetworkMode networkMode = NetworkMode.Broadcast)
        {
            EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            NetworkMode = networkMode;

            // 验证IP地址与模式的兼容性
            ValidateNetworkModeAndAddress();

            // 创建UDP客户端
            _UdpClient = new UdpClient();

            // 根据模式配置UDP客户端
            ConfigureUdpClient();

            // 使用PcapFile.IO库中定义的缓冲区大小
            // 缓冲区大小为MAX_BUFFER_SIZE的一半，确保发送缓冲区足够大
            _UdpClient.Client.SendBufferSize = PcapConstants.MAX_BUFFER_SIZE / 2;
        }

        /// <summary>
        /// 验证网络模式与IP地址的兼容性
        /// </summary>
        private void ValidateNetworkModeAndAddress()
        {
            switch (NetworkMode)
            {
                case NetworkMode.Broadcast:
                    if (!IsBroadcastAddress(EndPoint.Address))
                    {
                        throw new ArgumentException(
                            "传输模式需要使用传输地址（如 255.255.255.255）"
                        );
                    }

                    break;

                case NetworkMode.Multicast:
                    if (!IsMulticastAddress(EndPoint.Address))
                    {
                        throw new ArgumentException(
                            "组播模式需要使用组播地址（IPv4: 224.0.0.0-239.255.255.255, IPv6: ff00::/8）"
                        );
                    }

                    break;

                case NetworkMode.Unicast:
                    if (!IsUnicastAddress(EndPoint.Address))
                    {
                        throw new ArgumentException("单播模式需要使用单播地址");
                    }

                    break;
            }
        }

        /// <summary>
        /// 配置UDP客户端
        /// </summary>
        private void ConfigureUdpClient()
        {
            switch (NetworkMode)
            {
                case NetworkMode.Broadcast:
                    _UdpClient.EnableBroadcast = true;
                    break;

                case NetworkMode.Multicast:
                    ConfigureMulticast();
                    break;

                case NetworkMode.Unicast:
                    // 单播模式不需要特殊配置
                    break;
            }

            // 如果是IPv6地址，设置IPv6选项
            if (EndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                _UdpClient.Client.SetSocketOption(
                    SocketOptionLevel.IPv6,
                    SocketOptionName.MulticastTimeToLive,
                    64
                );
            }
        }

        /// <summary>
        /// 配置组播选项
        /// </summary>
        private void ConfigureMulticast()
        {
            if (EndPoint.AddressFamily == AddressFamily.InterNetwork)
            {
                // IPv4 组播配置
                _UdpClient.Client.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.MulticastTimeToLive,
                    64
                );

                // 加入组播组
                var multicastOption = new MulticastOption(EndPoint.Address);
                _UdpClient.Client.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.AddMembership,
                    multicastOption
                );
            }
            else if (EndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // IPv6 组播配置
                _UdpClient.Client.SetSocketOption(
                    SocketOptionLevel.IPv6,
                    SocketOptionName.MulticastTimeToLive,
                    64
                );

                // 加入组播组
                var multicastOption = new IPv6MulticastOption(EndPoint.Address);
                _UdpClient.Client.SetSocketOption(
                    SocketOptionLevel.IPv6,
                    SocketOptionName.AddMembership,
                    multicastOption
                );
            }
        }

        /// <summary>
        /// 判断是否为传输地址
        /// </summary>
        private static bool IsBroadcastAddress(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                return bytes[3] == 255; // 简单的传输地址检查
            }

            return false; // IPv6 没有传输
        }

        /// <summary>
        /// 判断是否为组播地址
        /// </summary>
        private static bool IsMulticastAddress(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                return bytes[0] >= 224 && bytes[0] <= 239; // IPv4 组播范围
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var bytes = address.GetAddressBytes();
                return bytes[0] == 0xFF; // IPv6 组播前缀
            }

            return false;
        }

        /// <summary>
        /// 判断是否为单播地址
        /// </summary>
        private static bool IsUnicastAddress(IPAddress address)
        {
            return !IsBroadcastAddress(address)
                && !IsMulticastAddress(address)
                && !IPAddress.IsLoopback(address)
                && !address.Equals(IPAddress.Any)
                && !address.Equals(IPAddress.IPv6Any);
        }

        /// <summary>
        /// 发送数据包
        /// </summary>
        /// <param name="packet">PCAP数据包</param>
        /// <returns>发送的字节数</returns>
        public int SendPacket(DataPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            // 检查是否需要分包发送
            if (packet.Data.Count <= MAX_UDP_PACKET_SIZE)
            {
                // 小包直接发送
                return _UdpClient.Send([.. packet.Data], packet.Data.Count, EndPoint);
            }
            else
            {
                // 大包需要分片发送
                return SendLargePacket([.. packet.Data]);
            }
        }

        /// <summary>
        /// 发送数据包（异步）
        /// </summary>
        /// <param name="packet">PCAP数据包</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送的字节数</returns>
        public async Task<int> SendPacketAsync(
            DataPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            // 检查是否需要分包发送
            if (packet.Data.Count <= MAX_UDP_PACKET_SIZE)
            {
                // 小包直接发送
                return await _UdpClient
                    .SendAsync([.. packet.Data], packet.Data.Count, EndPoint)
                    .WaitAsync(cancellationToken);
            }
            else
            {
                // 大包需要分片发送
                return await SendLargePacketAsync([.. packet.Data], cancellationToken);
            }
        }

        /// <summary>
        /// 发送原始数据
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <returns>发送的字节数</returns>
        public int SendData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // 检查是否需要分包发送
            if (data.Length <= MAX_UDP_PACKET_SIZE)
            {
                // 小包直接发送
                return _UdpClient.Send(data, data.Length, EndPoint);
            }
            else
            {
                // 大包需要分片发送
                return SendLargePacket(data);
            }
        }

        /// <summary>
        /// 发送大数据包（同步）
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <returns>发送的总字节数</returns>
        private int SendLargePacket(byte[] data)
        {
            var totalSent = 0;
            var offset = 0;

            // 每次发送不超过最大包大小的数据
            while (offset < data.Length)
            {
                // 计算当前片段大小
                var chunkSize = Math.Min(MAX_UDP_PACKET_SIZE, data.Length - offset);

                // 创建片段数据
                var chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);

                // 发送片段
                var sent = _UdpClient.Send(chunk, chunk.Length, EndPoint);
                totalSent += sent;
                offset += chunkSize;

                // 小延迟避免网络拥塞
                Thread.Sleep(1);
            }

            return totalSent;
        }

        /// <summary>
        /// 发送大数据包（异步）
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>发送的总字节数</returns>
        private async Task<int> SendLargePacketAsync(
            byte[] data,
            CancellationToken cancellationToken
        )
        {
            var totalSent = 0;
            var offset = 0;

            // 每次发送不超过最大包大小的数据
            while (offset < data.Length && !cancellationToken.IsCancellationRequested)
            {
                // 计算当前片段大小
                var chunkSize = Math.Min(MAX_UDP_PACKET_SIZE, data.Length - offset);

                // 创建片段数据
                var chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);

                // 发送片段
                var sent = await _UdpClient
                    .SendAsync(chunk, chunk.Length, EndPoint)
                    .WaitAsync(cancellationToken);

                totalSent += sent;
                offset += chunkSize;

                // 小延迟避免网络拥塞
                await Task.Delay(1, cancellationToken);
            }

            return totalSent;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    _UdpClient?.Dispose();
                }

                _IsDisposed = true;
            }
        }
    }
}
