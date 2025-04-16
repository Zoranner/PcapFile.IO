using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KimoTech.PcapFile.IO;

namespace KimoTech.PcapFile.IO.UdpBroadcaster
{
    /// <summary>
    /// UDP广播器类，负责通过UDP发送数据包
    /// </summary>
    public class UdpBroadcaster : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _endPoint;
        private bool _isDisposed;
        
        // UDP包的最大大小限制（约64KB）
        private const int MAX_UDP_PACKET_SIZE = 60000;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">目标端点</param>
        public UdpBroadcaster(IPEndPoint endPoint)
        {
            _endPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));

            // 创建UDP客户端
            _udpClient = new UdpClient();

            // 设置广播选项
            _udpClient.EnableBroadcast = true;

            // 如果是IPv6地址，设置IPv6选项
            if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                _udpClient.Client.SetSocketOption(
                    SocketOptionLevel.IPv6,
                    SocketOptionName.MulticastTimeToLive,
                    2
                );
            }

            // 使用PcapFile.IO库中定义的缓冲区大小
            // 缓冲区大小为MAX_BUFFER_SIZE的一半，确保发送缓冲区足够大
            _udpClient.Client.SendBufferSize = FileVersionConfig.MAX_BUFFER_SIZE / 2;
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
            if (packet.Data.Length <= MAX_UDP_PACKET_SIZE)
            {
                // 小包直接发送
                return _udpClient.Send(packet.Data, packet.Data.Length, _endPoint);
            }
            else
            {
                // 大包需要分片发送
                return SendLargePacket(packet.Data);
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
            if (packet.Data.Length <= MAX_UDP_PACKET_SIZE)
            {
                // 小包直接发送
                return await _udpClient
                    .SendAsync(packet.Data, packet.Data.Length, _endPoint)
                    .WaitAsync(cancellationToken);
            }
            else
            {
                // 大包需要分片发送
                return await SendLargePacketAsync(packet.Data, cancellationToken);
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
                return _udpClient.Send(data, data.Length, _endPoint);
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
            int totalSent = 0;
            int offset = 0;
            
            // 每次发送不超过最大包大小的数据
            while (offset < data.Length)
            {
                // 计算当前片段大小
                int chunkSize = Math.Min(MAX_UDP_PACKET_SIZE, data.Length - offset);
                
                // 创建片段数据
                byte[] chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);
                
                // 发送片段
                int sent = _udpClient.Send(chunk, chunk.Length, _endPoint);
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
        private async Task<int> SendLargePacketAsync(byte[] data, CancellationToken cancellationToken)
        {
            int totalSent = 0;
            int offset = 0;
            
            // 每次发送不超过最大包大小的数据
            while (offset < data.Length && !cancellationToken.IsCancellationRequested)
            {
                // 计算当前片段大小
                int chunkSize = Math.Min(MAX_UDP_PACKET_SIZE, data.Length - offset);
                
                // 创建片段数据
                byte[] chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);
                
                // 发送片段
                int sent = await _udpClient
                    .SendAsync(chunk, chunk.Length, _endPoint)
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
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _udpClient?.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}
