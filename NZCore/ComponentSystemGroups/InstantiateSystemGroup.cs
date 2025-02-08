// <copyright project="NZCore" file="InstantiateSystemGroup.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

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