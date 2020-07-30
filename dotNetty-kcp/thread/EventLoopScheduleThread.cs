using System;
using DotNetty.Transport.Channels;

namespace dotNetty_kcp.thread
{
    public class EventLoopScheduleThread : IScheduleThread
    {
        private readonly IEventLoop _eventLoop = new SingleThreadEventLoop();


        public void schedule(IScheduleTask scheduleTask, TimeSpan timeSpan)
        {
            _eventLoop.Schedule(scheduleTask, timeSpan);
        }

        public void stop()
        {
            if (_eventLoop.IsShuttingDown)
            {
                return;
            }
            _eventLoop.ShutdownGracefullyAsync();
        }
    }
}