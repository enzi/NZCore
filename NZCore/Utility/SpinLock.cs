using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;

// publicized SpinLock from Unity.Entities
namespace NZCore
{
    [GenerateTestsForBurstCompatibility]
    [StructLayout(LayoutKind.Explicit, Size = 64)] // prevent false sharing
    public struct SpinLock
    {
        [FieldOffset(0)]
        private int _lock;

        /// <summary>
        /// Continually spin until the lock can be acquired.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        
        public void Acquire()
        {
            while (true)
            {
                // Optimistically assume the lock is free on the first try.
                if (Volatile.Read(ref _lock) == 0 && 
                    Interlocked.CompareExchange(ref _lock, 1, 0) == 0)
                {
                    return;
                }

                while (Volatile.Read(ref _lock) != 0)
                {
                    Unity.Burst.Intrinsics.Common.Pause();
                }
            }
        }

        /// <summary>
        /// Try to acquire the lock and immediately return without spinning.
        /// </summary>
        /// <returns><see langword="true"/> if the lock was acquired, <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAcquire()
        {
            // First do a memory load (read) to check if lock is free in order to prevent unnecessary cache misses.
            return Volatile.Read(ref _lock) == 0 && Interlocked.CompareExchange(ref _lock, 1, 0) == 0;
        }

        /// <summary>
        /// Release the lock.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            Volatile.Write(ref _lock, 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLocked()
        {
            return Volatile.Read(ref _lock) != 0;
        }
    }
}