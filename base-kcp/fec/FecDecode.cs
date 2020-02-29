using System;
using System.Collections.Generic;
using System.Linq;
using DotNetty.Buffers;
using fec;

namespace fec.fec
{
    public class FecDecode
    {
        // queue size limit
        private readonly int rxlimit;
        private readonly int dataShards;

        private int parityShards;

        /** dataShards+parityShards **/
        private readonly int shardSize;

        // ordered receive queue
        private readonly List<FecPacket> rx;

        private readonly IByteBuffer[] decodeCache;

        /**标记是否已经缓存了**/
        private readonly bool[] flagCache;

        private readonly ReedSolomon codec;

        private readonly IByteBuffer zeros;


        public FecDecode(int rxlimit, ReedSolomon codec, int mtu)
        {
            this.rxlimit = rxlimit;
            this.dataShards = codec.getDataShardCount();
            this.parityShards = codec.getParityShardCount();
            this.shardSize = dataShards + parityShards;

            if (dataShards <= 0 || parityShards <= 0)
            {
                throw new Exception("dataShards and parityShards can not less than 0");
            }

            if (rxlimit < dataShards + parityShards)
            {
                throw new Exception("");
            }

            this.codec = codec;
            this.decodeCache = new IByteBuffer[this.shardSize];
            this.flagCache = new bool[this.shardSize];
            this.rx = new List<FecPacket>(rxlimit);

            zeros = PooledByteBufferAllocator.Default.DirectBuffer(mtu);
            zeros.WriteBytes(new byte[mtu]);
        }


        public List<IByteBuffer> decode(FecPacket pkt)
        {
            if (pkt.Flag == Fec.typeParity)
            {
                Snmp.snmp.FECParityShards++;
            }
            else
            {
                Snmp.snmp.FECDataShards++;
            }

            int n = rx.Count - 1;
            int insertIdx = 0;
            for (int i = n; i >= 0; i--)
            {
                //去重
                if (pkt.Seqid == rx[i].Seqid)
                {
                    Snmp.snmp.FECRepeatDataShards++;
                    pkt.release();
                    return null;
                }

                if (pkt.Seqid > rx[i].Seqid)
                {
                    // insertion
                    insertIdx = i + 1;
                    break;
                }
            }

            //插入 rx中
            if (insertIdx == n + 1)
            {
                this.rx.Add(pkt);
            }
            else
            {
                rx.Insert(insertIdx, pkt);
            }

            //所有消息列表中的第一个包
            // shard range for current packet
            long shardBegin = pkt.Seqid - pkt.Seqid % shardSize;
            long shardEnd = shardBegin + shardSize - 1;

            //rx数组中的第一个包
            // max search range in ordered queue for current shard
            int searchBegin = (int) (insertIdx - pkt.Seqid % shardSize);
            if (searchBegin < 0)
            {
                searchBegin = 0;
            }

            int searchEnd = searchBegin + shardSize - 1;
            if (searchEnd >= rx.Count)
            {
                searchEnd = rx.Count - 1;
            }

            List<IByteBuffer> result = null;
            if (searchEnd - searchBegin + 1 >= dataShards)
            {
                //当前包组的已收到的包数量
                int numshard = 0;
                //当前包组中属于数据包的数量
                int numDataShard = 0;
                //搜到第一个包在搜索行中的位置
                int first = 0;
                //收到的最大包的字节长度
                int maxlen = 0;

                // zero cache
                IByteBuffer[] shards = decodeCache;
                bool[] shardsflag = flagCache;
                for (int i = 0; i < shards.Length; i++)
                {
                    shards[i] = null;
                    shardsflag[i] = false;
                }

                for (int i = searchBegin; i <= searchEnd; i++)
                {
                    FecPacket fecPacket = rx[i];
                    long seqid = fecPacket.Seqid;
                    if (seqid > shardEnd)
                        break;
                    if (seqid < shardBegin)
                        continue;
                    shards[(int) (seqid % shardSize)] = fecPacket.Data;
                    shardsflag[(int) (seqid % shardSize)] = true;
                    numshard++;
                    if (fecPacket.Flag == Fec.typeData)
                    {
                        numDataShard++;
                    }

                    if (numshard == 1)
                    {
                        first = i;
                    }

                    if (fecPacket.Data.ReadableBytes> maxlen)
                    {
                        maxlen = fecPacket.Data.ReadableBytes;
                    }
                }

                if (numDataShard == dataShards)
                {
                    freeRange(first, numshard, rx);
                }
                else if (numshard >= dataShards)
                {
                    for (int i = 0; i < shards.Length; i++)
                    {
                        IByteBuffer shard = shards[i];
                        //如果数据不存在 用0填充起来
                        if (shard == null)
                        {
                            shards[i] = zeros.Copy(0, maxlen);
                            shards[i].SetWriterIndex(maxlen);
                            continue;
                        }

                        int left = maxlen - shard.ReadableBytes;
                        if (left > 0)
                        {
                            shard.WriteBytes(this.zeros, left);
                            zeros.ResetReaderIndex();
//                            zeros.resetReaderIndex();
                        }
                    }

                    codec.decodeMissing(shards, shardsflag, 0, maxlen);
                    result = new List<IByteBuffer>(this.dataShards);
                    for (int i = 0; i < shardSize; i++)
                    {
                        if (shardsflag[i])
                        {
                            continue;
                        }

                        IByteBuffer byteBufs = shards[i];
                        //释放构建的parityShards内存
                        if (i >= dataShards)
                        {
                            byteBufs.Release();
                            continue;
                        }

                        int packageSize = byteBufs.ReadShort();
                        if (byteBufs.ReadableBytes < packageSize)
                        {
////                            System.out.println("bytebuf长度: " + byteBufs.writerIndex() + " 读出长度" + packageSize);
//                            byte[] bytes = new byte[byteBufs.writerIndex()];
//                            byteBufs.getBytes(0, bytes);
//                            for (byte aByte :
//                            bytes) {
//                                System.out.print("[" + aByte + "] ");
//                            }
                            Snmp.snmp.FECErrs++;
                        }
                        else
                        {
                            Snmp.snmp.FECRecovered++;
                        }

                        //去除fec头标记的消息体长度2字段
                        byteBufs = byteBufs.Slice(Fec.fecDataSize, packageSize);
                        //int packageSize =byteBufs.readUnsignedShort();
                        //byteBufs = byteBufs.slice(0,packageSize);
                        result.Add(byteBufs);
                        Snmp.snmp.FECRecovered++;
                        //int packageSize =byteBufs.getUnsignedShort(0);
                        ////判断长度
                        //if(byteBufs.writerIndex()-Fec.fecHeaderSizePlus2>=packageSize&&packageSize>0)
                        //{
                        //    byteBufs = byteBufs.slice(Fec.fecHeaderSizePlus2,packageSize);
                        //    result.add(byteBufs);
                        //    Snmp.snmp.FECRecovered.incrementAndGet();
                        //}else{
                        //    System.out.println("bytebuf长度: "+byteBufs.writerIndex()+" 读出长度"+packageSize);
                        //    byte[] bytes = new byte[byteBufs.writerIndex()];
                        //    byteBufs.getBytes(0,bytes);
                        //    for (byte aByte : bytes) {
                        //        System.out.print("["+aByte+"] ");
                        //    }
                        //    Snmp.snmp.FECErrs.incrementAndGet();
                        //}
                    }

                    freeRange(first, numshard, rx);
                }
            }

            if (rx.Count> rxlimit)
            {
                if (rx[0].Flag == Fec.typeData)
                {
                    Snmp.snmp.FECShortShards++;
                }

                freeRange(0, 1, rx);
            }

            return result;
        }


        public void release(){
            this.parityShards=0;
            foreach (var fecPacket in this.rx)
            {
                fecPacket?.release();
            }
            this.zeros.Release();
        }
        /**
       * 1，回收first后n个bytebuf
       * 2，将q的first到first+n之间的数据移除掉
       * 3，将尾部的n个数据的data清空
       * 4，返回开头到尾部n个数组的对象
       *
       * @param first
       * @param n
       * @param q
       */
        private static void freeRange(int first,int n,List<FecPacket> q){
            for (int i = first; i < first + n; i++) {
                q[i].release();
            }
            //copy(q[first:], q[first+n:])
            for (int i = first; i < q.Count; i++) {
                int index = i+n;
                if(index==q.Count)
                    break;
                q[i]=q[index];
            }
            //for (int i = 0; i < n; i++) {
            //    q.get(q.size()-1-i).setData(null);
            //}
            for (int i = 0; i < n; i++) {
                q.RemoveAt(q.Count-1);
            }
        }
    }
}