using System;
using System.Net;
using System.Net.Sockets;
using base_kcp;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using dotNetty_kcp.thread;
using fec;
using fec.fec;

namespace dotNetty_kcp
{
    /**
     * kcp客户端
     * 客户端使用方式:
     * 1,与服务器tcp通讯得到conv
     * 2,kcp通过conv标识与服务器通讯
     * 3,客户端发现网络断开重连之后必须通过kcp发送一个心跳包出去 用于服务器确定客户端的出口地址
     * 4,客户端需要最少每60秒发送一个心跳数据包服务端收到后回复客户端用于 路由表记录映射信息
     */
    public class KcpClient
    {
        private Bootstrap bootstrap;

        private IExecutorPool _executorPool;

        private IChannelManager _channelManager;

        private IEventLoopGroup _eventLoopGroup;


        private static IChannel bindLocal(Bootstrap bootstrap,EndPoint localAddress = null)
        {
            if (localAddress == null)
            {
                localAddress = new IPEndPoint(IPAddress.Any, 0);
            }
            return bootstrap.BindAsync(localAddress).Result;
        }

        public void init(ChannelConfig channelConfig,ExecutorPool executorPool,IEventLoopGroup eventLoopGroup)
        {
            if(channelConfig.UseConvChannel){
                var convIndex = 0;
                if(channelConfig.Crc32Check){
                    convIndex+=Ukcp.HEADER_CRC;
                }
                if(channelConfig.FecDataShardCount!=0&&channelConfig.FecParityShardCount!=0){
                    convIndex+= Fec.fecHeaderSizePlus2;
                }
                _channelManager = new ClientConvChannelManager(convIndex);
            }else{
                _channelManager = new ClientEndPointChannelManager();
            }

            //初始化线程池 创建一个线程就够了
            _executorPool = executorPool;
            _executorPool.CreateMessageExecutor();
            _eventLoopGroup = eventLoopGroup;

            bootstrap = new Bootstrap();
            bootstrap.Group(_eventLoopGroup);
            bootstrap.ChannelFactory(() => new SocketDatagramChannel(AddressFamily.InterNetwork));
            bootstrap.Handler(new ActionChannelInitializer<SocketDatagramChannel>(channel =>
            {
                var pipeline = channel.Pipeline;
                pipeline.AddLast(new ClientChannelHandler(_channelManager,channelConfig));
            }));
        }



        public void init(ChannelConfig channelConfig)
        {
            var executorPool = new ExecutorPool();
            executorPool.CreateMessageExecutor();
            init(channelConfig,executorPool,new MultithreadEventLoopGroup());
        }
        
        
        /**
         * 重连接口
         * 使用旧的kcp对象，出口ip和端口替换为新的
         * 在4G切换为wifi等场景使用
         * @param ukcp
         */
        public void reconnect(Ukcp ukcp){
            if (!(_channelManager is ServerConvChannelManager))
            {
                throw new Exception("reconnect can only be used in convChannel");
            }
            ukcp.IMessageExecutor.execute(new ReconnectTask(ukcp,bootstrap));
        }

        private class ReconnectTask : ITask
        {
            private readonly Ukcp _ukcp;
            private readonly Bootstrap _bootstrap;

            public ReconnectTask(Ukcp ukcp, Bootstrap bootstrap)
            {
                _ukcp = ukcp;
                _bootstrap = bootstrap;
            }

            public override void execute()
            {
                _ukcp.user().Channel.CloseAsync();
                var iChannel = bindLocal(_bootstrap);
                _ukcp.user().Channel = iChannel;
            }
        }


        public Ukcp connect(IChannel localChannel,EndPoint remoteAddress, ChannelConfig channelConfig, KcpListener kcpListener)
        {

            KcpOutput kcpOutput = new KcpOutPutImp();
            ReedSolomon reedSolomon = null;
            if (channelConfig.FecDataShardCount != 0 && channelConfig.FecParityShardCount != 0)
            {
                reedSolomon = ReedSolomon.create(channelConfig.FecDataShardCount, channelConfig.FecParityShardCount);
            }

            var _messageExecutor = _executorPool.GetAutoMessageExecutor();

            var ukcp = new Ukcp(kcpOutput, kcpListener, _messageExecutor, reedSolomon, channelConfig);

            var user = new User(localChannel, remoteAddress, localChannel.LocalAddress);
            ukcp.user(user);

            _channelManager.New(localChannel.LocalAddress, ukcp,null);

            var scheduleTask = new ScheduleTask( _channelManager, ukcp);
            KcpUntils.scheduleHashedWheel(scheduleTask, TimeSpan.FromMilliseconds(ukcp.getInterval()));
            return ukcp;
        }

        /**
         * 连接一个服务器
         */
        public Ukcp connect(EndPoint localAddress,EndPoint remoteAddress, ChannelConfig channelConfig, KcpListener kcpListener)
        {
            var channel = bindLocal(bootstrap,localAddress);
            return connect(channel, remoteAddress, channelConfig, kcpListener);
        }

        /**
         * 连接一个服务器
         */
        public Ukcp connect(EndPoint remoteAddress, ChannelConfig channelConfig, KcpListener kcpListener)
        {
            var channel = bindLocal(bootstrap);
            return connect(channel, remoteAddress, channelConfig, kcpListener);
        }


        public void stop()
        {
            foreach (var ukcp in _channelManager.getAll())
            {
                ukcp.close();
            }
            _executorPool?.stop(false);
            if (_eventLoopGroup != null&&!_eventLoopGroup.IsShuttingDown)
            {
                _eventLoopGroup?.ShutdownGracefullyAsync().Wait();
            }
        }
    }
}