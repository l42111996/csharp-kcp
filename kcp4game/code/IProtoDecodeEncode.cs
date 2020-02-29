using DotNetty.Buffers;
using kcp4game;

namespace doNetty_tcp.code
{
    public interface IProtoDecodeEncode<T>
    {
        int CalculationLenth(Message<T> t);

        void encode(IByteBuffer byteBuffer,Message<T> t);

        Message<T> decode(IByteBuffer byteBuffer);
    }
}