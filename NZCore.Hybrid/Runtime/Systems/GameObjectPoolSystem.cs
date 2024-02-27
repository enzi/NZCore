using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace NZCore.Hybrid
{
    public class GameObjectPoolSingleton : IComponentData
    {
        public GameObjectPoolSingleton()
        {
            
        }
    }
    
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GameObjectPoolSystem : SystemBase
    {
        private Dictionary<int, Stack<GameObject>> Pool;
        private List<GameObject> activeObjects;
        

        protected override void OnCreate()
        {
            Pool = new Dictionary<int, Stack<GameObject>>();
            activeObjects = new List<GameObject>();

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

            var inst = MonoBehaviour.Instantiate(prefab);

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

        public void Reset()
        {
            Debug.Log("Pool Reset");
            int i = 0;
            foreach (var entry in Pool)
            {
                while (entry.Value.Count > 0)
                {
                    var obj = entry.Value.Pop();
                    MonoBehaviour.Destroy(obj.gameObject);

                    i++;
                }
            }

            foreach (var obj in MonoBehaviour.FindObjectsOfType<GameObjectPrefabID>())
            {
                MonoBehaviour.Destroy(obj.gameObject);
                
                i++;
            }
            
            Debug.Log($"Pool reset count: {i}");
            
            Pool.Clear();
        }
    }
}