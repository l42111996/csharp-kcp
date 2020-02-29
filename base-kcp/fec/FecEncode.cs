using System;
using DotNetty.Buffers;
using fec;

namespace fec.fec
{
    public class FecEncode
    {
        /**消息包长度**/
        private readonly int dataShards;

        /**冗余包长度**/
        private readonly int parityShards;

        /** dataShards+parityShards **/
        private int shardSize;

        //Protect Against Wrapped Sequence numbers
        private readonly long paws;

        // next seqid
        private long next;

        //count the number of datashards collected
        private int shardCount;

        // record maximum data length in datashard
        private int maxSize;

        // FEC header offset
        private readonly int headerOffset;

        // FEC payload offset
        private readonly int payloadOffset;

        //用完需要手动release
        private readonly IByteBuffer[] shardCache;
        private readonly IByteBuffer[] encodeCache;

        private readonly IByteBuffer zeros;

        private readonly ReedSolomon codec;

        public FecEncode(int headerOffset, ReedSolomon codec, int mtu)
        {
            this.dataShards = codec.getDataShardCount();
            this.parityShards = codec.getParityShardCount();
            this.shardSize = this.dataShards + this.parityShards;
            //this.paws = (Integer.MAX_VALUE/shardSize - 1) * shardSize;
            this.paws = 0xffffffffL / shardSize * shardSize;
            this.headerOffset = headerOffset;
            this.payloadOffset = headerOffset + Fec.fecHeaderSize;
            this.codec = codec;
            this.shardCache = new IByteBuffer[shardSize];
            this.encodeCache = new IByteBuffer[parityShards];
            zeros = PooledByteBufferAllocator.Default.DirectBuffer(mtu);
            zeros.WriteBytes(new byte[mtu]);
        }

        /**
     *
     *  使用方法:
     *  1，入bytebuf后 把bytebuf发送出去,并释放bytebuf
     *  2，判断返回值是否为null，如果不为null发送出去并释放它
     *
     *  headerOffset +6字节fectHead +  2字节bodylenth(lenth-headerOffset-6)
     *
     * 1,对数据写入头标记为数据类型  markData
     * 2,写入消息长度
     * 3,获得缓存数据中最大长度，其他的缓存进行扩容到同样长度
     * 4,去掉头长度，进行fec编码
     * 5,对冗余字节数组进行标记为fec  makefec
     * 6,返回完整长度
     *
     *  注意: 传入的bytebuf如果需要释放在传入后手动释放。
     *  返回的bytebuf 也需要自己释放
     * @param byteBuf
     * @return
     */
        public IByteBuffer[] encode(IByteBuffer byteBuf)
        {
            markData(byteBuf, headerOffset);
            int sz = byteBuf.WriterIndex;
            byteBuf.SetShort(payloadOffset, sz - headerOffset - Fec.fecHeaderSizePlus2);
            this.shardCache[shardCount] = byteBuf.RetainedDuplicate();
            this.shardCount++;
            if (sz > this.maxSize)
            {
                this.maxSize = sz;
            }

            if (shardCount != dataShards)
            {
                return null;
            }

            //填充parityShards
            for (int i = 0; i < parityShards; i++)
            {
                IByteBuffer parityByte = PooledByteBufferAllocator.Default.DirectBuffer(this.maxSize);
                shardCache[i + dataShards] = parityByte;
                encodeCache[i] = parityByte;
                markParity(parityByte, headerOffset);
                parityByte.SetWriterIndex(this.maxSize);
            }

            //按着最大长度不足补充0
            for (var i = 0; i < this.dataShards; i++)
            {
                var shard = shardCache[i];
                var left = this.maxSize - shard.WriterIndex;
                if (left <= 0)
                    continue;
                //是否需要扩容  会出现吗？？
                //if(shard.capacity()<this.maxSize){
                //    ByteBuf newByteBuf = ByteBufAllocator.DEFAULT.buffer(this.maxSize);
                //    newByteBuf.writeBytes(shard);
                //    shard.release();
                //    shard = newByteBuf;
                //    shardCache[i] = shard;
                //}
                shard.WriteBytes(zeros, left);
                zeros.SetReaderIndex(0);
            }

            codec.encodeParity(shardCache, payloadOffset, this.maxSize - payloadOffset);
            //释放dataShards
            for (int i = 0; i < dataShards; i++)
            {
                this.shardCache[i].Release();
                this.shardCache[i] = null;
            }

            this.shardCount = 0;
            this.maxSize = 0;
            return this.encodeCache;
        }


        public void release()
        {
            this.shardSize = 0;
            this.next = 0;
            this.shardCount = 0;
            this.maxSize = 0;
            for (int i = 0; i < dataShards; i++)
            {
                var byteBuf = this.shardCache[i];
                byteBuf?.Release();
            }
            zeros.Release();
        }


        private void markData(IByteBuffer byteBuf, int offset)
        {
            byteBuf.SetIntLE(offset,  (int)this.next);
            byteBuf.SetShortLE(offset + 4, Fec.typeData);
            this.next++;
        }

        private void markParity(IByteBuffer byteBuf, int offset)
        {
            byteBuf.SetIntLE(offset, (int) this.next);
            byteBuf.SetShortLE(offset + 4, Fec.typeParity);
            //if(next==this.paws){
            //    next=0;
            //}else{
            //    next++;
            //}
            this.next = (this.next + 1) % this.paws;
            //if(next+1>=this.paws) {
            //    this.next++;
            //    //this.next = (this.next + 1) % this.paws;
            //}
        }
    }
}