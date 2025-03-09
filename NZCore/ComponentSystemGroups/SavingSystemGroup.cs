// <copyright project="NZCore" file="SavingSystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Scenes;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial class SavingSystemGroup : ComponentSystemGroup
    {
    }
}