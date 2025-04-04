﻿// <copyright project="NZCore" file="BakerExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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