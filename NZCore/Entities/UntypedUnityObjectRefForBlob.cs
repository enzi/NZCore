// <copyright project="NZCore" file="UnityObjectRefForBlob.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UnityObjectRefForBlob<T> : IEquatable<UnityObjectRefForBlob<T>>
        where T : Object
    {
        [SerializeField] internal UntypedUnityObjectRef Id;

        public static implicit operator UnityObjectRefForBlob<T>(T instance)
        {
#if UNITY_6000_4_OR_NEWER
            var entityId = instance == null ? default : instance.GetEntityId();
            return FromEntityID(entityId);
#else
            var instanceId = instance == null ? 0 : instance.GetInstanceID();
            return FromInstanceID(instanceId);
#endif
        }

#if UNITY_6000_4_OR_NEWER
        internal static UnityObjectRefForBlob<T> FromEntityID(EntityId entityId)
        {

            var result = new UnityObjectRefForBlob<T> { Id = new UntypedUnityObjectRef { entityId = entityId } };
            return result;
        }
#else
        internal static UnityObjectRefForBlob<T> FromInstanceID(int instanceId)
        {

            var result = new UnityObjectRefForBlob<T> { Id = new UntypedUnityObjectRef { instanceId = instanceId } };
            return result;
        }
#endif

        public static implicit operator T(UnityObjectRefForBlob<T> unityObjectRef)
        {
#if UNITY_6000_4_OR_NEWER
            if (unityObjectRef.Id.entityId == default)
            {
                return null;
            }

            return (T)Resources.EntityIdToObject(unityObjectRef.Id.entityId);
#else
            if (unityObjectRef.Id.instanceId == 0)
            {
                return null;
            }

            return (T)Resources.InstanceIDToObject(unityObjectRef.Id.instanceId);
#endif
        }

        public T Value
        {
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            get => this;
            [ExcludeFromBurstCompatTesting("Sets managed object")]
            set => this = value;
        }

#if UNITY_6000_4_OR_NEWER
        public bool Equals(UnityObjectRefForBlob<T> other) => Id.entityId == other.Id.entityId;
        public override int GetHashCode() => Id.entityId.GetHashCode();
        public bool IsValid() => Resources.EntityIdIsValid(Id.entityId);
#else
        public bool Equals(UnityObjectRefForBlob<T> other) => Id.instanceId == other.Id.instanceId;
        public override int GetHashCode() => Id.instanceId.GetHashCode();
        public bool IsValid() => Resources.InstanceIDIsValid(Id.instanceId);
#endif

        public override bool Equals(object obj) => obj is UnityObjectRefForBlob<T> other && Equals(other);


        public static implicit operator bool(UnityObjectRefForBlob<T> obj) => obj.IsValid();

        public static bool operator ==(UnityObjectRefForBlob<T> left, UnityObjectRefForBlob<T> right) => left.Equals(right);

        public static bool operator !=(UnityObjectRefForBlob<T> left, UnityObjectRefForBlob<T> right) => !left.Equals(right);

        public static implicit operator UnityObjectRef<T>(UnityObjectRefForBlob<T> value)
        {
            return new UnityObjectRef<T>()
            {
                Id = value.Id
            };
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UntypedUnityObjectRefForBlob
    {
        [SerializeField] internal int instanceId;

        // public static implicit operator UntypedUnityObjectRef(UntypedUnityObjectRefForBlob objRef)
        // {
        //     return new UntypedUnityObjectRef() { instanceId = objRef.instanceId };
        // }

        public bool Equals(UntypedUnityObjectRefForBlob other) => instanceId == other.instanceId;

        public override bool Equals(object obj) => obj is UntypedUnityObjectRefForBlob other && Equals(other);

        public override int GetHashCode() => instanceId;
    }
}