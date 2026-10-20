// <copyright project="NZCore" file="UnityObjectRefExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Internal;
using Unity.Entities;
using UnityEngine;

namespace NZCore
{
    public static class UnityObjectRefExtensions
    {
#if UNITY_6000_4_OR_NEWER
        public static EntityId GetEntityId<T>(this UnityObjectRef<T> objectRef)
            where T : Object =>
            objectRef.GetId().entityId;
#else
        public static int GetInstanceId<T>(this UnityObjectRef<T> objectRef)
            where T : Object
        {
            return objectRef.GetId().instanceId;
        } 
#endif
    }
}