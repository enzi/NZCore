using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class ScriptableObjectWithAutoID : ScriptableObject, IAutoID
    {
        public abstract int AutoID { get; set; }
        public abstract IChangeProcessor ChangeProcessor { get; }
    }
}