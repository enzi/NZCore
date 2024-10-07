// <copyright project="NZCore" file="BakerExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

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
    }
}