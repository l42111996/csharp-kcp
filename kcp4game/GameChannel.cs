using System;
using doNetty_tcp.code;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using dotNetty_kcp;

namespace kcp4game
{
    public class GameChannel<T>
    {
        private Ukcp _kcpChannel;

        private IChannel _tcpChannel;

        private IProtoDecodeEncode<T> _protoDecodeEncode;


        public void sendMessage(Message<T> message)
        {
            var lenth = _protoDecodeEncode.CalculationLenth(message);
            var bytebuffer = PooledByteBufferAllocator.Default.DirectBuffer(lenth);
            _protoDecodeEncode.encode(bytebuffer,message);

            // if (protocolType == Ukcp.TCP_PROTOCOL)
            // {
            //     _tcpChannel.WriteAndFlushAsync(bytebuffer);
            // }
            //TODO 缓冲区满了？
            try
            {
                // if (protocolType == Ukcp.UDP_PROTOCOL)
                // {
                //     _kcpChannel.writeUdpMessage(bytebuffer);
                // }
                // else if(protocolType== Ukcp.KCP_PROTOCOL)
                // {
                //     _kcpChannel.writeMessage(bytebuffer);
                // }
            }
            finally
            {
                bytebuffer.Release();
            }
        }
    }
}