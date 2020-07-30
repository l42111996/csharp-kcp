using System;
using base_kcp;
using DotNetty.Common.Utilities;
using dotNetty_kcp.thread;

namespace dotNetty_kcp
{
    public class ScheduleTask:IScheduleTask,ITask
    {
        private readonly IMessageExecutor _imessageExecutor;

        private readonly IChannelManager _channelManager;

        private readonly Ukcp _ukcp;

        private readonly IScheduleThread _scheduleThread;


        public ScheduleTask(IChannelManager channelManager, Ukcp ukcp, IScheduleThread scheduleThread)
        {
            _imessageExecutor = ukcp.IMessageExecutor;
            _channelManager = channelManager;
            _ukcp = ukcp;
            _scheduleThread = scheduleThread;
        }

        public void execute()
        {
            try
            {
                long now = _ukcp.currentMs();
                //判断连接是否关闭
                if (_ukcp.TimeoutMillis != 0 && now - _ukcp.TimeoutMillis > _ukcp.LastRecieveTime) {
                    _ukcp.internalClose();
                }
                if(!_ukcp.isActive()){
                    var user = _ukcp.user();
                    //抛回网络线程处理连接删除
                    user.Channel.EventLoop.Execute(() => {_channelManager.del(_ukcp);});
                    _ukcp.release();
                    return;
                }
                long timeLeft = _ukcp.getTsUpdate()-now;
                //判断执行时间是否到了
                if(timeLeft>0){
                    //System.err.println(timeLeft);
                    _scheduleThread.schedule(this, TimeSpan.FromMilliseconds(timeLeft));
                    return;
                }

                //long start = System.currentTimeMillis();
                long next = _ukcp.flush(now);
                //System.err.println(next);
                //System.out.println("耗时  "+(System.currentTimeMillis()-start));
                _scheduleThread.schedule(this, TimeSpan.FromMilliseconds(next));

                //检测写缓冲区 如果能写则触发写事件
                if(!_ukcp.WriteQueue.IsEmpty&&_ukcp.canSend(false)
                ){
                    _ukcp.notifyWriteEvent();
                }
            } catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        public void Run(ITimeout timeout)
        {
            _imessageExecutor.execute(this);
        }

        public void Run()
        {
            _imessageExecutor.execute(this);
        }
    }
}