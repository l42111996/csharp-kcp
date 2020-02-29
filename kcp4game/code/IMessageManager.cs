using System;
using DotNetty.Buffers;

namespace doNetty_tcp.code
{
    public interface IMessageManager<T>
    {
        IHandler<T> getHandler(object messageId);

        void addHandler(IHandler<T> handler);
    }
}