// <copyright file="IEntityCache.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public interface IEntityCache
    {
        Entity Entity { get; set; }
    }
}