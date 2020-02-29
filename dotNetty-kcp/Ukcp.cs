using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using base_kcp;
using DotNetty.Buffers;
using DotNetty.Transport.Channels.Sockets;
using dotNetty_kcp.thread;
using fec;
using fec.fec;

namespace dotNetty_kcp
{
    public class Ukcp
    {
        public const int HEADER_CRC = 4, KCP_TAG=1, HEADER_NONCESIZE = 16;

        public const int UDP_PROTOCOL = 1, KCP_PROTOCOL = 0, TCP_PROTOCOL = 2;

        private readonly Kcp kcp;

        private bool fastFlush = true;

        private long tsUpdate = -1;

        private bool active;

        private readonly FecEncode _fecEncode;
        private readonly FecDecode _fecDecode;

        private readonly MpscArrayQueue<IByteBuffer> _sendList;

        private readonly MpscArrayQueue<IByteBuffer> _recieveList;

        private readonly IMessageExecutor _iMessageExecutor;

        private readonly KcpListener _kcpListener;

        private readonly ChannelConfig _channelConfig;



        /**
         * 上次收到完整消息包时间
         * 用于心跳检测
         **/
        public long LastRecieveTime { get; set; } = KcpUntils.currentMs();



        /**
         * Creates a new instance.
         *
         * @param output output for kcp
         */
        public Ukcp(KcpOutput output, KcpListener kcpListener, IMessageExecutor iMessageExecutor,
            ReedSolomon reedSolomon, ChannelConfig channelConfig)
        {
            this._channelConfig = channelConfig;
            this.kcp = new Kcp(channelConfig.Conv, output);
            this.active = true;
            this._kcpListener = kcpListener;
            this._iMessageExecutor = iMessageExecutor;
            //默认2<<11   可以修改
            _sendList = new MpscArrayQueue<IByteBuffer>(2<<10);
            _recieveList = new MpscArrayQueue<IByteBuffer>(2<<10);
            //recieveList = new SpscLinkedQueue<>();
            int headerSize = 0;

            if (channelConfig.KcpTag)
            {
                headerSize += KCP_TAG;
            }

            //init crc32
            if(channelConfig.Crc32Check){
                var kcpOutput = kcp.Output;
                kcpOutput = new Crc32OutPut(kcpOutput,headerSize);
                kcp.Output=kcpOutput;
                headerSize+=HEADER_CRC;
            }

            //init fec
            if (reedSolomon != null)
            {
                var kcpOutput = kcp.Output;
                _fecEncode = new FecEncode(headerSize, reedSolomon, channelConfig.Mtu);
                _fecDecode = new FecDecode(3 * reedSolomon.getTotalShardCount(), reedSolomon, channelConfig.Mtu);
                kcpOutput = new FecOutPut(kcpOutput, _fecEncode);
                kcp.Output = kcpOutput;
                headerSize += Fec.fecHeaderSizePlus2;
            }

            kcp.setReserved(headerSize);
            intKcpConfig(channelConfig);
        }


        private void intKcpConfig(ChannelConfig channelConfig)
        {
            kcp.initNodelay(channelConfig.Nodelay, channelConfig.Interval, channelConfig.Fastresend,
                channelConfig.Nocwnd);
            kcp.SndWnd = channelConfig.Sndwnd;
            kcp.RcvWnd = channelConfig.Rcvwnd;
            kcp.Mtu = channelConfig.Mtu;
            kcp.Stream = channelConfig.Stream;
            kcp.AckNoDelay = channelConfig.AckNoDelay;
            kcp.AutoSetConv = channelConfig.AutoSetConv;
            kcp.setAckMaskSize(channelConfig.AckMaskSize);
            fastFlush = channelConfig.FastFlush;
        }


        /**
         * Receives ByteBufs.
         *
         * @param bufList received IByteBuffer will be add to the list
         */
        protected internal void receive(List<IByteBuffer> bufList)
        {
            kcp.recv(bufList);
        }


        protected internal IByteBuffer mergeReceive()
        {
            return kcp.mergeRecv();
        }


        public void input(IByteBuffer data, long current)
        {
//            _lastRecieveTime = KcpUntils.currentMs();
            Snmp.snmp.InPkts++;
            Snmp.snmp.InBytes += data.ReadableBytes;
            if (_channelConfig.Crc32Check)
            {
                long checksum = data.ReadUnsignedInt();
                if (checksum != Crc32.ComputeChecksum(data,data.ReaderIndex,data.ReadableBytes))
                {
                    Snmp.snmp.InCsumErrors++;
                    return;
                }
            }

            if (_fecDecode != null)
            {
                FecPacket fecPacket = FecPacket.newFecPacket(data);
                if (fecPacket.Flag == Fec.typeData)
                {
                    data.SkipBytes(2);
                    input(data, true, current);
                }

                if (fecPacket.Flag == Fec.typeData || fecPacket.Flag == Fec.typeParity)
                {
                    var byteBufs = _fecDecode.decode(fecPacket);
                    if (byteBufs != null)
                    {
                        foreach (var IByteBuffer in byteBufs)
                        {
                            input(IByteBuffer, false, current);
                            IByteBuffer.Release();
                        }
                    }
                }
            }
            else
            {
                input(data, true, current);
            }
        }

        private void input(IByteBuffer data, bool regular, long current)
        {
            int ret = kcp.input(data, regular, current);
            switch (ret)
            {
                case -1:
                    throw new IOException("No enough bytes of head");
                case -2:
                    throw new IOException("No enough bytes of data");
                case -3:
                    throw new IOException("Mismatch cmd");
                case -4:
                    throw new IOException("Conv inconsistency");
                default:
                    break;
            }
        }


        /**
         * Sends a IByteBuffer.
         *
         * @param buf
         * @throws IOException
         */
        public void send(IByteBuffer buf)
        {
            int ret = kcp.send(buf);
            switch (ret)
            {
                case -2:
                    throw new IOException("Too many fragments");
                default:
                    break;
            }
        }

        /**
         * The size of the first msg of the kcp.
         *
         * @return The size of the first msg of the kcp, or -1 if none of msg
         */
        public int peekSize()
        {
            return kcp.peekSize();
        }

        /**
         * Returns {@code true} if there are bytes can be received.
         *
         * @return
         */
        protected internal bool canRecv()
        {
            return kcp.canRecv();
        }


        /**
         * Returns {@code true} if the kcp can send more bytes.
         *
         * @param curCanSend last state of canSend
         * @return {@code true} if the kcp can send more bytes
         */
        protected internal bool canSend(bool curCanSend)
        {
            int max = kcp.SndWnd * 2;

            int waitSnd = kcp.waitSnd();
            if (curCanSend)
            {
                return waitSnd < max;
            }
            else
            {
                int threshold = Math.Max(1, max / 2);
                return waitSnd < threshold;
            }
        }

        /**
         * Udpates the kcp.
         *
         * @param current current time in milliseconds
         * @return the next time to update
         */
        public long update(long current)
        {
            kcp.update(current);
            long nextTsUp = check(current);

            setTsUpdate(nextTsUp);
            return nextTsUp;
        }

        protected internal long flush(long current)
        {
            return kcp.flush(false, current);
        }

        /**
         * Determines when should you invoke udpate.
         *
         * @param current current time in milliseconds
         * @return
         * @see Kcp#check(long)
         */
        protected internal long check(long current)
        {
            return kcp.check(current);
        }

        /**
         * Returns {@code true} if the kcp need to flush.
         *
         * @return {@code true} if the kcp need to flush
         */
        protected internal bool checkFlush()
        {
            return kcp.checkFlush();
        }

        /**
         * Sets params of nodelay.
         *
         * @param nodelay  {@code true} if nodelay mode is enabled
         * @param interval protocol internal work interval, in milliseconds
         * @param resend   fast retransmission mode, 0 represents off by default, 2 can be set (2 ACK spans will result
         *                 in direct retransmission)
         * @param nc       {@code true} if turn off flow control
         */
        protected internal void nodelay(bool nodelay, int interval, int resend, bool nc)
        {
            kcp.initNodelay(nodelay, interval, resend, nc);
        }

        /**
         * Returns conv of kcp.
         *
         * @return conv of kcp
         */
        public int getConv()
        {
            return kcp.Conv;
        }

        /**
         * Set the conv of kcp.
         *
         * @param conv the conv of kcp
         */
        public void setConv(int conv)
        {
            kcp.Conv = conv;
        }

        /**
         * Returns {@code true} if and only if nodelay is enabled.
         *
         * @return {@code true} if and only if nodelay is enabled
         */
        public bool isNodelay()
        {
            return kcp.Nodelay;
        }

        /**
         * Sets whether enable nodelay.
         *
         * @param nodelay {@code true} if enable nodelay
         * @return this object
         */
        public Ukcp setNodelay(bool nodelay)
        {
            kcp.Nodelay = nodelay;
            return this;
        }

        /**
         * Returns update interval.
         *
         * @return update interval
         */
        public int getInterval()
        {
            return kcp.Interval;
        }

        /**
         * Sets update interval
         *
         * @param interval update interval
         * @return this object
         */
        public Ukcp setInterval(int interval)
        {
            kcp.setInterval(interval);
            return this;
        }

        /**
         * Returns the fastresend of kcp.
         *
         * @return the fastresend of kcp
         */
        public int getFastResend()
        {
            return kcp.Fastresend;
        }

        /**
         * Sets the fastresend of kcp.
         *
         * @param fastResend
         * @return this object
         */
        public Ukcp setFastResend(int fastResend)
        {
            kcp.Fastresend=fastResend;
            return this;
        }

        public bool isNocwnd()
        {
            return kcp.Nocwnd;
        }

        public Ukcp setNocwnd(bool nocwnd)
        {
            kcp.Nocwnd = nocwnd;
            return this;
        }

        public int getMinRto()
        {
            return kcp.RxMinrto;
        }

        public Ukcp setMinRto(int minRto)
        {
            kcp.RxMinrto = minRto;
            return this;
        }

        public int getMtu()
        {
            return kcp.Mtu;
        }

        public Ukcp setMtu(int mtu)
        {
            kcp.setMtu(mtu);
            return this;
        }

        public bool isStream()
        {
            return kcp.Stream;
        }

        public Ukcp setStream(bool stream)
        {
            kcp.Stream=stream;
            return this;
        }

        public int getDeadLink()
        {
            return kcp.DeadLink;
        }

        public Ukcp setDeadLink(int deadLink)
        {
            kcp.DeadLink = deadLink;
            return this;
        }

        /**
         * Sets the {@link ByteBufAllocator} which is used for the kcp to allocate buffers.
         *
         * @param allocator the allocator is used for the kcp to allocate buffers
         * @return this object
         */
        public Ukcp setByteBufAllocator(IByteBufferAllocator allocator)
        {
            kcp.ByteBufAllocator = allocator;
            return this;
        }

        public bool isAutoSetConv()
        {
            return kcp.AutoSetConv;
        }

        public Ukcp setAutoSetConv(bool autoSetConv)
        {
            kcp.AutoSetConv =autoSetConv ;
            return this;
        }

        public int waitSnd()
        {
            return kcp.waitSnd();
        }

        public int getRcvWnd()
        {
            return kcp.RcvWnd;
        }


        protected internal bool isFastFlush()
        {
            return fastFlush;
        }

        public Ukcp setFastFlush(bool fastFlush)
        {
            this.fastFlush = fastFlush;
            return this;
        }


        internal void read(IByteBuffer iByteBuffer)
        {
            if (_recieveList.TryEnqueue(iByteBuffer))
            {
                notifyReadEvent();
            }
            else
            {
                iByteBuffer.Release();
                Console.WriteLine("conv "+kcp.Conv+" recieveList is full");
            }
        }

        /**
         * 主动发消息使用
         * 线程安全的
         * @param IByteBuffer 发送后需要手动释放
         * @return
         */
        public bool writeKcpMessage(IByteBuffer byteBuffer)
        {
            byteBuffer = byteBuffer.RetainedDuplicate();

            if (!_sendList.TryEnqueue(byteBuffer))
            {
                Console.WriteLine("conv "+kcp.Conv+" sendList is full");
                byteBuffer.Release();
                return false;
            }
            notifyWriteEvent();
            return true;
        }


        /**
         * 发送udp消息
         */
        public void writeUdpMessage(IByteBuffer byteBuffer)
        {
            byteBuffer = byteBuffer.RetainedDuplicate();
            //写入头信息
            var head = PooledByteBufferAllocator.Default.DirectBuffer(1);
            head.WriteByte(UDP_PROTOCOL);
            var content = Unpooled.WrappedBuffer(head, byteBuffer);
            var user   = kcp.User as User;
            var temp = new DatagramPacket(content,user.LocalAddress, user.RemoteAddress);
            user.Channel.WriteAndFlushAsync(temp);
        }

        public IMessageExecutor getDisruptorSingleExecutor()
        {
            return _iMessageExecutor;
        }

        /**
         * 主动关闭连接调用
         */
        public void notifyCloseEvent()
        {
            this._iMessageExecutor.execute(new CloseTask(this));
        }

        private void notifyReadEvent()
        {
            var recieveTask = RecieveTask.New(this);
            this._iMessageExecutor.execute(recieveTask);
        }

        protected internal void notifyWriteEvent()
        {
            SendTask sendTask = SendTask.New(this);
            this._iMessageExecutor.execute(sendTask);
        }


        protected internal long getTsUpdate()
        {
            return tsUpdate;
        }


        protected internal Ukcp setTsUpdate(long tsUpdate)
        {
            this.tsUpdate = tsUpdate;
            return this;
        }




        protected internal KcpListener getKcpListener()
        {
            return _kcpListener;
        }

        public bool isActive()
        {
            return active;
        }


        protected internal void close()
        {
            _kcpListener.handleClose(this);
            active = false;
        }

        public void release()
        {
            kcp.State = -1;
            kcp.release();

            IByteBuffer buffer  = null;
            while (_sendList.TryDequeue(out buffer))
            {
                buffer.Release();
            }

            while (_recieveList.TryDequeue(out buffer))
            {
                buffer.Release();
            }
            Console.WriteLine("关闭");
            _fecEncode?.release();
            _fecDecode?.release();
        }



        public User user()
        {
            return (User) kcp.User;
        }

        public Ukcp user(User user)
        {
            kcp.User = user;
            return this;
        }

        public MpscArrayQueue<IByteBuffer> SendList => _sendList;

        public MpscArrayQueue<IByteBuffer> RecieveList => _recieveList;

        public ChannelConfig ChannelConfig => _channelConfig;

        public long currentMs()
        {
            return kcp.currentMs();
        }

        protected internal KcpListener KcpListener => _kcpListener;

        protected internal IMessageExecutor IMessageExecutor => _iMessageExecutor;
    }
}