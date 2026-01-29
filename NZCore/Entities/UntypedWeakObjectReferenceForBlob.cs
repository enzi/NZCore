// <copyright project="NZCore" file="UntypedWeakReferenceIdForBlob.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore
{
    [Serializable]
    public struct WeakObjectReferenceForBlob<T> : IEquatable<WeakObjectReferenceForBlob<T>>
        where T : Object
    {
        [SerializeField] 
        public UntypedWeakObjectReferenceForBlob Id;

        public static implicit operator WeakObjectReference<T>(WeakObjectReferenceForBlob<T> weakObjectRef) => new(weakObjectRef.Id);
        public static implicit operator WeakObjectReferenceForBlob<T>(WeakObjectReference<T> weakRef) => new() { Id = weakRef.Id };
        public static implicit operator UntypedWeakReferenceId(WeakObjectReferenceForBlob<T> weakObjectRef) => weakObjectRef.Id;
        public static bool operator ==(WeakObjectReferenceForBlob<T> left, WeakObjectReferenceForBlob<T> right) => left.Equals(right);
        public static bool operator !=(WeakObjectReferenceForBlob<T> left, WeakObjectReferenceForBlob<T> right) => !left.Equals(right);
        public bool Equals(WeakObjectReferenceForBlob<T> other) => Id.Equals(other.Id);
        public override bool Equals(object obj) => obj is WeakObjectReferenceForBlob<T> other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }
    
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UntypedWeakObjectReferenceForBlob : IEquatable<UntypedWeakObjectReferenceForBlob>
    {
        public RuntimeGlobalObjectId GlobalId;
        public WeakReferenceGenerationType GenerationType;
        
        public UntypedWeakObjectReferenceForBlob(UntypedWeakReferenceId weakRef)
        {
            GlobalId = weakRef.GlobalId;
            GenerationType = weakRef.GenerationType;
        }

        public static implicit operator UntypedWeakReferenceId(UntypedWeakObjectReferenceForBlob weakObjectRef) =>
            new(weakObjectRef.GlobalId, weakObjectRef.GenerationType);

        public static implicit operator UntypedWeakObjectReferenceForBlob(UntypedWeakReferenceId weakRef) => new()
        {
            GlobalId = weakRef.GlobalId,
            GenerationType = weakRef.GenerationType
        };

        public WeakObjectReference<AnimationClip> AsAnimationClip()
        {
            return new WeakObjectReference<AnimationClip>(this);
        }
        
        public WeakObjectReference<AudioClip> AsAudioClip()
        {
            return new WeakObjectReference<AudioClip>(this);
        }
        
        public WeakObjectReference<GameObject> AsGameObject()
        {
            return new WeakObjectReference<GameObject>(this);
        }

        public static bool operator ==(UntypedWeakObjectReferenceForBlob left, UntypedWeakObjectReferenceForBlob right) => left.Equals(right);
        public static bool operator !=(UntypedWeakObjectReferenceForBlob left, UntypedWeakObjectReferenceForBlob right) => !left.Equals(right);
        public bool Equals(UntypedWeakObjectReferenceForBlob other) => GlobalId.Equals(other.GlobalId) && GenerationType == other.GenerationType;
        public override bool Equals(object obj) => obj is UntypedWeakObjectReferenceForBlob other && Equals(other);
        public override int GetHashCode() => GlobalId.GetHashCode();
    }
}