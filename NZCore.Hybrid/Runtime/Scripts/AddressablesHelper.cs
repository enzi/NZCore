using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace NZCore.Hybrid
{
    public class AddressableAndHandle<T> where T : class
    {
        public T Asset;
        public AsyncOperationHandle<T> Handle;

        public void Unload()
        {
            Addressables.Release(Handle);
        }
    }
    
    public class AddressablesAndHandles<T> where T : class
    {
        public readonly Dictionary<string, T> Assets = new();
        public readonly Dictionary<string, AsyncOperationHandle<T>> Handles = new();

        public void UnloadAll()
        {
            foreach (var pair in Handles)
            {
                // release the handle
                Addressables.Release(pair.Value);
            }
            
            Assets.Clear();
            Handles.Clear();
        }
    }

    public static class AddressablesHelper
    {
        public static async Task<Dictionary<string, T>> LoadAssets<T>(IList<string> keys) where T : class
        {
            var loadHandle = Addressables.LoadAssetsAsync<T>(keys, null, Addressables.MergeMode.Union);
            await loadHandle.Task;

            var assets = loadHandle.Result;

            Dictionary<string, T> val = new Dictionary<string, T>();

            if (assets.Count < keys.Count)
                Debug.LogError($"Not all assets could be loaded! {assets.Count} != {keys.Count}");

            for (int i = 0; i < assets.Count; i++)
            {
                val.Add(keys[i], assets[i]);
            }

            return val;
        }

        public static async Task<AddressablesAndHandles<T>> LoadAssetsFromLabel<T>(string labelName) where T : class
        {
            IList<string> keys = new[]
            {
                labelName
            };

            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = Addressables.LoadResourceLocationsAsync(keys, Addressables.MergeMode.Union, typeof(T));

            await locationsHandle.Task;

            var locations = locationsHandle.Result;

            var loadOps = new List<AsyncOperationHandle>(locations.Count);

            var val = new AddressablesAndHandles<T>();

            foreach (IResourceLocation location in locations)
            {
                var loadHandle = Addressables.LoadAssetAsync<T>(location);
                loadHandle.Completed += (assetHandle) =>
                {
                    val.Assets.Add(location.PrimaryKey, assetHandle.Result);
                    val.Handles.Add(location.PrimaryKey, assetHandle);
                };

                loadOps.Add(loadHandle);
            }

            await Addressables.ResourceManager.CreateGenericGroupOperation(loadOps, true).Task;

            return val;
        }
    }
}