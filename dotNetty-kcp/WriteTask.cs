using System;
using System.IO;
using DotNetty.Buffers;
using DotNetty.Common;
using dotNetty_kcp.thread;

namespace dotNetty_kcp
{
    public class WriteTask : ITask
    {
        private Ukcp kcp;

        private static readonly ThreadLocalPool<WriteTask> RECYCLER =
            new ThreadLocalPool<WriteTask>(handle => new WriteTask(handle));

        private readonly ThreadLocalPool.Handle recyclerHandle;

        private WriteTask(ThreadLocalPool.Handle recyclerHandle)
        {
            this.recyclerHandle = recyclerHandle;
        }

        public static WriteTask New(Ukcp kcp)
        {
            WriteTask recieveTask = RECYCLER.Take();
            recieveTask.kcp = kcp;
            return recieveTask;
        }


        public void execute()
        {
            try
            {
                //查看连接状态
                if (!kcp.isActive())
                {
                    return;
                }

                //从发送缓冲区到kcp缓冲区
                var writeQueue = kcp.WriteQueue;
                IByteBuffer byteBuf = null;
                while (kcp.canSend(false))
                {
                    if (!writeQueue.TryDequeue(out byteBuf))
                    {
                        break;
                    }
                    try
                    {
                        this.kcp.send(byteBuf);
                        byteBuf.Release();
                    }
                    catch (IOException e)
                    {
                        kcp.getKcpListener().handleException(e, kcp);
                        return;
                    }
                }

                //如果有发送 则检测时间
                if (kcp.canSend(false) && (!kcp.checkFlush() || !kcp.isFastFlush()))
                {
                    return;
                }

                long now = kcp.currentMs();
                long next = kcp.flush(now);
                //System.out.println(next);
                //System.out.println("耗时"+(System.currentTimeMillis()-now));
                kcp.setTsUpdate(now + next);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                release();
            }
        }

        private void release()
        {
            kcp.WriteProcessing.Set(false);
            kcp = null;
            recyclerHandle.Release(this);
        }
    }
}