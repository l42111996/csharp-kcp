using System.Collections.Generic;
using System.Threading;
using base_kcp;

namespace dotNetty_kcp.thread
{
    public class ExecutorPool:IExecutorPool
    {
        private List<IMessageExecutor> _messageExecutors = new List<IMessageExecutor>();

        private int atomicIndex;

        public IMessageExecutor CreateMessageExecutor()
        {
            IMessageExecutor executor = new ThreadMessageExecutor();
            executor.start();
            _messageExecutors.Add(executor);
            return executor;
        }

        public void stop(bool stopImmediately)
        {
            foreach (var messageExecutor in _messageExecutors)
            {
                messageExecutor.stop(stopImmediately);

            }
        }

        public IMessageExecutor GetAutoMessageExecutor()
        {
            Interlocked.Increment(ref atomicIndex);
            return _messageExecutors[atomicIndex % _messageExecutors.Count];
        }
    }
}