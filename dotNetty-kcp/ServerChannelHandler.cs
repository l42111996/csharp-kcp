using System;
using base_kcp;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using dotNetty_kcp.thread;
using DotNetty.Buffers;
using fec;
using fec.fec;

namespace dotNetty_kcp
{
    public class ServerChannelHandler:ChannelHandlerAdapter
    {
        private readonly IChannelManager _channelManager;

        private readonly ChannelConfig _channelConfig ;

        private readonly IExecutorPool _executorPool;

        private readonly KcpListener _kcpListener;


        public ServerChannelHandler(IChannelManager channelManager, ChannelConfig channelConfig, IExecutorPool executorPool, KcpListener kcpListener)
        {
            _channelManager = channelManager;
            _channelConfig = channelConfig;
            _executorPool = executorPool;
            _kcpListener = kcpListener;
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine(exception);
        }


        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var msg = (DatagramPacket) message;
            var channel = context.Channel;
            var ukcp = _channelManager.get(msg);
            var content = msg.Content;
            User user;
            if (ukcp != null)
            {
                user = ukcp.user();
                //每次收到消息重绑定地址
                user.RemoteAddress = msg.Sender;
                ukcp.read(content);
                return;
            }
            
            
            //如果是新连接第一个包的sn必须为0
            var sn = getSn(content,_channelConfig);
            if(sn!=0)
            {
                msg.Release();
                return;
            }

            var messageExecutor = _executorPool.GetAutoMessageExecutor();
            KcpOutput kcpOutput = new KcpOutPutImp();

            ReedSolomon reedSolomon = null;
            if(_channelConfig.FecDataShardCount!=0&&_channelConfig.FecParityShardCount!=0){
                reedSolomon = ReedSolomon.create(_channelConfig.FecDataShardCount,_channelConfig.FecParityShardCount);
            }

            ukcp = new Ukcp(kcpOutput,_kcpListener,messageExecutor,reedSolomon,_channelConfig);

            user = new User(channel,msg.Sender,msg.Recipient);
            ukcp.user(user);

            _channelManager.New(msg.Sender,ukcp,msg);
            ukcp.read(content);

            var scheduleTask = new ScheduleTask(_channelManager,ukcp);
            KcpUntils.scheduleHashedWheel(scheduleTask, TimeSpan.FromMilliseconds(ukcp.getInterval()));
        }
        
        
        private int getSn(IByteBuffer byteBuf,ChannelConfig channelConfig){
            var headerSize = 0;
            if (channelConfig.Crc32Check)
            {
                headerSize+=Ukcp.HEADER_CRC;
            }
            if(channelConfig.FecDataShardCount!=0&&channelConfig.FecParityShardCount!=0){
                headerSize+= Fec.fecHeaderSizePlus2;
            }
            var sn = byteBuf.GetIntLE(byteBuf.ReaderIndex+Kcp.IKCP_SN_OFFSET+headerSize);
            return sn;
        }


    }
}