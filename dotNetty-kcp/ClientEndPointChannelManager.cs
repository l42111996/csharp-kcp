using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using DotNetty.Transport.Channels.Sockets;

namespace dotNetty_kcp
{
    public class ClientEndPointChannelManager : IChannelManager
    {
        private readonly ConcurrentDictionary<EndPoint, Ukcp> _ukcps = new ConcurrentDictionary<EndPoint, Ukcp>();

        public Ukcp get(DatagramPacket msg)
        {
            _ukcps.TryGetValue(msg.Recipient, out var ukcp);
            return ukcp;
        }

        public void New(EndPoint endPoint, Ukcp ukcp, DatagramPacket msg)
        {
            _ukcps[endPoint] = ukcp;
        }

        public void del(Ukcp ukcp)
        {
            _ukcps.Remove(ukcp.user().LocalAddress, out var temp);
            if (temp == null)
            {
                Console.WriteLine("ukcp session is not exist RemoteAddress: " + ukcp.user().RemoteAddress);
            }
        }

        public ICollection<Ukcp> getAll()
        {
            return _ukcps.Values;
        }
    }
}