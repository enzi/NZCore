// <copyright project="NZCore" file="UntypedUnityObjectRefForBlob.cs">
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
        [SerializeField]
        internal UntypedUnityObjectRef Id;
        
        public static implicit operator UnityObjectRefForBlob<T>(T instance)
        {
            var instanceId = instance == null ? 0 : instance.GetInstanceID();
    
            return FromInstanceID(instanceId);
        }
    
        internal static UnityObjectRefForBlob<T> FromInstanceID(int instanceId)
        {
            var result = new UnityObjectRefForBlob<T>{Id = new UntypedUnityObjectRef{ instanceId = instanceId }};
            return result;
        }
    
        public static implicit operator T(UnityObjectRefForBlob<T> unityObjectRef)
        {
            if (unityObjectRef.Id.instanceId == 0)
            {
                return null;
            }
    
            return (T) Resources.InstanceIDToObject(unityObjectRef.Id.instanceId);
        }
    
        public T Value
        {
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            get => this;
            [ExcludeFromBurstCompatTesting("Sets managed object")]
            set => this = value;
        }
    
        public bool Equals(UnityObjectRefForBlob<T> other)
        {
            return Id.instanceId == other.Id.instanceId;
        }
    
        public override bool Equals(object obj)
        {
            return obj is UnityObjectRefForBlob<T> other && Equals(other);
        }
    
    
        public static implicit operator bool(UnityObjectRefForBlob<T> obj)
        {
            return obj.IsValid();
        }
    
        public override int GetHashCode()
        {
            return Id.instanceId.GetHashCode();
        }
    
        public bool IsValid()
        {
            return Resources.InstanceIDIsValid(Id.instanceId);
        }
    
        public static bool operator ==(UnityObjectRefForBlob<T> left, UnityObjectRefForBlob<T> right)
        {
            return left.Equals(right);
        }
    
        public static bool operator !=(UnityObjectRefForBlob<T> left, UnityObjectRefForBlob<T> right)
        {
            return !left.Equals(right);
        }
    }
    
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UntypedUnityObjectRefForBlob
    {
        [SerializeField]
        internal int instanceId;
    
        // public static implicit operator UntypedUnityObjectRef(UntypedUnityObjectRefForBlob objRef)
        // {
        //     return new UntypedUnityObjectRef() { instanceId = objRef.instanceId };
        // }
        
        public bool Equals(UntypedUnityObjectRefForBlob other)
        {
            return instanceId == other.instanceId;
        }
    
        public override bool Equals(object obj)
        {
            return obj is UntypedUnityObjectRefForBlob other && Equals(other);
        }
    
        public override int GetHashCode()
        {
            return instanceId;
        }
    }
}