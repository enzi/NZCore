// <copyright project="NZCore" file="GameObjectPoolSystem.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Hybrid
{
    public class GameObjectPoolSingleton : IComponentData
    {
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GameObjectPoolSystem : SystemBase
    {
        private Dictionary<int, Stack<GameObject>> Pool;


        protected override void OnCreate()
        {
            Pool = new Dictionary<int, Stack<GameObject>>();
            Enabled = false; // this sytem has no Update
        }

        protected override void OnUpdate()
        {
        }

        public GameObject Get(GameObject prefab, out bool freshInstance)
        {
            var key = prefab.GetInstanceID();

            if (Pool.TryGetValue(key, out var poolableObjects) && poolableObjects.Count > 0)
            {
                var poolableObj = poolableObjects.Pop();

                poolableObj.gameObject.SetActive(true);

                freshInstance = false;

                return poolableObj;
            }

            var inst = Object.Instantiate(prefab);

            var prefabId = inst.AddComponent<GameObjectPrefabID>();
            prefabId.prefabId = key;

            freshInstance = true;

            return inst;
        }

        public void Release(GameObject pooledObject)
        {
            if (!pooledObject.TryGetComponent(out GameObjectPrefabID goPrefabId))
                return;

            var prefabId = goPrefabId.prefabId;

            pooledObject.SetActive(false);

            if (Pool.TryGetValue(prefabId, out var poolableObjects))
            {
                poolableObjects.Push(pooledObject);
                pooledObject.gameObject.SetActive(false);
            }
            else
            {
                var stack = new Stack<GameObject>();
                stack.Push(pooledObject);
                Pool.Add(prefabId, stack);
            }
        }

        public void Unload()
        {
            Debug.Log("Pool Reset");
            int i = 0;
            foreach (var entry in Pool)
            {
                while (entry.Value.Count > 0)
                {
                    var obj = entry.Value.Pop();
                    Object.Destroy(obj.gameObject);

                    i++;
                }
            }

            foreach (var obj in Object.FindObjectsByType<GameObjectPrefabID>(FindObjectsSortMode.None))
            {
                Object.Destroy(obj.gameObject);

                i++;
            }

            Debug.Log($"Pool reset count: {i}");

            Pool.Clear();
        }
    }
}