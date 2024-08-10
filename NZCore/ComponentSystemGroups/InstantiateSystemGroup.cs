// <copyright project="NZCore" file="InstantiateSystemGroup.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using NZSpellCasting;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(SavingSystemGroup))]
    public partial class InstantiateSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SavingSystemGroup))]
    public partial class PostInstantiateSystemGroup : ComponentSystemGroup
    {
    }
}