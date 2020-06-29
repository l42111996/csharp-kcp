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
     *  TODO 需要测试一下移动环境ip切换了  是否需要重新绑定本地ip  朋友说是不用的  还是需要测一下
     */
    public class KcpClient
    {
        private Bootstrap bootstrap;

        private IExecutorPool _executorPool;

        private IChannelManager _channelManager;

        private IEventLoopGroup _eventLoopGroup;


        public IChannel bindLocal(EndPoint localAddress = null)
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
                if (channelConfig.KcpTag)
                {
                    convIndex += Ukcp.KCP_TAG;
                }
                if(channelConfig.Crc32Check){
                    convIndex+=Ukcp.HEADER_CRC;
                }
                if(channelConfig.FecDataShardCount!=0&&channelConfig.FecParityShardCount!=0){
                    convIndex+= Fec.fecHeaderSizePlus2;
                }
                _channelManager = new ConvChannelManager(convIndex);
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
            var channel = bindLocal(localAddress);
            return connect(channel, remoteAddress, channelConfig, kcpListener);
        }

        /**
         * 连接一个服务器
         */
        public Ukcp connect(EndPoint remoteAddress, ChannelConfig channelConfig, KcpListener kcpListener)
        {
            var channel = bindLocal();
            return connect(channel, remoteAddress, channelConfig, kcpListener);
        }


        public void stop()
        {
            foreach (var ukcp in _channelManager.getAll())
            {
                ukcp.notifyCloseEvent();
            }
            _executorPool?.stop(false);
            if (_eventLoopGroup != null&&!_eventLoopGroup.IsShuttingDown)
            {
                _eventLoopGroup?.ShutdownGracefullyAsync().Wait();
            }
        }
    }
}