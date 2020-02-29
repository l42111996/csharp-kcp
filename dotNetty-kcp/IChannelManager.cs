using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

namespace dotNetty_kcp
{
    public interface IChannelManager
    {
        Ukcp get(DatagramPacket msg);

        void New(EndPoint endPoint, Ukcp ukcp);

        void del(Ukcp ukcp);

        ICollection<Ukcp> getAll();
    }
}