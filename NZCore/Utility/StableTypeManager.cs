// <copyright project="NZCore" file="StableTypeManager.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Linq;
using System.Runtime.InteropServices;
using NZCore.Helper;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace NZCore
{
    public static unsafe class StableTypeManager
    {
        private static bool initialized;
        private static bool appDomainUnloadRegistered;
        
        private static UnsafeHashMap<ulong, StableTypeIndex> stableTypeMap;
        private static UnsafeHashMap<StableTypeIndex, ulong> stableHashMap;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            
            TypeManager.Initialize();
            
            stableTypeMap = new UnsafeHashMap<ulong, StableTypeIndex>(1024, Allocator.Persistent);
            stableHashMap = new UnsafeHashMap<StableTypeIndex, ulong>(1024, Allocator.Persistent);
            
            BuildMap();
            
            fixed (UnsafeHashMap<ulong, StableTypeIndex>* ptr = &stableTypeMap)
            {
                StableTypeMap.Ref.Data = new IntPtr(ptr);
            }
            
            fixed (UnsafeHashMap<StableTypeIndex, ulong>* ptr = &stableHashMap)
            {
                StableHashMap.Ref.Data = new IntPtr(ptr);
            }
            
            if (!appDomainUnloadRegistered)
            {
                AppDomain.CurrentDomain.DomainUnload += CurrentDomainOnDomainUnload;
                AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
                
                appDomainUnloadRegistered = true;
            }
        }

        private static void CurrentDomainOnProcessExit(object sender, EventArgs e)
        {
            Shutdown();
        }

        private static void CurrentDomainOnDomainUnload(object sender, EventArgs e)
        {
            Shutdown();
        }

        private static void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            initialized = false;

            stableHashMap.Dispose();
            stableTypeMap.Dispose();
        }

        private static void BuildMap()
        {
            var allTypes = TypeManager.GetAllTypes();

            stableTypeMap.Add(0, new StableTypeIndex() { Value = 0 });
            stableHashMap.Add(new StableTypeIndex(){ Value = 0}, 0);

            for (int i = 1; i < allTypes.Count();i++)
            {
                var typeInfo = allTypes[i];
                
                var hash = StableTypeHashHelper.GetFixedHash(typeInfo.Type);

                if (hash == 0)
                {
                    continue;
                }

                stableTypeMap.Add(hash, new StableTypeIndex() { Value = typeInfo.TypeIndex.Value });
                stableHashMap.Add(new StableTypeIndex() { Value = typeInfo.TypeIndex.Value }, hash);
            }
        }
        
        public static Type GetTypeFromStableTypeIndex(StableTypeIndex typeIndex)
        {
            return TypeManager.GetType(typeIndex);
        }

        public static TypeIndex GetStableTypeIndex<T>()
            where T : unmanaged
        {
            var hash = StableTypeHashHelper.GetFixedHash(typeof(T));
            return GetTypeIndexFromStableTypeHash(hash);
        }
        
        public static bool TryGetTypeIndexFromStableTypeHash(ulong hash, out TypeIndex typeIndex)
        {
            var map = *(UnsafeHashMap<ulong, StableTypeIndex>*) StableTypeMap.Ref.Data;

            if (map.TryGetValue(hash, out var typeIndex2))
            {
                typeIndex = typeIndex2;
                return true;
            }

            typeIndex = default;
            return false;
        }

        public static TypeIndex GetTypeIndexFromStableTypeHash(ulong hash)
        {
            var map = *(UnsafeHashMap<ulong, StableTypeIndex>*) StableTypeMap.Ref.Data;

            if (map.TryGetValue(hash, out var stableTypeIndex))
            {
                return stableTypeIndex;
            }
            
            return TypeIndex.Null;
        }
        
        public static bool TryGetTypeInfoFromStableTypeHash(ulong hash, out TypeManager.TypeInfo type)
        {
            if (TryGetTypeIndexFromStableTypeHash(hash, out var typeIndex))
            {
                type = GetTypeInfo(typeIndex);
                return true;
            }

            type = default;
            return false;
        }

        public static TypeManager.TypeInfo GetTypeInfo(TypeIndex typeIndex)
        {
            return TypeManager.GetTypeInfo(typeIndex);
        }
        
        public static ulong GetStableTypeHashFromTypeIndex(TypeIndex typeIndex)
        {
            var map = *(UnsafeHashMap<StableTypeIndex, ulong>*) StableHashMap.Ref.Data;
            return map.TryGetValue(typeIndex, out var hash) ? hash : 0;
        }
        
        private struct StableTypeManagerKey { }
        private struct StableTypeMap
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<StableTypeManagerKey, StableTypeMap>();
        }
        
        private struct StableHashMap
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<StableTypeManagerKey, StableHashMap>();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct StableTypeIndex : IComparable<StableTypeIndex>, IEquatable<StableTypeIndex>
    {
        [FieldOffset(0)]
        public int Value;
        
        public int CompareTo(StableTypeIndex other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(StableTypeIndex other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value;
        }
        
        public static implicit operator StableTypeIndex(TypeIndex typeIndex)
        {
            return UnsafeUtility.As<TypeIndex, StableTypeIndex>(ref typeIndex);
        }
        
        public static implicit operator TypeIndex(StableTypeIndex typeIndex)
        {
            return UnsafeUtility.As<StableTypeIndex, TypeIndex>(ref typeIndex);
        }
    }
}