using System.Collections.Generic;
using DotNetty.Common;

namespace dotNetty_kcp
{
    public class CodecOutputList<T>:List<T>
    {

        const int DefaultInitialCapacity =16;

        static readonly ThreadLocalPool<CodecOutputList<T>> Pool = new ThreadLocalPool<CodecOutputList<T>>(handle => new CodecOutputList<T>(handle));

        readonly ThreadLocalPool.Handle returnHandle;

        CodecOutputList(ThreadLocalPool.Handle returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        public static CodecOutputList<T> NewInstance() => NewInstance(DefaultInitialCapacity);

        public static CodecOutputList<T> NewInstance(int minCapacity)
        {
            CodecOutputList<T> ret = Pool.Take();
            if (ret.Capacity < minCapacity)
            {
                ret.Capacity = minCapacity;
            }
            return ret;

        }

        public void Return()
        {
            this.Clear();
            this.returnHandle.Release(this);
        }
    }
}