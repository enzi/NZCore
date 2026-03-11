// <copyright project="NZCore" file="WeakObjectReferenceExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine;

namespace NZCore
{
    public static class WeakObjectReferenceExtensions
    {
        public static bool IsValidBurst<T>(this WeakObjectReference<T> weakRef)
            where T : Object =>
            weakRef.Id.IsValid;

        public static UntypedWeakReferenceId GetInternalId<T>(this WeakObjectReference<T> weakRef)
            where T : Object =>
            weakRef.Id;
    }
}