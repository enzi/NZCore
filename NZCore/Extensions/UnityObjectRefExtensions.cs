// <copyright project="NZCore" file="UnityObjectRefExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore
{
    public static class UnityObjectRefExtensions
    {
        public static int GetInstanceId<T>(this UnityObjectRef<T> objectRef)
            where T : Object =>
            objectRef.Id.instanceId;
    }
}