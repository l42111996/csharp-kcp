using System;
using base_kcp;
using DotNetty.Buffers;
using DotNetty.Transport.Channels.Sockets;
using fec;

namespace dotNetty_kcp
{
    public class KcpOutPutImp:KcpOutput
    {
        public void outPut(IByteBuffer data, Kcp kcp)
        {
            Snmp.snmp.OutPkts++;
            Snmp.snmp.OutBytes+=data.WriterIndex;
            var user = (User) kcp.User;
            var temp = new DatagramPacket(data,user.LocalAddress, user.RemoteAddress);
            user.Channel.WriteAndFlushAsync(temp);
        }
    }
}