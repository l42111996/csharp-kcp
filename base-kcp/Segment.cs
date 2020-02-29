using DotNetty.Buffers;
using DotNetty.Common;

namespace base_kcp
{
    public class Segment
    {
        private readonly ThreadLocalPool.Handle recyclerHandle;

        /**会话id**/

        /**命令**/
        private byte cmd;

        /**message中的segment分片ID（在message中的索引，由大到小，0表示最后一个分片）**/
        private short frg;

        /**剩余接收窗口大小(接收窗口大小-接收队列大小)**/
        private int wnd;

        /**message发送时刻的时间戳**/
        private long ts;

        /**message分片segment的序号**/
        private long sn;

        /**待接收消息序号(接收滑动窗口左端)**/
        private long una;

        /**下次超时重传的时间戳**/
        private long resendts;

        /**该分片的超时重传等待时间**/
        private int rto;

        /**收到ack时计算的该分片被跳过的累计次数，即该分片后的包都被对方收到了，达到一定次数，重传当前分片**/
        private int fastack;

        /***发送分片的次数，每发送一次加一**/
        private int xmit;

        private long ackMask;

        private IByteBuffer data;

        private int ackMaskSize;

        private static readonly ThreadLocalPool<Segment> RECYCLER =
            new ThreadLocalPool<Segment>(handle =>
            {
                return new Segment(handle);
            });

        private Segment(ThreadLocalPool.Handle recyclerHandle)
        {
            this.recyclerHandle =recyclerHandle;
        }

        public void recycle(bool releaseBuf) {
            Conv = 0;
            cmd = 0;
            frg = 0;
            wnd = 0;
            ts = 0;
            sn = 0;
            una = 0;
            resendts = 0;
            rto = 0;
            fastack = 0;
            xmit = 0;
            ackMask=0;
            if (releaseBuf) {
                data.Release();
            }
            data = null;
            recyclerHandle.Release(this);
        }

        public static Segment createSegment(IByteBufferAllocator byteBufAllocator, int size) {
            Segment seg = RECYCLER.Take();
            if (size == 0) {
                seg.data = byteBufAllocator.DirectBuffer(0, 0);
            } else {
                seg.data = byteBufAllocator.DirectBuffer(size);
            }
            return seg;
        }


        public static Segment createSegment(IByteBuffer buf) {
            Segment seg = RECYCLER.Take();
            seg.data = buf;
            return seg;
        }


        public int Conv { get; set; }

        public byte Cmd
        {
            get => cmd;
            set => cmd = value;
        }

        public short Frg
        {
            get => frg;
            set => frg = value;
        }

        public int Wnd
        {
            get => wnd;
            set => wnd = value;
        }

        public long Ts
        {
            get => ts;
            set => ts = value;
        }

        public long Sn
        {
            get => sn;
            set => sn = value;
        }

        public long Una
        {
            get => una;
            set => una = value;
        }

        public long Resendts
        {
            get => resendts;
            set => resendts = value;
        }

        public int Rto
        {
            get => rto;
            set => rto = value;
        }

        public int Fastack
        {
            get => fastack;
            set => fastack = value;
        }

        public int Xmit
        {
            get => xmit;
            set => xmit = value;
        }

        public long AckMask
        {
            get => ackMask;
            set => ackMask = value;
        }

        public IByteBuffer Data
        {
            get => data;
            set => data = value;
        }

        public int AckMaskSize
        {
            get => ackMaskSize;
            set => ackMaskSize = value;
        }
    }
}