// <copyright project="NZCore" file="SavingSystemGroup.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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