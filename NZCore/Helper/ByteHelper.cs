// <copyright project="NZCore" file="ByteHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    public static class ByteHelper
    {
        public static unsafe ref T ReadFromPointer<T>(ref byte* ptr)
            where T : unmanaged
        {
            ref var val = ref UnsafeUtility.AsRef<T>(ptr);
            ptr += UnsafeUtility.SizeOf<T>();
            return ref val;
        }

        public static unsafe ref T ReadFromPointerWithOffset<T>(ref byte* ptr, ref int offset)
            where T : unmanaged
        {
            ref var val = ref UnsafeUtility.AsRef<T>(ptr);
            var size = UnsafeUtility.SizeOf<T>();
            ptr += size;
            offset += size;
            return ref val;
        }

        public static unsafe ref T WriteToPointer<T>(ref byte* ptr, T payload)
            where T : unmanaged
        {
            ref var val = ref UnsafeUtility.AsRef<T>(ptr);
            val = payload;
            ptr += UnsafeUtility.SizeOf<T>();
            return ref val;
        }
        
        public static unsafe byte[] GetBytes<T>(T value) 
            where T : unmanaged
        {
            byte[] result = new byte[sizeof(T)];
            fixed (byte* ptr = result)
            {
                *(T*)ptr = value;
            }
            return result;
        }

        public static byte[] GetBytesFromStruct<T>(T structure)
            where T : struct
        {
            int size = Marshal.SizeOf(structure);
            int size2 = UnsafeUtility.SizeOf<T>();

            if (size != size2)
            {
                throw new Exception($"The sizes are different for {typeof(T).Name} - {size} vs {size2}! Fix your struct!");
            }

            byte[] arr = new byte[size];

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(structure, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return arr;
        }

        public static byte ToByte(this bool boolean)
        {
            return boolean ? (byte)1 : (byte)0;
        }
    }
}