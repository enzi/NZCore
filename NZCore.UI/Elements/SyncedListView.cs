// <copyright project="NZCore.UI" file="SyncedListView.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.MVVM;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UI.Elements
{
    public abstract unsafe class SyncedListView<TStructData, TViewData> : ListView
        where TStructData : unmanaged
    {
        private SyncListCommand<TStructData> _onSyncList;

        private List<TViewData> _items;
        private NativeList<TStructData> _trackArray;


        [CreateProperty]
        public SyncListCommand<TStructData> syncList
        {
            get => _onSyncList;
            set
            {
                if (_onSyncList != null)
                {
                    _onSyncList.SyncList -= OnSyncListOnSyncList;
                }

                _onSyncList = value;

                // Subscribe to new command
                if (_onSyncList != null)
                {
                    _onSyncList.SyncList += OnSyncListOnSyncList;
                }
            }
        }

        private void OnSyncListOnSyncList(UnsafeList<TStructData> obj)
        {
            SyncData(obj);
        }

        protected SyncedListView()
        {
            _items = new List<TViewData>();
            _trackArray = new NativeList<TStructData>(0, Allocator.Persistent);

            RegisterCallback<AttachToPanelEvent>(_ => itemsSource = _items);
            RegisterCallback<DetachFromPanelEvent>(_ => _trackArray.Dispose());

            bindItem = (element, index) => element.dataSource = itemsSource[index];
        }

        public void SyncData(UnsafeList<TStructData> list)
        {
            if (_trackArray.Length != list.Length)
            {
                _items.Clear();
                foreach (var element in list)
                {
                    _items.Add(CreateItem(element));
                }

                _trackArray.CopyFrom(list);
                RefreshItems();
                return;
            }

            var ptr1 = list.Ptr;
            var ptr2 = _trackArray.GetUnsafeReadOnlyPtr();

            for (var i = 0; i < list.Length; i++)
            {
                // todo what if _trackArray is smaller. it reads beyond memory
                if (CompareElements(ref *(ptr2 + i), ref *(ptr1 + i)))
                {
                    continue;
                }

                _items[i] = UpdateItem(_items[i], ptr1[i]);
                RefreshItem(i);
            }

            _trackArray.CopyFrom(list);
        }

        protected abstract TViewData CreateItem(TStructData data);
        protected abstract TViewData UpdateItem(TViewData current, TStructData newData);

        /// <summary>
        /// Override for custom compare
        /// </summary>
        /// <param name="current">The data in the change track array</param>
        /// <param name="newData">The list element of the SyncData parameter</param>
        /// <returns>Returns true when elements are the same</returns>
        public virtual bool CompareElements(ref TStructData current, ref TStructData newData)
        {
            var ptr1 = UnsafeUtility.AddressOf(ref current);
            var ptr2 = UnsafeUtility.AddressOf(ref newData);

            return UnsafeUtility.MemCmp(ptr1, ptr2, UnsafeUtility.SizeOf<TStructData>()) == 0;
        }
    }
}