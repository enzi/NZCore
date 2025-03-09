// <copyright project="NZCore" file="InstantiateSystemGroup.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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