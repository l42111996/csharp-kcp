using System;
using DotNetty.Buffers;
using DotNetty.Common;
using dotNetty_kcp.thread;

namespace dotNetty_kcp
{
    public class RecieveTask : ITask
    {
        private Ukcp kcp;

        private static readonly ThreadLocalPool<RecieveTask> RECYCLER =
            new ThreadLocalPool<RecieveTask>(handle => new RecieveTask(handle));

        private readonly ThreadLocalPool.Handle recyclerHandle;

        private RecieveTask(ThreadLocalPool.Handle recyclerHandle)
        {
            this.recyclerHandle = recyclerHandle;
        }

        public static RecieveTask New(Ukcp kcp)
        {
            RecieveTask recieveTask = RECYCLER.Take();
            recieveTask.kcp = kcp;
            return recieveTask;
        }

        public override void execute()
        {
            CodecOutputList<IByteBuffer> bufList = null;
            try {
                //Thread.sleep(1000);
                //查看连接状态
                if (!kcp.isActive()) {
                    return;
                }
                bool hasKcpMessage = false;
                long current = kcp.currentMs();
                var recieveList = kcp.RecieveList;
                IByteBuffer byteBuf = null;
                for (;;)
                {
                    if (!recieveList.TryDequeue(out byteBuf))
                    {
                        break;
                    }
                    //区分udp还是kcp消息
                    if (kcp.ChannelConfig.KcpTag && byteBuf.ReadByte() == Ukcp.UDP_PROTOCOL)
                    {
                        readBytebuf(byteBuf, current,Ukcp.UDP_PROTOCOL);
                    }
                    else
                    {
                        hasKcpMessage = true;
                        kcp.input(byteBuf, current);
                        byteBuf.Release();
                    }
                }
                if (!hasKcpMessage) {
                    return;
                }
                if (kcp.isStream()) {
                    while (kcp.canRecv()) {
                        if (bufList == null) {
                            bufList = CodecOutputList<IByteBuffer>.NewInstance();
                        }
                        kcp.receive(bufList);
                    }
                    int size = bufList.Count;
                    for (int i = 0; i < size; i++)
                    {
                        byteBuf = bufList[i];
                        readBytebuf(byteBuf,current,Ukcp.KCP_PROTOCOL);
                    }
                } else {
                    while (kcp.canRecv()) {
                        IByteBuffer recvBuf = kcp.mergeReceive();
                        readBytebuf(recvBuf,current,Ukcp.KCP_PROTOCOL);
                    }
                }
                //判断写事件
                if (!kcp.SendList.IsEmpty&&kcp.canSend(false)) {
                    kcp.notifyWriteEvent();
                }
            } catch (Exception e) {
                kcp.KcpListener.handleException(e,kcp);
                Console.WriteLine(e);
            } finally {
                release();
                bufList?.Return();
            }
        }


        private void readBytebuf(IByteBuffer buf,long current,int protocolType)
        {
            kcp.LastRecieveTime = current;
            try
            {
                kcp.getKcpListener().handleReceive(buf, kcp, protocolType);
            }
            catch (Exception throwable)
            {
                kcp.getKcpListener().handleException(throwable, kcp);
            }
            finally
            {
                buf.Release();
            }
        }

        private void release()
        {
            kcp = null;
            recyclerHandle.Release(this);
        }
    }
}