// <copyright project="NZCore.Inject" file="InjectAttribute.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using JetBrains.Annotations;

namespace NZCore.Inject
{
    [UsedImplicitly]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectAttribute : Attribute { }
}