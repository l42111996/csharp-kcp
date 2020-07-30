using dotNetty_kcp.thread;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;

namespace dotNetty_kcp
{
    public interface IScheduleTask:ITimerTask,IRunnable
    {
        
    }
}