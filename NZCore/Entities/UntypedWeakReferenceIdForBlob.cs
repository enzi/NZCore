// <copyright project="NZCore" file="UntypedWeakReferenceIdForBlob.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine;

namespace NZCore
{
    public struct UntypedWeakReferenceIdForBlob
    {
        public RuntimeGlobalObjectId GlobalId;
        public WeakReferenceGenerationType GenerationType;
        
        public UntypedWeakReferenceIdForBlob(UntypedWeakReferenceId weakAssetRef)
        {
            GlobalId = weakAssetRef.GlobalId;
            GenerationType = weakAssetRef.GenerationType;
        }
        
        public static implicit operator UntypedWeakReferenceId(UntypedWeakReferenceIdForBlob blob)
        {
            return UnsafeUtility.As<UntypedWeakReferenceIdForBlob, UntypedWeakReferenceId>(ref blob);
        }

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
    }
}