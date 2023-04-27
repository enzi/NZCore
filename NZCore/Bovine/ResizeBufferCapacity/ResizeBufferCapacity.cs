using System;
using System.Collections.Generic;
using System.Linq;
using BovineLabs.Core.Utility;
using Unity.Entities;
using UnityEngine;

namespace NZCore
{
    public static unsafe class ResizeBufferCapacity
    {
        static ResizeBufferCapacity()
        {
            Unity.Debug.Log($"Running ResizeBufferCapacity");
            Initialize();
            
        }

        // Runtime initialization
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Initialize()
        {
            Unity.Debug.Log($"Running ResizeBufferCapacity-Initialize");
            TypeManager.Initialize();
            
            foreach (var attr in GetAllAssemblyAttributes<BufferCapacityAttribute>())
            {
                SetBufferCapacity(attr.Type, attr.Capacity);
            }

            // TODO might be needed in future
            // typeof(BindingRegistry).GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
        }
        
        public static IEnumerable<T> GetAllAssemblyAttributes<T>()
            where T : Attribute
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetCustomAttributes(typeof(T), true))
                .Cast<T>();
        }


        public static void SetBufferCapacity(Type type, int bufferCapacity)
        {
            TypeManager.Initialize();
            SetBufferCapacity(TypeManager.GetTypeIndex(type).Index, bufferCapacity);
        }

        public static void SetBufferCapacity<T>(int bufferCapacity)
            where T : unmanaged, IBufferElementData
        {
            TypeManager.Initialize();
            SetBufferCapacity(TypeManager.GetTypeIndex<T>().Index, bufferCapacity);
        }

        private static void SetBufferCapacity(int index, int bufferCapacity)
        {
            var typeInfoPointer = TypeManager.GetTypeInfoPointer() + index;
            if (typeInfoPointer->Category != TypeManager.TypeCategory.BufferData)
            {
                Debug.LogError($"Trying to set buffer capacity on typeindex ({index}) that isn't buffer");
                return;
            }

            *&typeInfoPointer->BufferCapacity = bufferCapacity;
            *&typeInfoPointer->SizeInChunk = sizeof(BufferHeader) + (bufferCapacity * typeInfoPointer->ElementSize);
        }
    }

}