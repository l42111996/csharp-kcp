using System;

namespace dotNetty_kcp.thread
{
    public interface IExecutorPool
    {
         IMessageExecutor CreateMessageExecutor();

         void stop(bool stopImmediately);

         IMessageExecutor GetAutoMessageExecutor();

    }
}