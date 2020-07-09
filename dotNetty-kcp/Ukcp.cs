using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using base_kcp;
using DotNetty.Buffers;
using DotNetty.Transport.Channels.Sockets;
using dotNetty_kcp.thread;
using DotNetty.Common.Internal;
using fec;
using fec.fec;

namespace dotNetty_kcp
{
    public class Ukcp
    {
        public const int HEADER_CRC = 4,  HEADER_NONCESIZE = 16;

        private readonly Kcp _kcp;

        private bool fastFlush = true;

        private long tsUpdate = -1;

        private bool _active;

        private readonly FecEncode _fecEncode;
        private readonly FecDecode _fecDecode;

        private readonly ConcurrentQueue<IByteBuffer> _writeQueue;

        private readonly MpscArrayQueue<IByteBuffer> _readQueue;

        private readonly IMessageExecutor _iMessageExecutor;

        private readonly KcpListener _kcpListener;

        private readonly long _timeoutMillis;
        
        private readonly AtomicBoolean _readProcessing = new AtomicBoolean();
        
        private readonly AtomicBoolean _writeProcessing = new AtomicBoolean();

        private readonly bool _crc32Check;


        /**
         * 上次收到完整消息包时间
         * 用于心跳检测
         **/
        internal long LastRecieveTime { get; set; } = KcpUntils.currentMs();



        /**
         * Creates a new instance.
         *
         * @param output output for kcp
         */
        public Ukcp(KcpOutput output, KcpListener kcpListener, IMessageExecutor iMessageExecutor,
            ReedSolomon reedSolomon, ChannelConfig channelConfig)
        {
            this._timeoutMillis = channelConfig.TimeoutMillis;
            this._crc32Check = channelConfig.Crc32Check;
            this._kcp = new Kcp(channelConfig.Conv, output);
            this._active = true;
            this._kcpListener = kcpListener;
            this._iMessageExecutor = iMessageExecutor;
            //默认2<<11   可以修改
            _writeQueue = new ConcurrentQueue<IByteBuffer>();
                // <IByteBuffer>(2<<10);
            _readQueue = new MpscArrayQueue<IByteBuffer>(2<<10);
            //recieveList = new SpscLinkedQueue<>();
            int headerSize = 0;


            //init crc32
            if(channelConfig.Crc32Check){
                var kcpOutput = _kcp.Output;
                kcpOutput = new Crc32OutPut(kcpOutput,headerSize);
                _kcp.Output=kcpOutput;
                headerSize+=HEADER_CRC;
            }

            //init fec
            if (reedSolomon != null)
            {
                var kcpOutput = _kcp.Output;
                _fecEncode = new FecEncode(headerSize, reedSolomon, channelConfig.Mtu);
                _fecDecode = new FecDecode(3 * reedSolomon.getTotalShardCount(), reedSolomon, channelConfig.Mtu);
                kcpOutput = new FecOutPut(kcpOutput, _fecEncode);
                _kcp.Output = kcpOutput;
                headerSize += Fec.fecHeaderSizePlus2;
            }

            _kcp.setReserved(headerSize);
            initKcpConfig(channelConfig);
        }


        private void initKcpConfig(ChannelConfig channelConfig)
        {
            _kcp.initNodelay(channelConfig.Nodelay, channelConfig.Interval, channelConfig.Fastresend,
                channelConfig.Nocwnd);
            _kcp.SndWnd = channelConfig.Sndwnd;
            _kcp.RcvWnd = channelConfig.Rcvwnd;
            _kcp.Mtu = channelConfig.Mtu;
            _kcp.Stream = channelConfig.Stream;
            _kcp.AckNoDelay = channelConfig.AckNoDelay;
            _kcp.setAckMaskSize(channelConfig.AckMaskSize);
            fastFlush = channelConfig.FastFlush;
        }


        /**
         * Receives ByteBufs.
         *
         * @param bufList received IByteBuffer will be add to the list
         */
        protected internal void receive(List<IByteBuffer> bufList)
        {
            _kcp.recv(bufList);
        }


        protected internal IByteBuffer mergeReceive()
        {
            return _kcp.mergeRecv();
        }


        internal void input(IByteBuffer data, long current)
        {
//            _lastRecieveTime = KcpUntils.currentMs();
            Snmp.snmp.InPkts++;
            Snmp.snmp.InBytes += data.ReadableBytes;
            if (_crc32Check)
            {
                long checksum = data.ReadUnsignedIntLE();
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
            int ret = _kcp.input(data, regular, current);
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
        internal void send(IByteBuffer buf)
        {
            int ret = _kcp.send(buf);
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
        internal int peekSize()
        {
            return _kcp.peekSize();
        }

        /**
         * Returns {@code true} if there are bytes can be received.
         *
         * @return
         */
        protected internal bool canRecv()
        {
            return _kcp.canRecv();
        }


        /**
         * Returns {@code true} if the kcp can send more bytes.
         *
         * @param curCanSend last state of canSend
         * @return {@code true} if the kcp can send more bytes
         */
        protected internal bool canSend(bool curCanSend)
        {
            int max = _kcp.SndWnd * 2;

            int waitSnd = _kcp.waitSnd();
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
        internal long update(long current)
        {
            _kcp.update(current);
            long nextTsUp = check(current);

            setTsUpdate(nextTsUp);
            return nextTsUp;
        }

        protected internal long flush(long current)
        {
            return _kcp.flush(false, current);
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
            return _kcp.check(current);
        }

        /**
         * Returns {@code true} if the kcp need to flush.
         *
         * @return {@code true} if the kcp need to flush
         */
        protected internal bool checkFlush()
        {
            return _kcp.checkFlush();
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
            _kcp.initNodelay(nodelay, interval, resend, nc);
        }

        /**
         * Returns conv of kcp.
         *
         * @return conv of kcp
         */
        public int getConv()
        {
            return _kcp.Conv;
        }

        /**
         * Set the conv of kcp.
         *
         * @param conv the conv of kcp
         */
        public void setConv(int conv)
        {
            _kcp.Conv = conv;
        }

        /**
         * Returns {@code true} if and only if nodelay is enabled.
         *
         * @return {@code true} if and only if nodelay is enabled
         */
        public bool isNodelay()
        {
            return _kcp.Nodelay;
        }

        /**
         * Sets whether enable nodelay.
         *
         * @param nodelay {@code true} if enable nodelay
         * @return this object
         */
        public Ukcp setNodelay(bool nodelay)
        {
            _kcp.Nodelay = nodelay;
            return this;
        }

        /**
         * Returns update interval.
         *
         * @return update interval
         */
        public int getInterval()
        {
            return _kcp.Interval;
        }

        /**
         * Sets update interval
         *
         * @param interval update interval
         * @return this object
         */
        public Ukcp setInterval(int interval)
        {
            _kcp.setInterval(interval);
            return this;
        }

        /**
         * Returns the fastresend of kcp.
         *
         * @return the fastresend of kcp
         */
        public int getFastResend()
        {
            return _kcp.Fastresend;
        }

        /**
         * Sets the fastresend of kcp.
         *
         * @param fastResend
         * @return this object
         */
        public Ukcp setFastResend(int fastResend)
        {
            _kcp.Fastresend=fastResend;
            return this;
        }

        public bool isNocwnd()
        {
            return _kcp.Nocwnd;
        }

        public Ukcp setNocwnd(bool nocwnd)
        {
            _kcp.Nocwnd = nocwnd;
            return this;
        }

        public int getMinRto()
        {
            return _kcp.RxMinrto;
        }

        public Ukcp setMinRto(int minRto)
        {
            _kcp.RxMinrto = minRto;
            return this;
        }

        public int getMtu()
        {
            return _kcp.Mtu;
        }

        public Ukcp setMtu(int mtu)
        {
            _kcp.setMtu(mtu);
            return this;
        }

        public bool isStream()
        {
            return _kcp.Stream;
        }

        public Ukcp setStream(bool stream)
        {
            _kcp.Stream=stream;
            return this;
        }

        public int getDeadLink()
        {
            return _kcp.DeadLink;
        }

        public Ukcp setDeadLink(int deadLink)
        {
            _kcp.DeadLink = deadLink;
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
            _kcp.ByteBufAllocator = allocator;
            return this;
        }

        public int waitSnd()
        {
            return _kcp.waitSnd();
        }

        public int getRcvWnd()
        {
            return _kcp.RcvWnd;
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
            if (_readQueue.TryEnqueue(iByteBuffer))
            {
                notifyReadEvent();
            }
            else
            {
                iByteBuffer.Release();
                Console.WriteLine("conv "+_kcp.Conv+" recieveList is full");
            }
        }

        /**
         * 主动发消息使用
         * 线程安全的
         * @param IByteBuffer 发送后需要手动释放
         * @return
         */
        public bool writeMessage(IByteBuffer byteBuffer)
        {
            byteBuffer = byteBuffer.RetainedDuplicate();

            _writeQueue.Enqueue(byteBuffer);
            // if (!_writeQueue.TryEnqueue(byteBuffer))
            // {
            //     Console.WriteLine("conv "+kcp.Conv+" sendList is full");
            //     byteBuffer.Release();
            //     return false;
            // }
            notifyWriteEvent();
            return true;
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
            if (_readProcessing.CompareAndSet(false, true))
            {
                var readTask = ReadTask.New(this);
                _iMessageExecutor.execute(readTask);
            }
        }

        protected internal void notifyWriteEvent()
        {
            if (_writeProcessing.CompareAndSet(false, true))
            {
                var writeTask = WriteTask.New(this);
                _iMessageExecutor.execute(writeTask);
            }
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
            return _active;
        }


        protected internal void close()
        {
            _kcpListener.handleClose(this);
            _active = false;
        }

        internal void release()
        {
            _kcp.State = -1;
            _kcp.release();

            IByteBuffer buffer  = null;
            while (_writeQueue.TryDequeue(out buffer))
            {
                buffer.Release();
            }

            while (_readQueue.TryDequeue(out buffer))
            {
                buffer.Release();
            }
            Console.WriteLine("关闭");
            _fecEncode?.release();
            _fecDecode?.release();
        }



        public User user()
        {
            return (User) _kcp.User;
        }

        public Ukcp user(User user)
        {
            _kcp.User = user;
            return this;
        }

        internal ConcurrentQueue<IByteBuffer> WriteQueue => _writeQueue;

        internal MpscArrayQueue<IByteBuffer> ReadQueue => _readQueue;

        public long TimeoutMillis => _timeoutMillis;


        internal long currentMs()
        {
            return _kcp.currentMs();
        }

        
        internal AtomicBoolean ReadProcessing => _readProcessing;

        internal AtomicBoolean WriteProcessing => _writeProcessing;

        protected internal KcpListener KcpListener => _kcpListener;

        internal IMessageExecutor IMessageExecutor => _iMessageExecutor;
    }
}