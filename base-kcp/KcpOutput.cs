using DotNetty.Buffers;

namespace base_kcp
{
    public interface KcpOutput
    {
        void outPut(IByteBuffer data, Kcp kcp);
    }
}