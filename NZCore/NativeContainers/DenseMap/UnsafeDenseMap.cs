// <copyright project="NZCore" file="UnsafeDenseMap.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using NZCore.Helper;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace NZCore.NativeContainers.DenseMap
{
    public unsafe struct UnsafeDenseMap
    {
        public const sbyte EmptyBucket = -127;
        public static readonly v128 EmptyBucketVector = new(EmptyBucket);
    }

    public unsafe struct UnsafeDenseMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public int Count { get; private set; }
        public uint Size => arrayLength;

        private const sbyte _tombstone = -126;
        private const uint _goldenRatio = 0x9E3779B9; //2654435769;


        private uint _length;
        private readonly double _loadFactor;
        private uint _maxLookupsBeforeResize;
        private byte _shift;
        private uint _lengthMinusOne;

        [NativeDisableUnsafePtrRestriction] private Entry* _entries;
        [NativeDisableUnsafePtrRestriction] private sbyte* _metadata;
        private uint arrayLength;

        internal AllocatorManager.AllocatorHandle Allocator;

        public UnsafeDenseMap(uint length, double loadFactor, AllocatorManager.AllocatorHandle allocator)
        {
            Allocator = allocator;

            _length = length;
            _loadFactor = loadFactor;

            if (loadFactor > 0.9)
            {
                _loadFactor = 0.9;
            }

            if (_length < 16)
            {
                _length = 16;
            }
            else if (math.ispow2(_length))
            {
                _length = length;
            }
            else
            {
                _length = BitHelper.RoundToPowerOf2(_length);
            }

            _maxLookupsBeforeResize = (uint)(_length * loadFactor);
            //_comparer

            _shift = 32;
            _shift = (byte)(_shift - math.log2(_length));

            arrayLength = _length + 16;

            var memBlockSize = arrayLength * UnsafeUtility.SizeOf<Entry>() + arrayLength;

            var block = (byte*)Memory.Unmanaged.Allocate(memBlockSize, JobsUtility.CacheLineSize, allocator.ToAllocator);

            //_entries = (Entry*) Memory.Unmanaged.Allocate(arrayLength * UnsafeUtility.SizeOf<Entry>(), JobsUtility.CacheLineSize, allocator.ToAllocator);
            //_metadata = (sbyte*) Memory.Unmanaged.Allocate(arrayLength, JobsUtility.CacheLineSize, allocator.ToAllocator);

            _entries = (Entry*)block;
            _metadata = (sbyte*)(block + arrayLength * UnsafeUtility.SizeOf<Entry>());

            for (int i = 0; i < arrayLength; i++)
            {
                _metadata[i] = UnsafeDenseMap.EmptyBucket;
            }

            _lengthMinusOne = _length - 1;

            Count = 0;
        }

        public static UnsafeDenseMap<TKey, TValue>* Create(uint length, double loadFactor, AllocatorManager.AllocatorHandle allocator)
        {
            UnsafeDenseMap<TKey, TValue>* map = allocator.Allocate(default(UnsafeDenseMap<TKey, TValue>), 1);
            *map = new UnsafeDenseMap<TKey, TValue>(length, loadFactor, allocator);
            return map;
        }

        public void Dispose()
        {
            Memory.Unmanaged.Free(_entries, Allocator.ToAllocator);
            //Memory.Unmanaged.Free(_metadata, Allocator.ToAllocator);
            //UnsafeUtility.Free(_entries, Allocator.ToAllocator);
            //UnsafeUtility.Free(_metadata, Allocator.ToAllocator);
        }

        public bool Emplace(TKey key, TValue value)
        {
            if (Count >= _maxLookupsBeforeResize)
            {
                Resize();
            }

            var hashcode = (uint)key.GetHashCode();
            var h2 = H2(hashcode);
            //var target = new v128(UnsafeUtility.As<uint, sbyte>(ref h2));
            var target = X86.Sse2.set1_epi8(UnsafeUtility.As<uint, sbyte>(ref h2));
            uint index = (_goldenRatio * hashcode) >> _shift;
            uint jumpDistance = 0;

            while (true)
            {
                var source = X86.Sse2.load_si128(_metadata + index);
                //uint mask = X86.Sse2.cmpeq_epi32(source, target).ExtractMostSignificantBits<sbyte>();
                var mask = CompareGetMask(source, target);

                while (mask != 0)
                {
                    int bitPos = math.tzcnt(mask);
                    ref var entry = ref UnsafeUtility.ArrayElementAsRef<Entry>(_entries, (int)index + bitPos);

                    if (entry.Key.Equals(key))
                    {
                        return false;
                    }

                    mask = ResetLowestSetBit(mask);
                }

                //mask =  source.ExtractMostSignificantBits<sbyte>();
                mask = (uint)X86.Sse2.movemask_epi8(source);

                if (mask != 0)
                {
                    var bitPos = math.tzcnt(mask);
                    index += UnsafeUtility.As<int, uint>(ref bitPos);

                    *(_metadata + index) = UnsafeUtility.As<uint, sbyte>(ref h2);

                    ref var currentEntry = ref *(_entries + index);

                    currentEntry.Key = key;
                    currentEntry.Value = value;

                    ++Count;
                    return true;
                }

                jumpDistance += 16;
                index += jumpDistance;

                if (index > 10000)
                {
                    Debug.LogError("Emplace - Failed endless loop");
                    return false;
                }

                index = index & _lengthMinusOne;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint CompareGetMask(v128 a, v128 b)
        {
            var cmp = X86.Sse2.cmpeq_epi8(a, b);
            int mask = X86.Sse2.movemask_epi8(cmp);

            return (uint)mask;
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        public bool Get(TKey key, out TValue value)
        {
            var hashcode = (uint)key.GetHashCode();
            var h2 = H2(hashcode);
            //var target = new v128(UnsafeUtility.As<uint, sbyte>(ref h2));
            var target = X86.Sse2.set1_epi8(UnsafeUtility.As<uint, sbyte>(ref h2));
            uint index = (_goldenRatio * hashcode) >> _shift;
            uint jumpDistance = 0;

            while (true)
            {
                var source = X86.Sse2.load_si128(_metadata + index);
                var mask = CompareGetMask(target, source);

                while (mask > 0)
                {
                    int bitPos = math.tzcnt(mask);
                    //ref var entry = ref UnsafeUtility.ArrayElementAsRef<Entry>(_entries, (int) index + bitPos);
                    var entry = UnsafeUtility.ReadArrayElement<Entry>(_entries, (int)index + bitPos);
                    //var entry = *(_entries + (int)index + bitPos);

                    if (Hint.Likely(entry.Key.Equals(key)))
                    {
                        value = entry.Value;
                        return true;
                    }

                    mask = ResetLowestSetBit(mask);
                }

                if (Hint.Unlikely(CompareGetMask(source, UnsafeDenseMap.EmptyBucketVector) > 0))
                {
                    value = default;
                    return false;
                }

                jumpDistance += 16;
                index += jumpDistance;

                // if (index > 10000)
                // {
                //     Debug.LogError("Get - Failed endless loop");
                //     value = default;
                //     return int.MaxValue;
                // }

                index = index & _lengthMinusOne;
            }
        }


        private void Resize()
        {
            --_shift;
            _length = _length << 1;
            _lengthMinusOne = _length - 1;
            _maxLookupsBeforeResize = (uint)(_length * _loadFactor);

            var oldEntries = _entries;
            var oldMetaData = _metadata;
            var oldSize = arrayLength;

            var newSize = UnsafeUtility.As<uint, int>(ref _length) + 16;

            //_entries = (Entry*) Memory.Unmanaged.Allocate(newSize * UnsafeUtility.SizeOf<Entry>(), JobsUtility.CacheLineSize, Allocator.ToAllocator);
            //_metadata = (sbyte*) Memory.Unmanaged.Allocate(newSize, JobsUtility.CacheLineSize, Allocator.ToAllocator);
            var memBlockSize = newSize * UnsafeUtility.SizeOf<Entry>() + newSize;
            var block = (byte*)Memory.Unmanaged.Allocate(memBlockSize, JobsUtility.CacheLineSize, Allocator.ToAllocator);

            //_entries = (Entry*) Memory.Unmanaged.Allocate(arrayLength * UnsafeUtility.SizeOf<Entry>(), JobsUtility.CacheLineSize, allocator.ToAllocator);
            //_metadata = (sbyte*) Memory.Unmanaged.Allocate(arrayLength, JobsUtility.CacheLineSize, allocator.ToAllocator);

            _entries = (Entry*)block;
            _metadata = (sbyte*)(block + arrayLength * UnsafeUtility.SizeOf<Entry>());

            arrayLength = (uint)newSize;

            for (int i = 0; i < newSize; i++)
            {
                _metadata[i] = UnsafeDenseMap.EmptyBucket;
            }

            for (uint i = 0; i < oldSize; ++i)
            {
                var h2 = *(oldMetaData + i);
                if (h2 < 0)
                {
                    continue;
                }

                var entry = *(oldEntries + i);

                var hashcode = (uint)entry.Key.GetHashCode();
                uint index = (_goldenRatio * hashcode) >> _shift;
                uint jumpDistance = 0;

                while (true)
                {
                    var mask = X86.Sse2.movemask_epi8(X86.Sse2.load_si128(_metadata + index));

                    if (mask != 0)
                    {
                        var bitPos = math.tzcnt(mask);

                        index += UnsafeUtility.As<int, uint>(ref bitPos);

                        *(_metadata + index) = h2;
                        *(_entries + index) = entry;
                        break;
                    }

                    jumpDistance += 16;
                    index += jumpDistance;

                    if (index > 10000)
                    {
                        Debug.LogError("Resize - Failed endless loop");
                        return;
                    }

                    index = index & _lengthMinusOne;
                }
            }

            Memory.Unmanaged.Free(oldEntries, Allocator.ToAllocator);
            //Memory.Unmanaged.Free(oldMetaData, Allocator.ToAllocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResetLowestSetBit(uint value)
        {
            // It's lowered to BLSR on x86
            return value & value - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint H2(uint hashcode) => hashcode & 0b01111111;

        public struct Entry
        {
            public TKey Key;
            public TValue Value;
        }
    }
}