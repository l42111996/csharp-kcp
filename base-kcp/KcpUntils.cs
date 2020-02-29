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

        /**时间轮机制  测试2万定时任务10秒执行一次  耗费单核 70%**/
        private static readonly HashedWheelTimer HashedWheelTimer = new HashedWheelTimer(TimeSpan.FromMilliseconds(1),512,-1 );

        public static void scheduleHashedWheel(ITimerTask timerTask, TimeSpan timeSpan){
            HashedWheelTimer.NewTimeout(timerTask,timeSpan);
        }
    }
}