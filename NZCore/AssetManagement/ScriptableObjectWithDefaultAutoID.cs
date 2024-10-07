// <copyright project="NZCore" file="ScriptableObjectWithDefaultAutoID.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.AssetManagement.Interfaces;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ScriptableObjectWithDefaultAutoID : ScriptableObjectWithAutoID, IDefaultAutoID
    {
        [Tooltip("Some internal code requires a default \"Hit\" result, like Effects, Traits or Triggers. Naturally only one AttackResult can be set as default!")]
        public bool DefaultValue;

        public bool Default => DefaultValue;

        public abstract Type DefaultType { get; }
    }
}