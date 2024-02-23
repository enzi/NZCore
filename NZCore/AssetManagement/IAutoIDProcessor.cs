namespace NZCore.AssetManagement
{
    public interface IAutoIDProcessor
    {
        void Process(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths);
    }
}