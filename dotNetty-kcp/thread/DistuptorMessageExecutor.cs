using System.Threading.Tasks;

namespace dotNetty_kcp.thread
{
    /**
     *
     * 单生产者单消费者  500W tps
     *
     */
    public class DistuptorMessageExecutor:AbstratcMessageExecutor
    {
        private RingBuffer<ITask> _ringBuffer;

        private const int MAX_QUEUE_SIZE = 2 << 10;


        public override void start()
        {
            _ringBuffer = new RingBuffer<ITask>(MAX_QUEUE_SIZE);
            base.start();
        }


        public override bool isFull()
        {
            return _ringBuffer.Count == MAX_QUEUE_SIZE;
        }

        protected override bool isEmpty()
        {
            return _ringBuffer.Count == 0;
        }

        protected override bool TryDequeue(out ITask task)
        {
            return _ringBuffer.TryDequeue(out task);
        }

        protected override bool TryEnqueue(ITask task)
        {
            return _ringBuffer.tryEnqueue(task);
        }
    }
}