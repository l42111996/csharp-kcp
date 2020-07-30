using System;
using DotNetty.Common.Utilities;

namespace base_kcp
{
    public class KcpUntils
    {
        public static long currentMs()
        {
            return DateTime.Now.Ticks/TimeSpan.TicksPerMillisecond;
        }
    }
}