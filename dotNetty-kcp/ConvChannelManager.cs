using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Xml.Linq;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

namespace dotNetty_kcp
{
    /**
     * 根据conv确定一个session
     */
    public class ConvChannelManager : IChannelManager
    {

        private readonly ConcurrentDictionary<int, Ukcp> _ukcps = new ConcurrentDictionary<int, Ukcp>();

        private readonly int convIndex;

        public ConvChannelManager(int convIndex)
        {
            this.convIndex = convIndex;
        }

        public Ukcp get(DatagramPacket msg)
        {
            var bytebuffer = msg.Content;
            int conv = bytebuffer.GetIntLE(convIndex);
            _ukcps.TryGetValue(conv, out var ukcp);
            return ukcp;
        }

        public void New(EndPoint endPoint, Ukcp ukcp)
        {
            _ukcps.TryAdd(ukcp.getConv(), ukcp);
        }

        public void del(Ukcp ukcp)
        {
            _ukcps.Remove(ukcp.getConv(), out var temp);
            if (temp == null)
            {
                Console.WriteLine("ukcp session is not exist conv: " + ukcp.getConv());
            }
        }

        public ICollection<Ukcp> getAll()
        {
            return this._ukcps.Values;
        }
    }
}