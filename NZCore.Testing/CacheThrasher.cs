// <copyright project="NZCore.Testing" file="CacheThrasher.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;

namespace NZCore
{
    /// <summary>
    /// Streams a buffer bigger than the last level cache through every core to evict L1/L2/L3.
    /// Call it between tests when a warm cache would make a measurement meaningless.
    /// </summary>
    public static unsafe class CacheThrasher
    {
        private const int CacheLineSize = 64;
        private const int BytesPerBatch = 256 * 1024;
        private const int MainThreadBytes = 8 * 1024 * 1024;
        private const int FallbackBufferSize = 256 * 1024 * 1024;
        private const long MaxBufferSize = 512L * 1024 * 1024;

        private static byte* _buffer;
        private static int _allocatedSize;

        /// <summary>
        /// Size of the eviction buffer in bytes. Defaults to 4x the largest L3 the OS reports, because the
        /// sweep is split across the workers and a single cache complex therefore only streams its share of it.
        /// Assigning a different size reallocates on the next <see cref="Thrash"/> call.
        /// </summary>
        public static int BufferSize { get; set; } = GetDefaultBufferSize();

        static CacheThrasher()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        /// <summary>
        /// Evicts the caches of every core. Costs a few milliseconds, the first call additionally pays
        /// for faulting in the buffer.
        /// </summary>
        public static void Thrash()
        {
            var size = Math.Max(BufferSize / BytesPerBatch, 1) * BytesPerBatch;

            if (_buffer == null || _allocatedSize != size)
            {
                Dispose();
                _buffer = (byte*)UnsafeUtility.Malloc(size, CacheLineSize, Allocator.Persistent);
                _allocatedSize = size;
            }

            var batches = size / BytesPerBatch;
            var job = new ThrashJob { Buffer = _buffer };

            // one batch per dispatch so every worker gets work, which is what kills the per core L1/L2
            // and both L3 instances instead of just the ones the calling thread sits on
            job.Schedule(batches, 1).Complete();

            // the main thread only helps out with the batches above when the job system happens to hand
            // it one, so give its own L1/L2 a guaranteed sweep
            job.Run(Math.Min(batches, MainThreadBytes / BytesPerBatch));
        }

        /// <summary>
        /// Frees the eviction buffer. Called automatically on domain reload and on editor quit.
        /// </summary>
        public static void Dispose()
        {
            if (_buffer == null)
            {
                return;
            }

            UnsafeUtility.Free(_buffer, Allocator.Persistent);
            _buffer = null;
            _allocatedSize = 0;
        }

        [BurstCompile]
        private struct ThrashJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public byte* Buffer;

            public void Execute(int index)
            {
                var ptr = (ulong*)(Buffer + (long)index * BytesPerBatch);
                var count = BytesPerBatch / sizeof(ulong);
                var step = CacheLineSize / sizeof(ulong);

                ulong sum = 0;

                for (int i = 0; i < count; i += step)
                {
                    sum += ptr[i];
                }

                // dirties one line per batch and keeps the loads above from being optimized away
                ptr[0] = sum;
            }
        }

        private static int GetDefaultBufferSize()
        {
            const string cpuRoot = "/sys/devices/system/cpu";

            long largestL3 = 0;

            try
            {
                if (Directory.Exists(cpuRoot))
                {
                    foreach (var cpu in Directory.GetDirectories(cpuRoot, "cpu*"))
                    {
                        var cacheDir = Path.Combine(cpu, "cache");

                        if (!Directory.Exists(cacheDir))
                        {
                            continue;
                        }

                        foreach (var index in Directory.GetDirectories(cacheDir, "index*"))
                        {
                            var levelFile = Path.Combine(index, "level");
                            var sizeFile = Path.Combine(index, "size");

                            if (!File.Exists(levelFile) || !File.Exists(sizeFile) || File.ReadAllText(levelFile).Trim() != "3")
                            {
                                continue;
                            }

                            largestL3 = Math.Max(largestL3, ParseCacheSize(File.ReadAllText(sizeFile).Trim()));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // no sysfs (non linux editor), fall back to a size that covers a typical desktop L3
                largestL3 = 0;
            }

            return largestL3 <= 0 ? FallbackBufferSize : (int)Math.Min(largestL3 * 4, MaxBufferSize);
        }

        private static long ParseCacheSize(string size)
        {
            long multiplier = 1;

            if (size.EndsWith("K"))
            {
                multiplier = 1024;
                size = size[..^1];
            }
            else if (size.EndsWith("M"))
            {
                multiplier = 1024 * 1024;
                size = size[..^1];
            }

            return long.TryParse(size, out var value) ? value * multiplier : 0;
        }
    }
}
