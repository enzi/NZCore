// <copyright project="NZCore" file="AddressableAndHandle.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_ADDRESSABLES
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NZCore.AssetManagement
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
}
#endif