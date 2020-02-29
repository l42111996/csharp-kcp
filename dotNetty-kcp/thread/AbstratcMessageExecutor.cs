using System.Threading;
using base_kcp;

namespace dotNetty_kcp.thread
{
    public abstract class AbstratcMessageExecutor:IMessageExecutor
    {
        private Thread _thread;

        private volatile bool shutdown;
        private volatile bool close;

        private readonly object _gate = new object();
        private static int id;


        /**
 * 启动消息处理器
 */
        public virtual void start()
        {
            _thread = new Thread(run) {Name = "ThreadMessageExecutor-" + id++};
            _thread.Start();
        }

        /****
         *
         */
        public void stop(bool stopImmediately)
        {
            if (shutdown)
                return;
            shutdown = true;
            if (stopImmediately)
            {
                close = true;
                lock (_gate)
                {
                    Monitor.Pulse(_gate);
                }

                return;
            }
            while (!isEmpty())
            {
                Thread.Sleep(1);
            }
            close = true;
            lock (_gate)
            {
                Monitor.Pulse(_gate);
            }
        }

        public abstract bool isFull();

        protected abstract bool isEmpty();

        protected abstract bool TryDequeue(out ITask task);

        protected abstract bool TryEnqueue( ITask task);


        private void run()
        {
            while (!close)
            {
                if (TryDequeue(out var task))
                {
                    task.execute();
                    continue;
                }
                lock (_gate)
                {
                    Monitor.Wait(_gate);
                }
            }
        }




        public bool execute(ITask iTask)
        {
            if (shutdown)
                return false;
            bool result = TryEnqueue(iTask);
            lock (_gate)
            {
                Monitor.Pulse(_gate);
            }
            return result;
        }

    }
}