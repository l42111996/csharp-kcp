using System.Collections.Generic;
using base_kcp;
using DotNetty.Buffers;

namespace kcp4game
{
    public class Message<T>
    {
        /**
         * Ukcp.KCP_PROTOCOL
         */
        private int protocolType;

        private int messageId;

        private T body;

        private Message()
        {
        }

        public static Message<T> newMessage()
        {
            return new Message<T>();
        }

        public int ProtocolType
        {
            get => protocolType;
            set => protocolType = value;
        }

        public int MessageId
        {
            get => messageId;
            set => messageId = value;
        }

        public T Body
        {
            get => body;
            set => body = value;
        }
    }
}