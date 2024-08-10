// <copyright project="NZCore" file="NZDestroySystemGroup.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class NZDestroySystemGroup : ComponentSystemGroup
    {
    }
}