using System;

namespace dotNetty_kcp.thread
{
    public interface IMessageExecutor
    {
        /**
	 * 启动消息处理器
	 */
        void start();

        /**
         * 停止消息处理器
         * shutdownRightNow false该方法会堵塞当前队列全部执行完再关闭
         */
        void stop(bool stopImmediately);

        /**
         * 判断队列是否已经达到上限了
         * @return
         */
        bool isFull();

        /**
         * 执行任务
         * 注意: 如果线程等于当前线程 则直接执行  如果非当前线程放进队列
         *
         * @param iTask
         */
        bool execute(ITask iTask);


    }
}