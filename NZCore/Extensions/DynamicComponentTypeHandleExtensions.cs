// <copyright project="NZCore" file="DynamicComponentTypeHandleExtensions.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public static class DynamicComponentTypeHandleExtensions
    {
        public static ref readonly TypeIndex GetTypeIndex(this ref DynamicComponentTypeHandle handle)
        {
            return ref handle.m_TypeIndex;
        }
    }
}