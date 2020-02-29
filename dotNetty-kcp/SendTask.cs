using System;
using System.IO;
using DotNetty.Buffers;
using DotNetty.Common;
using dotNetty_kcp.thread;

namespace dotNetty_kcp
{
    public class SendTask : ITask
    {
        private Ukcp kcp;

        private static readonly ThreadLocalPool<SendTask> RECYCLER =
            new ThreadLocalPool<SendTask>(handle => new SendTask(handle));

        private readonly ThreadLocalPool.Handle recyclerHandle;

        private SendTask(ThreadLocalPool.Handle recyclerHandle)
        {
            this.recyclerHandle = recyclerHandle;
        }

        public static SendTask New(Ukcp kcp)
        {
            SendTask recieveTask = RECYCLER.Take();
            recieveTask.kcp = kcp;
            return recieveTask;
        }


        public override void execute()
        {
            try
            {
                //查看连接状态
                if (!kcp.isActive())
                {
                    return;
                }

                //从发送缓冲区到kcp缓冲区
                var queue = kcp.SendList;
                IByteBuffer byteBuf = null;
                while (kcp.canSend(false))
                {
                    if (!queue.TryDequeue(out byteBuf))
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
            kcp = null;
            recyclerHandle.Release(this);
        }
    }
}