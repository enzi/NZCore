using System.Collections.Generic;

namespace NZCore.AssetManagement
{
    public interface IChangeProcessor
    {
        void DidChange(List<ScriptableObjectWithAutoID> allAssets);
    }
}