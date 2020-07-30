using System;
using dotNetty_kcp.thread;

namespace base_kcp
{
    public class MessageExecutorTest:ITask
    {
        private static IMessageExecutor _messageExecutor;

        public int i;

        public static long start = KcpUntils.currentMs();

        private static int index = 0;

        public MessageExecutorTest(int i)
        {
            this.i = i;
        }

        public static int addIndex;

        public static void en()
        {
            int i = 0;
            while (true)
            {
                var queueTest = new MessageExecutorTest(i);
                if (_messageExecutor.execute(queueTest))
                {
                    i++;
                }
            }
        }


        public void execute()
        {
            long now = KcpUntils.currentMs();
            if (now - start > 1000)
            {
                Console.WriteLine("i "+(i-index) +"time "+(now-start));
                index = i;
                start = now;
            }
        }

        public static void test()
        {
            _messageExecutor = new DistuptorMessageExecutor();
            _messageExecutor.start();
            en();
        }
    }
}