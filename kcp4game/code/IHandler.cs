using DotNetty.Buffers;
using kcp4game;

namespace doNetty_tcp.code
{
    public interface IHandler<T>:IProtoDecodeEncode<T>
    {
        void handler<T>(Message<T> message);

        object getMessageId();
    }
}