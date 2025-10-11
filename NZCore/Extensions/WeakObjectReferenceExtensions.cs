// <copyright project="NZCore" file="WeakObjectReferenceExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities.Content;
using UnityEngine;

namespace NZCore
{
    public static class WeakObjectReferenceExtensions
    {
        public static bool IsValidBurst<T>(this WeakObjectReference<T> weakRef)
            where T : Object
        {
            return weakRef.Id.IsValid;
        }
    }
}