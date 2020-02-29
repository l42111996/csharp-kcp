using System;
using System.Collections.Generic;
using System.Threading;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using base_kcp;

namespace fec
{
    public class LatencySimulator : KcpOutput
    {
        private static long long2Uint(long n)
        {
            return n & 0x00000000FFFFFFFFL;
        }

        private long current;

        /**
         * 丢包率
         **/
        private int lostrate;
        private int rttmin;
        private int rttmax;
        private LinkedList<DelayPacket> p12 = new LinkedList<DelayPacket>();
        private LinkedList<DelayPacket> p21 = new LinkedList<DelayPacket>();
        private Random r12 = new Random();
        private Random r21 = new Random();

        private Random _random = new Random();


        // lostrate: 往返一周丢包率的百分比，默认 10%
        // rttmin：rtt最小值，默认 60
        // rttmax：rtt最大值，默认 125
        //func (p *LatencySimulator)Init(int lostrate = 10, int rttmin = 60, int rttmax = 125, int nmax = 1000):
        public void init(int lostrate, int rttmin, int rttmax)
        {
            this.current = DateTime.Now.Ticks/10000;
            this.lostrate = lostrate / 2; // 上面数据是往返丢包率，单程除以2
            this.rttmin = rttmin / 2;
            this.rttmax = rttmax / 2;
        }


        // 发送数据
        // peer - 端点0/1，从0发送，从1接收；从1发送从0接收
        public int send(int peer, IByteBuffer data)
        {
            int rnd;
            if (peer == 0)
            {
                rnd = r12.Next(100);
            }
            else
            {
                rnd = r21.Next(100);
            }

            //println("!!!!!!!!!!!!!!!!!!!!", rnd, p.lostrate, peer)
            if (rnd < lostrate)
            {
                return 0;
            }

            DelayPacket pkt = new DelayPacket();
            pkt.init(data);
            current = DateTime.Now.Ticks/10000;
            int delay = rttmin;
            if (rttmax > rttmin)
            {
                delay += _random.Next(10000) % (rttmax - rttmin);
            }

            pkt.setTs(current + delay);
            if (peer == 0)
            {
                p12.AddLast(pkt);
            }
            else
            {
                p21.AddLast(pkt);
            }

            return 1;
        }

        // 接收数据
        public int recv(int peer, IByteBuffer data)
        {
            DelayPacket pkt;
            if (peer == 0)
            {
                if (p21.Count == 0)
                {
                    return -1;
                }

                pkt = p21.First.Value;
            }
            else
            {
                if (p12.Count == 0)
                {
                    return -1;
                }

                pkt = p12.First.Value;
            }

            current = DateTime.Now.Ticks/10000;

            if (current < pkt.getTs())
            {
                return -2;
            }

            if (peer == 0)
            {
                p21.RemoveFirst();
            }
            else
            {
                p12.RemoveFirst();
            }

            int maxsize = pkt.getPtr().ReadableBytes;
//            IByteBuffer data1 = data;
//            IByteBuffer data2 = pkt.getPtr();
//            Console.WriteLine(data1.AddressOfPinnedMemory().ToString());
//            Console.WriteLine(data2.AddressOfPinnedMemory().ToString());

            data.WriteBytes(pkt.getPtr());
//            data2.Release();
            pkt.Release();
//            Console.WriteLine(data.ReferenceCount);
            return maxsize;
        }


        public static void Main(String[] args)
        {
//            LatencySimulator latencySimulator = new LatencySimulator();
//            try
//            {
////                //latencySimulator.test(0);
////                //latencySimulator.test(1);
//                latencySimulator.test(2);
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e);
//            }

//            latencySimulator.BenchmarkFlush();
        }


        //测试flush性能
        public void BenchmarkFlush()
        {
            Kcp kcp = new Kcp(1, new LatencySimulator());
            for (int i = 0; i < 1000; i++)
            {
                Segment segment = Segment.createSegment(null);
                kcp.sndBuf.AddLast(segment);
            }

            foreach (var seg in kcp.sndBuf)
            {
                seg.Xmit = 1;
                seg.Resendts = kcp.currentMs() + 10000;
            }

            //预热
            for (int i = 0; i < 1000000; i++)
            {
                kcp.flush(false, kcp.currentMs());
            }

            long start = kcp.currentMs();
            for (int i = 0; i < 200000; i++)
            {
                kcp.flush(false, kcp.currentMs());
            }

            Console.WriteLine((kcp.currentMs() - start) / 200000);
        }



        class TestOutPut:KcpOutput
        {
            private LatencySimulator vnet;
            private int id;

            public TestOutPut(LatencySimulator vnet,int id)
            {
                this.vnet = vnet;
                this.id = id;
            }

            public void outPut(IByteBuffer data, Kcp kcp)
            {
                vnet.send(id, data);
                data.Release();
            }
        }




        public void test(int mode)
        {
            LatencySimulator vnet = new LatencySimulator();
            vnet.init(20, 600, 600);
            TestOutPut output1 = new TestOutPut(vnet, 0);
            TestOutPut output2 = new TestOutPut(vnet, 1);


            Kcp kcp1 = new Kcp(0x11223344, output1);
            Kcp kcp2 = new Kcp(0x11223344, output2);
            //kcp1.setAckMaskSize(8);
            //kcp2.setAckMaskSize(8);

            current = long2Uint(kcp1.currentMs());
            long slap = current + 20;
            int index = 0;
            int next = 0;
            long sumrtt = 0;
            int count = 0;
            int maxrtt = 0;
            kcp1.RcvWnd = 512;
            kcp1.SndWnd = 512;
            kcp2.RcvWnd = 512;
            kcp2.SndWnd = 512;

            // 判断测试用例的模式
            if (mode == 0)
            {
                // 默认模式
                kcp1.initNodelay(false, 10, 0, false);
                kcp2.initNodelay(false, 10, 0, false);
            }
            else if (mode == 1)
            {
                // 普通模式，关闭流控等
                kcp1.initNodelay(false, 10, 0, true);
                kcp2.initNodelay(false, 10, 0, true);
            }
            else
            {
                // 启动快速模式
                // 第二个参数 nodelay-启用以后若干常规加速将启动
                // 第三个参数 interval为内部处理时钟，默认设置为 10ms
                // 第四个参数 resend为快速重传指标，设置为2
                // 第五个参数 为是否禁用常规流控，这里禁止
                kcp1.initNodelay(true, 10, 2, true);
                kcp2.initNodelay(true, 10, 2, true);
                kcp1.RxMinrto = 10;
                kcp1.Fastresend = 1;
            }

            int hr;
            long ts1 = kcp1.currentMs();

            //写数据 定时更新
            for (;;)
            {
                current = long2Uint(kcp1.currentMs());
                Thread.Sleep(1);
                long now = kcp1.currentMs();
                kcp1.update(now);
                kcp2.update(now);


                //每隔 20ms，kcp1发送数据
                for (; current >= slap; slap += 20)
                {
                    IByteBuffer buf = PooledByteBufferAllocator.Default.Buffer();
                    buf.WriteIntLE(index);
                    index++;
                    buf.WriteIntLE((int) current);
                    kcp1.send(buf);
                    buf.Release();
                }

                //处理虚拟网络：检测是否有udp包从p1->p2
                for (;;)
                {
                    IByteBuffer buffer = PooledByteBufferAllocator.Default.DirectBuffer(2000);
//                    Console.WriteLine("buffer:" +buffer.AddressOfPinnedMemory().ToString());
                    try
                    {
                        hr = vnet.recv(1, buffer);
                        if (hr < 0)
                        {
                            break;
                        }

                        kcp2.input(buffer, true, kcp1.currentMs());
                    }
                    finally
                    {
                        buffer.Release();
                    }
                }

                // 处理虚拟网络：检测是否有udp包从p2->p1
                for (;;)
                {
                    IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer(2000);
                    try
                    {
                        hr = vnet.recv(0, buffer);
                        if (hr < 0)
                        {
                            break;
                        }

                        // 如果 p1收到udp，则作为下层协议输入到kcp1
                        kcp1.input(buffer, true, kcp1.currentMs());
                    }
                    finally
                    {
                        buffer.Release();
                    }
                }

                // kcp2接收到任何包都返回回去
                List<IByteBuffer> bufList = new List<IByteBuffer>();
                kcp2.recv(bufList);
                foreach (var byteBuf in bufList)
                {
                    kcp2.send(byteBuf);
                    byteBuf.Release();
                }

                // kcp1收到kcp2的回射数据
                bufList = new List<IByteBuffer>();
                kcp1.recv(bufList);
                foreach (var byteBuf in bufList){
                    long sn = byteBuf.ReadIntLE();
                    long ts = byteBuf.ReadUnsignedIntLE();
                    long rtt = 0;
                    rtt = current - ts;
                    Console.WriteLine("rtt :" + rtt);


                    if (sn != next)
                    {
                        // 如果收到的包不连续
                        //for i:=0;i<8 ;i++ {
                        //println("---", i, buffer[i])
                        //}
                        Console.WriteLine("ERROR sn " + count + "<->" + next + sn);
                        return;
                    }

                    next++;
                    sumrtt += rtt;
                    count++;
                    if (rtt > maxrtt)
                    {
                        maxrtt = (int) rtt;
                    }

                    byteBuf.Release();
                }
                if (next > 1000)
                {
                    break;
                }
            }

            ts1 = kcp1.currentMs() - ts1;
            String[] names = new String[] {"default", "normal", "fast"};
            Console.WriteLine(names[mode]+" mode result :"+ts1+" \n");
            Console.WriteLine("avgrtt="+(sumrtt / count)+" maxrtt="+maxrtt+" \n");
            Console.WriteLine("lost percent: " + (Snmp.snmp.RetransSegs)+"\n");
            Console.WriteLine("snmp: " + (Snmp.snmp.ToString()));
        }


        public void outPut(IByteBuffer data, Kcp kcp)
        {
            throw new NotImplementedException();
        }
    }
}