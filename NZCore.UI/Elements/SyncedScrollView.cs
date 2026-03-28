// <copyright project="NZCore.UI" file="SyncedScrollView.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    public abstract unsafe class SyncedScrollViewBase : ScrollView
    {
        protected static readonly BindingId MakeItemBinding = (BindingId)"makeItem";
        protected static readonly BindingId ItemsSourceBinding = (BindingId)"itemsSource";
    }

    public abstract unsafe class SyncedScrollView<TStructData, TViewData> : SyncedScrollViewBase
        where TStructData : unmanaged
    {
        private SyncListCommand<TStructData> _onSyncList;
        private Func<VisualElement> _makeItem;
        private IList _itemsSource;

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

                if (_onSyncList != null)
                {
                    _onSyncList.SyncList += OnSyncListOnSyncList;
                }
            }
        }

        [CreateProperty]
        public Func<VisualElement> makeItem
        {
            get => _makeItem;
            set
            {
                if (value == _makeItem)
                {
                    return;
                }

                _makeItem = value;
                NotifyPropertyChanged(in MakeItemBinding);
            }
        }

        [CreateProperty]
        public IList itemsSource
        {
            get => _itemsSource;
            set
            {
                _itemsSource = value;
                Rebuild();
                NotifyPropertyChanged(in ItemsSourceBinding);
            }
        }

        private void OnSyncListOnSyncList(UnsafeList<TStructData> obj)
        {
            SyncData(obj);
        }

        protected SyncedScrollView()
        {
            _items = new List<TViewData>();
            _trackArray = new NativeList<TStructData>(0, Allocator.Persistent);

            RegisterCallback<AttachToPanelEvent>(_ => itemsSource = _items);
            RegisterCallback<DetachFromPanelEvent>(_ => _trackArray.Dispose());
        }

        public void SyncData(UnsafeList<TStructData> list)
        {
            if (_trackArray.Length != list.Length)
            {
                _items.Clear();
                foreach (var data in list)
                {
                    _items.Add(CreateItem(data));
                }

                _trackArray.CopyFrom(list);
                RefreshItems();
                return;
            }

            if (list.Length == 0)
            {
                return;
            }

            var ptr1 = list.Ptr;
            var ptr2 = _trackArray.GetUnsafeReadOnlyPtr();

            for (var i = 0; i < list.Length; i++)
            {
                if (CompareElements(ref *(ptr2 + i), ref *(ptr1 + i)))
                {
                    continue;
                }

                _items[i] = UpdateItem(_items[i], ptr1[i]);
                RefreshItem(i);
            }

            _trackArray.CopyFrom(list);
        }

        private void Rebuild()
        {
            contentContainer.Clear();

            if (_itemsSource == null || _makeItem == null)
            {
                return;
            }

            foreach (var item in _itemsSource)
            {
                var element = _makeItem();
                element.dataSource = item;
                contentContainer.Add(element);
            }
        }

        public void RefreshItems()
        {
            if (_itemsSource == null)
            {
                Debug.LogError("ItemSource is null");
                return;
            }

            if (contentContainer.childCount != _itemsSource.Count)
            {
                Rebuild();
                return;
            }

            for (var i = 0; i < contentContainer.childCount; i++)
            {
                contentContainer.ElementAt(i).dataSource = _itemsSource[i];
            }
        }

        public void RefreshItem(int index)
        {
            contentContainer.ElementAt(index).dataSource = _itemsSource[index];
        }

        protected abstract TViewData CreateItem(TStructData data);
        protected abstract TViewData UpdateItem(TViewData current, TStructData newData);

        /// <summary>
        /// Override for custom compare.
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