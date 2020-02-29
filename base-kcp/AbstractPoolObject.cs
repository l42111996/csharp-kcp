using System;
using DotNetty.Common;

namespace base_kcp
{
    public abstract class AbstractPoolObject
    {
        private ThreadLocalPool.Handle _recyclerHandle;

        internal ThreadLocalPool.Handle RecyclerHandle
        {
            set => _recyclerHandle = value;
        }

        public virtual void Release()
        {
            _recyclerHandle.Release(this);
        }
    }
}