using System;
using System.Linq;
using System.Net;
using System.Timers;
using base_kcp;
using DotNetty.Buffers;
using dotNetty_kcp;
using fec;

namespace example_Kcp
{
   public class KcpRttExampleClient : KcpListener
    {

        public static int fastResend = 0;

        private static Ukcp _ukcp;
        public static void start()
        {
            ChannelConfig channelConfig = new ChannelConfig();
            channelConfig.initNodelay(true,40,2,true);
            channelConfig.Sndwnd=512;
            channelConfig.Rcvwnd=512;
            channelConfig.Mtu=512;
            channelConfig.FecDataShardCount=3;
            channelConfig.FecParityShardCount=1;
            channelConfig.AckNoDelay=true;
            channelConfig.Crc32Check = true;
            channelConfig.Conv = 55;
            //channelConfig.setTimeoutMillis(10000);

            KcpClient kcpClient = new KcpClient();
            kcpClient.init(channelConfig);

            KcpRttExampleClient kcpClientRttExample = new KcpRttExampleClient();
            //kcpClient.connect(new InetSocketAddress("127.0.0.1",20003),channelConfig,kcpClientRttExample);

            EndPoint remoteAddress = new IPEndPoint(IPAddress.Parse("127.0.0.1"),20003);
            _ukcp = kcpClient.connect(remoteAddress,channelConfig,kcpClientRttExample);

            try
            {
                kcpClientRttExample.init();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


        }
        private IByteBuffer data;

        private int[] rtts;

        private volatile int count;


        private long startTime;


        Timer timer20 = new Timer();

        ElapsedEventHandler sendhandler;
        private ElapsedEventHandler overHandler;

        public void init()
        {
            sendhandler = new ElapsedEventHandler(sendFunc);
            overHandler = new ElapsedEventHandler(overPrint);

            timer20.Elapsed += sendhandler;
        }


        public KcpRttExampleClient()
        {
            data = Unpooled.DirectBuffer(200);
            for (int i = 0; i < data.Capacity; i++)
            {
                data.WriteByte((byte) i);
            }

            rtts = new int[300];
            for (int i = 0; i < rtts.Length; i++)
            {
                rtts[i] = -1;
            }

            timer20.Enabled = true;
            timer20.Interval = 20;
            timer20.Start();
            startTime = KcpUntils.currentMs();
        }

        private void overPrint(object source, ElapsedEventArgs e)
        {
            var sum = rtts.Sum();
            var max = rtts.Max();
            Console.WriteLine("average: " + (sum / rtts.Length)+" max:"+max);
            Console.WriteLine(Snmp.snmp.ToString());
            timer20.Elapsed -= overHandler;
        }


        private void sendFunc(object source, ElapsedEventArgs e)
        {
            var byteBuf = rttMsg(++count);
            _ukcp.write(byteBuf);
            byteBuf.Release();
            if (count >= rtts.Length) {
                // finish
                timer20.Elapsed -= sendhandler;
                byteBuf = rttMsg(-1);
                _ukcp.write(byteBuf);
                byteBuf.Release();
            }
        }



        /**
       * count+timestamp+dataLen+data
       *
       * @param count
       * @return
       */
        public IByteBuffer rttMsg(int count) {
            IByteBuffer buf = Unpooled.DirectBuffer(10);
            buf.WriteShort(count);
            buf.WriteInt((int) (KcpUntils.currentMs()- startTime));

            //int dataLen = new Random().nextInt(200);
            //buf.writeBytes(new byte[dataLen]);

            int dataLen = data.ReadableBytes;
            buf.WriteShort(dataLen);
            buf.WriteBytes(data, data.ReaderIndex, dataLen);
            return buf;
        }

        public void onConnected(Ukcp ukcp)
        {
            
            
        }

        public void handleReceive(IByteBuffer byteBuf, Ukcp ukcp)
        {
            int curCount = byteBuf.ReadShort();
            if (curCount == -1)
            {
                timer20.Elapsed += overHandler;
            }
            else
            {
                int idx = curCount - 1;
                long time = byteBuf.ReadInt();
                if (rtts[idx] != -1)
                {
                    Console.WriteLine("end");
                }
                //log.info("rcv count {} {}", curCount, System.currentTimeMillis());
                rtts[idx] = (int) (KcpUntils.currentMs() - startTime - time);
                Console.WriteLine("rtt : " + curCount + "  " + rtts[idx]);
            }
        }

        public void handleException(Exception ex, Ukcp ukcp)
        {
            Console.WriteLine(ex);
        }

        public void handleClose(Ukcp ukcp)
        {
        }
    }
}