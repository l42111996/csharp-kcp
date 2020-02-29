using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DotNetty.Common;

namespace base_kcp
{
    public static class ObjectPool
    {

        private static readonly ConcurrentDictionary<Type, ThreadLocalPool<AbstractPoolObject>> recycler =
            new ConcurrentDictionary<Type, ThreadLocalPool<AbstractPoolObject>>();

        public static T New<T>(Type type) where T:AbstractPoolObject
        {
            if (!recycler.TryGetValue(type, out var pool))
            {
                pool = Register<T>(type);
            }
            var t = pool.Take();
            return (T) t;
        }

        private static ThreadLocalPool<AbstractPoolObject> Register<T>(Type type) where T:AbstractPoolObject
        {
            var pool = new ThreadLocalPool<AbstractPoolObject>(handle =>
            {
                var t = Activator.CreateInstance(type) as T;
                t.RecyclerHandle = handle;
                return t;
            });
            recycler.TryAdd(type,pool);
            return pool;
        }
    }
}