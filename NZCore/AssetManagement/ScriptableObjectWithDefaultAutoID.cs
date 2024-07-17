using System;
using NZCore.AssetManagement.Interfaces;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ScriptableObjectWithDefaultAutoID : ScriptableObject, IAutoID, IDefaultAutoID
    {
        public abstract int AutoID { get; set; }
        public abstract IChangeProcessor ChangeProcessor { get; }

        [Tooltip("Some internal code requires a default \"Hit\" result, like Effects, Traits or Triggers. Naturally only one AttackResult can be set as default!")]
        public bool DefaultValue;

        public bool Default => DefaultValue;

        public abstract Type DefaultType { get; }
    }
}