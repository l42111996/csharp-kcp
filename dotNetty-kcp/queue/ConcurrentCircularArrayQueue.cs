using DotNetty.Common.Internal;
using DotNetty.Common.Utilities;

namespace base_kcp
{
    public abstract class ConcurrentCircularArrayQueue<T> : ConcurrentCircularArrayQueueL0Pad<T>
        where T : class
    {
        protected long Mask;
        protected readonly T[] Buffer;

        protected ConcurrentCircularArrayQueue(int capacity)
        {
            int actualCapacity = IntegerExtensions.RoundUpToPowerOfTwo(capacity);
            this.Mask = actualCapacity - 1;
            // pad data on either end with some empty slots.
            this.Buffer = new T[actualCapacity + RefArrayAccessUtil.RefBufferPad * 2];
        }

        /// <summary>
        /// Calculates an element offset based on a given array index.
        /// </summary>
        /// <param name="index">The desirable element index.</param>
        /// <returns>The offset in bytes within the array for a given index.</returns>
        protected long CalcElementOffset(long index) => RefArrayAccessUtil.CalcElementOffset(index, this.Mask);

        /// <summary>
        /// A plain store (no ordering/fences) of an element to a given offset.
        /// </summary>
        /// <param name="offset">Computed via <see cref="CalcElementOffset"/>.</param>
        /// <param name="e">A kitty.</param>
        protected void SpElement(long offset, T e) => RefArrayAccessUtil.SpElement(this.Buffer, offset, e);

        /// <summary>
        /// An ordered store(store + StoreStore barrier) of an element to a given offset.
        /// </summary>
        /// <param name="offset">Computed via <see cref="CalcElementOffset"/>.</param>
        /// <param name="e">An orderly kitty.</param>
        protected void SoElement(long offset, T e) => RefArrayAccessUtil.SoElement(this.Buffer, offset, e);

        /// <summary>
        /// A plain load (no ordering/fences) of an element from a given offset.
        /// </summary>
        /// <param name="offset">Computed via <see cref="CalcElementOffset"/>.</param>
        /// <returns>The element at the offset.</returns>
        protected T LpElement(long offset) => RefArrayAccessUtil.LpElement(this.Buffer, offset);

        /// <summary>
        /// A volatile load (load + LoadLoad barrier) of an element from a given offset.
        /// </summary>
        /// <param name="offset">Computed via <see cref="CalcElementOffset"/>.</param>
        /// <returns>The element at the offset.</returns>
        protected T LvElement(long offset) => RefArrayAccessUtil.LvElement(this.Buffer, offset);

        public override void Clear()
        {
            while (this.TryDequeue(out T _) || !this.IsEmpty)
            {
                // looping
            }
        }

        public int Capacity() => (int)(this.Mask + 1);
    }

    public abstract class ConcurrentCircularArrayQueueL0Pad<T> : AbstractQueue<T>
    {
#pragma warning disable 169 // padded reference
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p30, p31, p32, p33, p34, p35, p36, p37;
#pragma warning restore 169
    }
}