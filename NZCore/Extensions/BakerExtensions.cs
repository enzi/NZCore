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
        /// Baking cannot touch arena memory, so this only bakes the request. <c>ArenaBufferReserveSystem</c>
        /// hands out the actual block at runtime. The capacity is rounded up to a power of two.
        /// </summary>
        public static void AddArenaBuffer<T>(this IBaker baker, Entity entity, int capacity = 8)
            where T : unmanaged, IArenaBuffer
        {
            if (!ArenaBufferRegistry.TryGetBakerAdder(typeof(T), out var adder))
            {
                throw new InvalidOperationException(
                    $"No arena buffer registration found for {typeof(T)}. The NZCore.ArenaBuffer source generator has to run on the assembly declaring it.");
            }

            adder(baker, entity, capacity);
        }
    }
}