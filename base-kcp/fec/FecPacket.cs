using DotNetty.Buffers;
using DotNetty.Common;

namespace fec.fec
{
    public class FecPacket
    {
        private long seqid;
        private int flag;
        private IByteBuffer data;

        private readonly ThreadLocalPool.Handle recyclerHandle;


        private static readonly ThreadLocalPool<FecPacket> fecPacketRecycler =
            new ThreadLocalPool<FecPacket>(handle => new FecPacket(handle));

        private FecPacket(ThreadLocalPool.Handle recyclerHandle)
        {
            this.recyclerHandle = recyclerHandle;
        }


        public static FecPacket newFecPacket(IByteBuffer byteBuf)
        {
            FecPacket pkt = fecPacketRecycler.Take();
            pkt.seqid = byteBuf.ReadUnsignedIntLE();
            pkt.flag = byteBuf.ReadUnsignedShortLE();
            pkt.data = byteBuf.RetainedSlice(byteBuf.ReaderIndex, byteBuf.Capacity - byteBuf.ReaderIndex);
            pkt.data.SetWriterIndex(byteBuf.ReadableBytes);
            return pkt;
        }


        public void release()
        {
            this.seqid = 0;
            this.flag = 0;
            this.data.Release();
            this.data = null;
            recyclerHandle.Release(this);
        }

        public long Seqid
        {
            get => seqid;
        }

        public int Flag
        {
            get => flag;
        }

        public IByteBuffer Data
        {
            get => data;
        }
    }
}