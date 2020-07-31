using System;
using DotNetty.Common.Utilities;

namespace dotNetty_kcp.thread
{
    /**
     * netty的实现轮实现，在unity环境下测试会导致cpu跑到50%
     * 服务器端使用不错
     */
    public class HashedWheelScheduleThread:IScheduleThread
    {
        
        private readonly HashedWheelTimer _hashedWheelTimer = new HashedWheelTimer(TimeSpan.FromMilliseconds(1),512,-1 );

        public void schedule(IScheduleTask scheduleTask,TimeSpan timeSpan)
        {
            _hashedWheelTimer.NewTimeout(scheduleTask,timeSpan);
        }

        public void stop()
        {
            _hashedWheelTimer.StopAsync();
        }
    }
}