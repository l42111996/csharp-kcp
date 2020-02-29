using System.Collections.Generic;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using kcp4game;

namespace doNetty_tcp.code
{
    public class TcpProtoDecode<T> : ByteToMessageDecoder
    {
        private readonly IProtoDecodeEncode<T> _protoDecodeEncode;

        public TcpProtoDecode(IProtoDecodeEncode<T> protoDecodeEncode)
        {
            _protoDecodeEncode = protoDecodeEncode;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (!input.IsReadable())
                return;
            var message = _protoDecodeEncode.decode(input);
            output.Add(message);
        }
    }
}