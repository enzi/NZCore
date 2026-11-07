// <copyright project="NZCore.Tests" file="CappedAutoIDTestAsset.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;
using UnityEngine;

namespace NZCore.Tests.AssetManagement
{
    public class CappedAutoIDTestAsset : ScriptableObjectWithAutoID
    {
        public const int MaxId = 3;

        [SerializeField] private int _autoID;

        public override int AutoID
        {
            get => _autoID;
            set => _autoID = value;
        }

        public override int MaxAutoID => MaxId;

        public override AutoIDConflictMode ConflictMode => AutoIDConflictMode.Error;

        public override HasChangeResult HasChanges(List<IChangeProcessor> allAssets) => HasChangeResult.None;

        public override void ProcessChanges(List<IChangeProcessor> allAssets) { }
    }
}