using System.Threading;

namespace dotNetty_kcp.thread
{
    public class AtomicBoolean
    {
        private int _value;
        
        public AtomicBoolean()
            : this(false) {

        }
        
        /// Creates a new <c>AtomicBoolean</c> instance with the initial value provided.
        /// </summary>
        public AtomicBoolean(bool value) {
            _value = value ? 1 : 0;
        }

        /// <summary>
        /// This method returns the current value.
        /// </summary>
        /// <returns>
        /// The <c>bool</c> value to be accessed atomically.
        /// </returns>
        public bool Get() {
            return _value != 0;
        }

        /// <summary>
        /// This method sets the current value atomically.
        /// </summary>
        /// <param name="value">
        /// The new value to set.
        /// </param>
        public void Set(bool value) {
            Interlocked.Exchange(ref _value, value ? 1 : 0);
        }

        /// <summary>
        /// This method atomically sets the value and returns the original value.
        /// </summary>
        /// <param name="value">
        /// The new value.
        /// </param>
        /// <returns>
        /// The value before setting to the new value.
        /// </returns>
        public bool GetAndSet(bool value) {
            return Interlocked.Exchange(ref _value, value ? 1 : 0) != 0;
        }

        /// <summary>
        /// Atomically sets the value to the given updated value if the current value <c>==</c> the expected value.
        /// </summary>
        /// <param name="expected">
        /// The value to compare against.
        /// </param>
        /// <param name="result">
        /// The value to set if the value is equal to the <c>expected</c> value.
        /// </param>
        /// <returns>
        /// <c>true</c> if the comparison and set was successful. A <c>false</c> indicates the comparison failed.
        /// </returns>
        public bool CompareAndSet(bool expected, bool result) {
            int e = expected ? 1 : 0;
            int r = result ? 1 : 0;
            return Interlocked.CompareExchange(ref _value, r, e) == e;
        }

        /// <summary>
        /// This operator allows an implicit cast from <c>AtomicBoolean</c> to <c>int</c>.
        /// </summary>
        public static implicit operator bool(AtomicBoolean value) {
            return value.Get();
        }
    }
}