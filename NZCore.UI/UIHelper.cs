#if UNITY_2023_3_0
using System;
using System.Runtime.InteropServices;
using BovineLabs.Core.UI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public unsafe struct UIHelper<T, TD>
        where T : class, IBindingObjectNotify<TD>, new()
        where TD : unmanaged, IBindingObjectNotifyData
    {
        private readonly FixedString128Bytes uniqueKey;
        private readonly FixedString128Bytes assetKey;
        private readonly int priority;
        private readonly bool visibleOnInstantiate;

        private GCHandle handle;
        private TD* data;

        public UIHelper(string uniqueKey, string assetKey, int priority = 0, bool visibleOnInstantiate = true)
        {
            this.uniqueKey = uniqueKey;
            this.assetKey = assetKey;
            this.priority = priority;
            this.visibleOnInstantiate = visibleOnInstantiate;
            
            handle = default;
            data = default;
        }

        public ref TD Model => ref UnsafeUtility.AsRef<TD>(data);

        public VisualElement Load(string containerName = null, string elementName = null)
        {
            var (ve, binding) = UIToolkitManager.Instance.AddInterface<T>(uniqueKey.ToString(), assetKey.ToString(), containerName, elementName, priority, visibleOnInstantiate);

            handle = GCHandle.Alloc(binding.Value, GCHandleType.Pinned);
            data = (TD*)UnsafeUtility.AddressOf(ref binding.Value);

            binding.Load();

            return ve;
        }
        
        public VisualElement Load(VisualElement container, string elementName = null)
        {
            var (ve, binding) = UIToolkitManager.Instance.AddInterface<T>(uniqueKey.ToString(), assetKey.ToString(), container, elementName, priority, visibleOnInstantiate);

            handle = GCHandle.Alloc(binding.Value, GCHandleType.Pinned);
            data = (TD*)UnsafeUtility.AddressOf(ref binding.Value);

            binding.Load();

            return ve;
        }
        
        public void Unload()
        {
            var binding = UIToolkitManager.Instance.RemovePanel(uniqueKey.ToString());

            if (handle.IsAllocated)
            {
                binding.Unload();
                if (binding is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                handle.Free();
                handle = default;
                data = default;
            }
        }
    }
    
    // public unsafe struct UIHelperForPtr<T, TD>
    //     where T : IBindingPtrObject<TD>, new()
    //     where TD : unmanaged
    // {
    //     private readonly FixedString128Bytes key;
    //     private readonly int priority;
    //     private readonly bool visibleOnInstantiate;
    //
    //     private GCHandle handle;
    //     private TD* dataPtr;
    //
    //     public UIHelperForPtr(string key, int priority = 0, bool visibleOnInstantiate = true)
    //     {
    //         this.key = key;
    //         this.priority = priority;
    //         this.visibleOnInstantiate = visibleOnInstantiate;
    //         
    //         handle = default;
    //         dataPtr = null;
    //     }
    //
    //     public ref TD Model => ref UnsafeUtility.AsRef<TD>(dataPtr);
    //     
    //     public void Load(TD* ptr, string containerName = null, string elementName = null)
    //     {
    //         var binding = new T();
    //         UIToolkitManager.Instance.AddInterface(key.ToString(), binding, containerName, elementName, priority, visibleOnInstantiate);
    //
    //         handle = GCHandle.Alloc(binding, GCHandleType.Pinned);
    //         dataPtr = ptr;
    //         binding.Ptr = ptr;
    //
    //         binding.Load();
    //     }
    //
    //     public void Unload()
    //     {
    //         UIToolkitManager.Instance.RemovePanel(key.ToString());
    //
    //         if (handle.IsAllocated)
    //         {
    //             var obj = (T)this.handle.Target;
    //             obj.Unload();
    //             if (obj is IDisposable disposable)
    //             {
    //                 disposable.Dispose();
    //             }
    //
    //             handle.Free();
    //             handle = default;
    //             dataPtr = null;
    //         }
    //     }
    // }
}
#endif