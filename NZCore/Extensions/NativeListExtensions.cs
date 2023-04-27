using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace NZCore
{
    public unsafe struct NativeListExtended<T> where T : unmanaged
    {
        public int increaseCount;
        public int currentIndex;

        [NativeDisableUnsafePtrRestriction] public UnsafeList<T>* list;
        [NativeDisableUnsafePtrRestriction] public T* currentPtr;

        public void Add(in T item)
        {
            if (currentPtr == null || currentIndex >= increaseCount)
            {
                if (ReserveNoResize(list, increaseCount, out currentPtr, out currentIndex))
                {
                    currentIndex = 0;
                }                
                else
                {
                    Debug.LogError($"Adding item failed! Could not reserve more memory. Capacity exceeded! index {currentIndex} length {list->m_length} capacity {list->m_capacity} increaseCount {increaseCount}");
                    return;
                }
            }

            //UnsafeUtility.WriteArrayElement(currentPtr, currentIndex, item);
            *(T*)((byte*)currentPtr + currentIndex * sizeof(T)) = item;

            //Debug.Log("Writing '" + item.ToString() + "' to index: " + currentIndex + " ptr: " + new IntPtr((void*)currentPtr).ToString("X"));
            currentIndex++;
        }

        public void AddRange(ref UnsafeList<T> listToAdd)
        {
            //list->AddRangeNoResize(listToAdd);
            int count = listToAdd.m_length;

            var sizeOf = sizeof(T);
            void* dst = (byte*)list->Ptr + list->m_length * sizeOf;
            UnsafeUtility.MemCpy(dst, listToAdd.Ptr, count * sizeOf);
            list->m_length += count;
        }

        public void FillEmpty()
        {
            if (currentPtr == null)
                return;
            
            //UnsafeUtility.MemSet(currentPtr + currentIndex * sizeof(T), 0, (increaseCount - currentIndex) * sizeof(T));

            while (currentIndex < increaseCount)
            {
                *(T*)((byte*)currentPtr + currentIndex * sizeof(T)) = default(T);
                currentIndex++;
            }
        }

        public static bool ReserveNoResize(UnsafeList<T>* list, int length, out T* ptr, out int idx)
        {
            if (list->m_length + length > list->m_capacity)
            {
                idx = 0;
                ptr = null;
                return false;
            }

            idx = Interlocked.Add(ref list->m_length, length) - length;
            ptr = (T*)(((byte*)list->Ptr) + (idx * UnsafeUtility.SizeOf<T>()));

            return true;
        }
    }

    public static unsafe class NativeListExtensions
    {
        public static NativeListExtended<T> GetExtendedList<T>(this ref NativeList<T>.ParallelWriter nativeList, int increaseCount = 10) where T : unmanaged
        {
            NativeListExtended<T> newList = new NativeListExtended<T>
            {
                list = nativeList.ListData,
                increaseCount = increaseCount,
                currentIndex = -1,
                currentPtr = null
            };
            return newList;
        }        
    }
}