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
    public class ClientConvChannelManager : IChannelManager
    {

        private readonly ConcurrentDictionary<int, Ukcp> _ukcps = new ConcurrentDictionary<int, Ukcp>();

        private readonly int convIndex;

        public ClientConvChannelManager(int convIndex)
        {
            this.convIndex = convIndex;
        }

        public Ukcp get(DatagramPacket msg)
        {
            var conv = getConv(msg);
            _ukcps.TryGetValue(conv, out var ukcp);
            return ukcp;
        }

        private int getConv(DatagramPacket msg) {
            var bytebuffer = msg.Content;
            return bytebuffer.GetIntLE(convIndex);;
        }
        
        
        
        public void New(EndPoint endPoint, Ukcp ukcp,DatagramPacket msg)
        {
            var conv = ukcp.getConv();
            if (msg != null) {
                conv = getConv(msg);
                ukcp.setConv(conv);
            }
            _ukcps.TryAdd(conv, ukcp);
        }

        public void del(Ukcp ukcp)
        {
            _ukcps.Remove(ukcp.getConv(), out var temp);
            if (temp == null)
            {
                Console.WriteLine("ukcp session is not exist conv: " + ukcp.getConv());
            }
            ukcp.user().Channel.CloseAsync();
        }

        public ICollection<Ukcp> getAll()
        {
            return this._ukcps.Values;
        }
    }
}