// <copyright project="NZCore" file="EntityRefBuffer.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    /// <summary>
    /// A ref buffer that is used to patch blob entity references
    /// </summary>
    public struct EntityRefPatchBuffer : IBufferElementData
    {
        public TypeIndex TypeIndex;
        public Entity EntityToPatch;
        public Entity BlobEntity;
        public long BlobOffset;
        public int BlobAssetReferenceIndex;
    }

    public struct EntityRefPatchBufferResolved : IComponentData, IEnableableComponent { }
}