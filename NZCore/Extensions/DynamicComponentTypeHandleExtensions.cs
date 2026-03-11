// <copyright project="NZCore" file="DynamicComponentTypeHandleExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public static class DynamicComponentTypeHandleExtensions
    {
        public static ref readonly TypeIndex GetTypeIndex(this ref DynamicComponentTypeHandle handle) => ref handle.m_TypeIndex;
    }
}