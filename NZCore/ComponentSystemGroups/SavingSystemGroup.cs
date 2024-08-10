// <copyright project="NZCore" file="SavingSystemGroup.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Entities;
using Unity.Scenes;

namespace NZSpellCasting
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial class SavingSystemGroup : ComponentSystemGroup
    {
    }
}