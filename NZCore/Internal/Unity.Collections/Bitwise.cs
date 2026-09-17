using System.Runtime.CompilerServices;

namespace NZCore.Internal
{
    public static class Bitwise
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignDown(int value, int alignPow2) => Unity.Collections.Bitwise.AlignDown(value, alignPow2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AlignDown(long value, int alignPow2) => Unity.Collections.Bitwise.AlignDown(value, alignPow2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignUp(int value, int alignPow2) => Unity.Collections.Bitwise.AlignUp(value, alignPow2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AlignUp(long value, int alignPow2) => Unity.Collections.Bitwise.AlignUp(value, alignPow2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromBool(bool value) => Unity.Collections.Bitwise.FromBool(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExtractBits(uint input, int pos, uint mask) => Unity.Collections.Bitwise.ExtractBits(input, pos, mask);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReplaceBits(uint input, int pos, uint mask, uint value) => Unity.Collections.Bitwise.ReplaceBits(input, pos, mask, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SetBits(uint input, int pos, uint mask, bool value) => Unity.Collections.Bitwise.SetBits(input, pos, mask, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ExtractBits(ulong input, int pos, ulong mask) => Unity.Collections.Bitwise.ExtractBits(input, pos, mask);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReplaceBits(ulong input, int pos, ulong mask, ulong value) => Unity.Collections.Bitwise.ReplaceBits(input, pos, mask, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SetBits(ulong input, int pos, ulong mask, bool value) => Unity.Collections.Bitwise.SetBits(input, pos, mask, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int lzcnt(byte value) => Unity.Collections.Bitwise.lzcnt(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int tzcnt(byte value) => Unity.Collections.Bitwise.tzcnt(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int lzcnt(ushort value) => Unity.Collections.Bitwise.lzcnt(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int tzcnt(ushort value) => Unity.Collections.Bitwise.tzcnt(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int FindWithBeginEnd(ulong* ptr, int beginBit, int endBit, int numBits) => Unity.Collections.Bitwise.FindWithBeginEnd(ptr, beginBit, endBit, numBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Find(ulong* ptr, int pos, int count, int numBits) => Unity.Collections.Bitwise.Find(ptr, pos, count, numBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TestNone(ulong* ptr, int length, int pos, int numBits = 1) => Unity.Collections.Bitwise.TestNone(ptr, length, pos, numBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TestAny(ulong* ptr, int length, int pos, int numBits = 1) => Unity.Collections.Bitwise.TestAny(ptr, length, pos, numBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TestAll(ulong* ptr, int length, int pos, int numBits = 1) => Unity.Collections.Bitwise.TestAll(ptr, length, pos, numBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int CountBits(ulong* ptr, int length, int pos, int numBits = 1) => Unity.Collections.Bitwise.CountBits(ptr, length, pos, numBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool IsSet(ulong* ptr, int pos) => Unity.Collections.Bitwise.IsSet(ptr, pos);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong GetBits(ulong* ptr, int length, int pos, int numBits = 1) => Unity.Collections.Bitwise.GetBits(ptr, length, pos, numBits);
    }
}
