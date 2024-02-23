using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NZCore.AssetManagement
{
    public abstract class AutoIDProcessor : AssetPostprocessor
    {
        // ReSharper disable once Unity.IncorrectMethodSignature
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload)
                return;

            var processors = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(t => t.GetTypes())
                .Where(t => !t.IsAbstract && t.GetInterfaces().Contains(typeof(IAutoIDProcessor)))
                .Select(t => (IAutoIDProcessor)Activator.CreateInstance(t))
                .ToList();

            if (processors.Count == 0)
            {
                //Debug.LogError("No IAutoIDProcessor found");
                return;
            }

            foreach (var processor in processors)
            {
                processor.Process(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
        }
    }
    
    public abstract class AutoIDProcessor<T, TChangeProcessor> : IAutoIDProcessor
        where T :  ScriptableObjectWithAutoID
        where TChangeProcessor : struct, IChangeProcessor
    {
        public void Process(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            //bool didChange = false;
            Dictionary<int, List<T>> map = new Dictionary<int, List<T>>();
            //List<ScriptableObjectWithAutoID> allAssets = new List<ScriptableObjectWithAutoID>();

            var assetGuids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            
            string rootFolder = null;
            bool runResolvePath = false;

            foreach (var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                rootFolder ??= GetRootFolder(assetPath);

                if (!map.TryGetValue(asset.AutoID, out var list))
                {
                    list = new List<T>();
                    map[asset.AutoID] = list;
                }
                else
                {
                    runResolvePath = true;
                }
                
                list.Add(asset);
                //allAssets.Add(asset);
            } 

            foreach (var importedAsset in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(importedAsset);
            
                if (asset == null)
                    continue;
            
                if (map.TryGetValue(asset.AutoID, out var list) && (asset.AutoID == 0 || list.Count > 1))
                {
                    var newId = GetFirstFreeID(map);
                    
                    if (newId == -1)
                    {
                        Debug.LogError("AutoIDProcessor has run out of IDs!");
                        return;
                    }
                    
                    map[asset.AutoID].Remove(asset);
                    asset.AutoID = newId;
                    if (!map.TryGetValue(newId, out var newList))
                    {
                        newList = new List<T>();
                        map.Add(newId, newList);
                    }
            
                    newList.Add(asset);
            
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
            
                    //didChange = true;
                }
            }

            if (runResolvePath)
            {
                // resolve any assets that still have a count > 1

                var keyList = map.Select(m => m.Key).ToList();

                bool hasErrors = false;

                foreach (var key in keyList)
                {
                    var value = map[key];

                    if (value.Count <= 1) 
                        continue;

                    hasErrors = true;
                    
                    foreach (var element in value)
                    {
                        Debug.LogError($"Conflicting ID in asset {element.name} -> {element.AutoID}");
                    }

                    // auto resolving is disabled
                    // and deemed too dangerous :)
                    //
                    // while (value.Count > 1)
                    // {
                    //     var asset = value[^1];
                    //     var newId = GetFirstFreeID(map);
                    //
                    //     if (newId == -1)
                    //     {
                    //         Debug.LogError("AutoIDProcessor has run out of IDs!");
                    //         return;
                    //     }
                    //
                    //     map[key].Remove(asset);
                    //     asset.AutoID = newId;
                    //
                    //     if (!map.TryGetValue(newId, out var newList))
                    //     {
                    //         newList = new List<T>();
                    //         map.Add(newId, newList);
                    //     }
                    //
                    //     newList.Add(asset);
                    //
                    //     EditorUtility.SetDirty(asset);
                    //     AssetDatabase.SaveAssetIfDirty(asset);
                    //
                    //     didChange = true;
                    // }
                }
                
                if (hasErrors)
                    return;
            }

            // if (rootFolder != null)
            // {
            //     foreach (var deletedAsset in deletedAssets)
            //     {
            //         if (deletedAsset.Contains(rootFolder))
            //             didChange = true;
            //     }
            // }
            //
            // if (didChange)
            // {
            //     // DidChange and subsequent codegen is handled via a button in the SO now
            //     // generating code on every change is not cool 
            //     //TChangeProcessor changeProcessor = default;
            //     //changeProcessor.DidChange(allAssets);
            //      
            //     //AssetDatabase.ForceReserializeAssets();
            // }
        }

        private static string GetRootFolder(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            int index = filePath.LastIndexOf('/');
            return filePath.Substring(0, index).Replace("Assets", "");
        }

        private static int GetFirstFreeID(Dictionary<int, List<T>> map)
        {
            // we start at 1 and reserve 0 for "None" states
            // tbd if this makes sense
            for (var i = 1; i < int.MaxValue; i++)
            {
                if (!map.ContainsKey(i))
                    return i;
            }

            return -1;
        }
    }
}