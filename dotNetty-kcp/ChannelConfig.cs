using base_kcp;
using fec.fec;

namespace dotNetty_kcp
{
    public class ChannelConfig
    {
        private bool nodelay;
        private int interval = Kcp.IKCP_INTERVAL;
        private int fastresend;
        private bool nocwnd;
        private int sndwnd = Kcp.IKCP_WND_SND;
        private int rcvwnd = Kcp.IKCP_WND_RCV;
        private int mtu = Kcp.IKCP_MTU_DEF;
        private int minRto = Kcp.IKCP_RTO_MIN;
        //超时时间 超过一段时间没收到消息断开连接
        private long timeoutMillis;
        //TODO 可能有bug还未测试
        private bool stream;

        //下面为新增参数
        private int fecDataShardCount;
        private int fecParityShardCount;
        //收到包立刻回传ack包
        private bool ackNoDelay = false;
        //发送包立即调用flush 延迟低一些  cpu增加  如果interval值很小 建议关闭该参数
        private bool fastFlush = true;
        //crc32校验
        private bool crc32Check = false;

        //增加ack包回复成功率 填 /8/16/32
        private int ackMaskSize = 0;
        /**使用conv确定一个channel 还是使用 socketAddress确定一个channel**/
        private bool useConvChannel=false;
        /**预留长度**/
        private int reserved;

        /**在协议的第一字节标记为kcp对象 用于与非可靠udp整合**/
        private bool kcpTag;

        public void initNodelay(bool nodelay, int interval, int resend, bool nc){
            this.nodelay = nodelay;
            this.interval = interval;
            this.fastresend = resend;
            this.nocwnd=nc;
        }


        public bool KcpTag
        {
            get => kcpTag;
            set
            {
                if (value)
                {
                    reserved += Fec.fecHeaderSizePlus2;
                }
                kcpTag = value;
            }
        }

        public int Conv { get; set; }

        public bool Nodelay
        {
            get => nodelay;
            set => nodelay = value;
        }

        public int Interval
        {
            get => interval;
            set => interval = value;
        }

        public int Fastresend
        {
            get => fastresend;
            set => fastresend = value;
        }

        public bool Nocwnd
        {
            get => nocwnd;
            set => nocwnd = value;
        }

        public int Sndwnd
        {
            get => sndwnd;
            set => sndwnd = value;
        }

        public int Rcvwnd
        {
            get => rcvwnd;
            set => rcvwnd = value;
        }

        public int Mtu
        {
            get => mtu;
            set => mtu = value;
        }

        public int MinRto
        {
            get => minRto;
            set => minRto = value;
        }

        public long TimeoutMillis
        {
            get => timeoutMillis;
            set => timeoutMillis = value;
        }

        public bool Stream
        {
            get => stream;
            set => stream = value;
        }

        public int FecDataShardCount
        {
            get => fecDataShardCount;
            set
            {
                if (value > 0)
                {
                    reserved += Fec.fecHeaderSizePlus2;
                }
                fecDataShardCount = value;
            }
        }

        public int FecParityShardCount
        {
            get => fecParityShardCount;
            set => fecParityShardCount = value;
        }

        public bool AckNoDelay
        {
            get => ackNoDelay;
            set => ackNoDelay = value;
        }

        public bool FastFlush
        {
            get => fastFlush;
            set => fastFlush = value;
        }

        public bool Crc32Check
        {
            get => crc32Check;
            set
            {
                if (value)
                {
                    reserved += Ukcp.HEADER_CRC;
                }
                crc32Check = value;
            }
        }

        public int AckMaskSize
        {
            get => ackMaskSize;
            set => ackMaskSize = value;
        }


        public int Reserved
        {
            get => reserved;
        }

        public bool UseConvChannel
        {
            get => useConvChannel;
            set => useConvChannel = value;
        }
    }
}