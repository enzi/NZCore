// <copyright project="NZCore" file="StableTypeManager.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
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
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static unsafe class StableTypeManager
    {
        private static bool initialized;
        private static bool appDomainUnloadRegistered;

#if UNITY_EDITOR
        public static Dictionary<string, ulong> TypeNameMap;

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

            // The maps live in SharedStatic memory, not in static fields: CoreCLR can move static structs, so a pointer to one goes stale
            StableTypeMap.Ref.Data = new UnsafeHashMap<ulong, StableTypeIndex>(1024, Allocator.Persistent);
            StableHashMap.Ref.Data = new UnsafeHashMap<StableTypeIndex, ulong>(1024, Allocator.Persistent);

#if UNITY_EDITOR
            TypeNameMap = new Dictionary<string, ulong>();
#endif

            BuildMap();

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

            StableHashMap.Ref.Data.Dispose();
            StableTypeMap.Ref.Data.Dispose();
        }

        private static void BuildMap()
        {
            var allTypes = TypeManager.GetAllTypes();
            ref var stableTypeMap = ref StableTypeMap.Ref.Data;
            ref var stableHashMap = ref StableHashMap.Ref.Data;

            stableTypeMap.Add(0, new StableTypeIndex { Value = 0 });
            stableHashMap.Add(new StableTypeIndex { Value = 0 }, 0);

            for (var i = 1; i < allTypes.Length; i++)
            {
                var typeInfo = allTypes[i];

                var hash = StableTypeHashHelper.GetFixedHash(typeInfo.Type);

                if (hash == 0)
                {
                    continue;
                }

                if (stableTypeMap.ContainsKey(hash))
                {
                    // entities 1.3.10 can have duplicate types
                    var tmp = stableTypeMap[hash];

                    stableHashMap.Remove(tmp);
                    stableTypeMap.Remove(hash);
                }
#if UNITY_EDITOR
                TypeNameMap[typeInfo.Type.Name] = hash;
#endif
                stableTypeMap.Add(hash, new StableTypeIndex { Value = typeInfo.TypeIndex.Value });
                stableHashMap.Add(new StableTypeIndex { Value = typeInfo.TypeIndex.Value }, hash);
            }
        }

        public static Type GetTypeFromStableTypeIndex(StableTypeIndex typeIndex) => TypeManager.GetType(typeIndex);

        public static TypeIndex GetStableTypeIndex<T>()
            where T : unmanaged
        {
            var hash = StableTypeHashHelper.GetFixedHash(typeof(T));
            return GetTypeIndexFromStableTypeHash(hash);
        }

        public static bool TryGetTypeIndexFromStableTypeHash(ulong hash, out TypeIndex typeIndex)
        {
            if (StableTypeMap.Ref.Data.TryGetValue(hash, out var typeIndex2))
            {
                typeIndex = typeIndex2;
                return true;
            }

            typeIndex = default;
            return false;
        }

        public static TypeIndex GetTypeIndexFromStableTypeHash(ulong hash)
        {
            if (StableTypeMap.Ref.Data.TryGetValue(hash, out var stableTypeIndex))
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

        public static TypeManager.TypeInfo GetTypeInfo(TypeIndex typeIndex) => TypeManager.GetTypeInfo(typeIndex);

        public static ulong GetStableTypeHashFromTypeIndex(TypeIndex typeIndex) => StableHashMap.Ref.Data.TryGetValue(typeIndex, out var hash) ? hash : 0;

        private struct StableTypeManagerKey { }

        private struct StableTypeMap
        {
            public static readonly SharedStatic<UnsafeHashMap<ulong, StableTypeIndex>> Ref =
                SharedStatic<UnsafeHashMap<ulong, StableTypeIndex>>.GetOrCreate<StableTypeManagerKey, StableTypeMap>();
        }

        private struct StableHashMap
        {
            public static readonly SharedStatic<UnsafeHashMap<StableTypeIndex, ulong>> Ref =
                SharedStatic<UnsafeHashMap<StableTypeIndex, ulong>>.GetOrCreate<StableTypeManagerKey, StableHashMap>();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct StableTypeIndex : IComparable<StableTypeIndex>, IEquatable<StableTypeIndex>
    {
        [FieldOffset(0)] public int Value;

        public int CompareTo(StableTypeIndex other) => Value.CompareTo(other.Value);

        public bool Equals(StableTypeIndex other) => Value == other.Value;

        public override int GetHashCode() => Value;

        public static implicit operator StableTypeIndex(TypeIndex typeIndex) => UnsafeUtility.As<TypeIndex, StableTypeIndex>(ref typeIndex);

        public static implicit operator TypeIndex(StableTypeIndex typeIndex) => UnsafeUtility.As<StableTypeIndex, TypeIndex>(ref typeIndex);
    }
}