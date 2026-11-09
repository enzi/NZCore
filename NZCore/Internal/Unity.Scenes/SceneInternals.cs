// <copyright project="Unity.Scenes" file="SceneInternals.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore.Internal
{
    public static class SceneInternals
    {
        public static TypeIndex EditorRenderDataTypeIndex => TypeManager.GetTypeIndex<EditorRenderData>();
    }
}