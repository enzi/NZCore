// <copyright project="NZCore.Hybrid" file="GameObjectPoolSystem.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Hybrid
{
    public class GameObjectPoolSingleton : IComponentData { }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GameObjectPoolSystem : SystemBase
    {
#if UNITY_6000_4_OR_NEWER
        private Dictionary<EntityId, Stack<GameObject>> _pool;
#else
        private Dictionary<int, Stack<GameObject>> _pool;
#endif


        protected override void OnCreate()
        {
#if UNITY_6000_4_OR_NEWER
            _pool = new Dictionary<EntityId, Stack<GameObject>>();
#else
            _pool = new Dictionary<int, Stack<GameObject>>();
#endif
            Enabled = false; // this sytem has no Update
        }

        protected override void OnUpdate() { }

        public GameObject Get(GameObject prefab, out bool freshInstance)
        {
#if UNITY_6000_4_OR_NEWER
            var key = prefab.GetEntityId();
#else
            var key = prefab.GetInstanceID();
#endif

            if (_pool.TryGetValue(key, out var poolableObjects) && poolableObjects.Count > 0)
            {
                var poolableObj = poolableObjects.Pop();

                poolableObj.gameObject.SetActive(true);

                freshInstance = false;

                return poolableObj;
            }

            var inst = Object.Instantiate(prefab);

            var prefabId = inst.AddComponent<GameObjectPrefabID>();
            prefabId.PrefabId = key;

            freshInstance = true;

            return inst;
        }

        public void Release(GameObject pooledObject)
        {
            if (!pooledObject.TryGetComponent(out GameObjectPrefabID goPrefabId))
            {
                return;
            }

            var prefabId = goPrefabId.PrefabId;

            pooledObject.SetActive(false);

            if (_pool.TryGetValue(prefabId, out var poolableObjects))
            {
                poolableObjects.Push(pooledObject);
                pooledObject.gameObject.SetActive(false);
            }
            else
            {
                var stack = new Stack<GameObject>();
                stack.Push(pooledObject);
                _pool.Add(prefabId, stack);
            }
        }

        public void Unload()
        {
            Debug.Log("Pool Reset");
            var i = 0;
            foreach (var entry in _pool)
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

            _pool.Clear();
        }
    }
}