// <copyright project="NZCore.Authoring" file="SettingsBuffer.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Settings
{
    [Serializable]
    public abstract class SettingsBuffer<T> : SettingsBase
        where T : unmanaged, IBufferElementData
    {
        [SerializeField] private T[] buffer = Array.Empty<T>();

        public sealed override void Bake(IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.None);
            var entityBuffer = baker.AddBuffer<T>(entity);

            foreach (var b in buffer)
            {
                entityBuffer.Add(b);
            }

            CustomBake(baker);
        }

        protected virtual void CustomBake(IBaker baker)
        {
        }
    }
}