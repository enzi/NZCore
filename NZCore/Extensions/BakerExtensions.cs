// <copyright project="NZCore" file="BakerExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Entities;
using UnityEngine;

namespace NZCore
{
    public static class BakerExtensions
    {
        public static bool TryGetComponent<T>(this IBaker baker, out T comp)
            where T : Component
        {
            comp = baker.GetComponent<T>();
            return comp != null;
        }

        /// <summary>
        /// Requests an <see cref="ArenaBuffer{T}"/> of <paramref name="capacity"/> elements for the entity.
        ///
        /// The returned DynamicBuffer is temporary serialized storage. <c>ArenaBufferReserveSystem</c>
        /// copies it into the arena and removes the staging component at runtime.
        /// </summary>
        public static DynamicBuffer<T> AddArenaBuffer<T>(this IBaker baker, Entity entity, int capacity)
            where T : unmanaged, IArenaBuffer
        {
            if (!ArenaBufferRegistry.TryGetBakerAdder(typeof(T), out var adder))
            {
                throw new InvalidOperationException(
                    $"No arena buffer registration found for {typeof(T)}. The NZCore.ArenaBuffer source generator has to run on the assembly declaring it.");
            }

            adder(baker, entity, capacity);
            var staging = baker.AddBuffer<ArenaBufferStaging<T>>(entity);
            staging.EnsureCapacity(capacity);
            return staging.Reinterpret<T>();
        }
    }
}
