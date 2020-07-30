using System;
using DotNetty.Common.Utilities;

namespace dotNetty_kcp.thread
{
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