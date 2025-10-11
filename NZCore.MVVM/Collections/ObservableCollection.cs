// <copyright project="NZCore.MVVM" file="ObservableCollection.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using NZCore.MVVM;

namespace NZCore.MVVM
{
    /// <summary>
    /// Represents a dynamic data collection that provides notifications when items get added, removed, or when the whole list is refreshed.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    public class ObservableCollection<T> : IList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    {
        private readonly List<T> _items = new();
        private bool _disposed;

        /// <summary>
        /// Occurs when the collection changes.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the number of elements contained in the collection.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Gets a value indicating whether the collection is read-only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        /// <returns>The element at the specified index.</returns>
        public T this[int index]
        {
            get => _items[index];
            set
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ObservableCollection<T>));

                var oldItem = _items[index];
                _items[index] = value;

                OnPropertyChanged("Item[]");
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, oldItem, index));
            }
        }

        /// <summary>
        /// Initializes a new instance of the ObservableCollection class.
        /// </summary>
        public ObservableCollection()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ObservableCollection class with the specified items.
        /// </summary>
        /// <param name="items">The items to initialize the collection with.</param>
        public ObservableCollection(IEnumerable<T> items)
        {
            if (items != null)
            {
                _items.AddRange(items);
            }
        }

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void Add(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            _items.Add(item);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _items.Count - 1));
        }

        /// <summary>
        /// Adds multiple items to the collection.
        /// </summary>
        /// <param name="items">The items to add.</param>
        public void AddRange(IEnumerable<T> items)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            if (items == null)
                return;

            var itemsList = items.ToList();
            if (itemsList.Count == 0)
                return;

            var startIndex = _items.Count;
            _items.AddRange(itemsList);

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsList, startIndex));
        }

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        public void Clear()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            if (_items.Count == 0)
                return;

            _items.Clear();
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Determines whether the collection contains a specific item.
        /// </summary>
        /// <param name="item">The item to locate.</param>
        /// <returns>True if the item is found; otherwise, false.</returns>
        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        /// <summary>
        /// Copies the elements of the collection to an array.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator for the collection.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        /// <summary>
        /// Determines the index of a specific item in the collection.
        /// </summary>
        /// <param name="item">The item to locate.</param>
        /// <returns>The index of the item if found; otherwise, -1.</returns>
        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
        }

        /// <summary>
        /// Inserts an item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which to insert the item.</param>
        /// <param name="item">The item to insert.</param>
        public void Insert(int index, T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            _items.Insert(index, item);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        /// <summary>
        /// Removes the first occurrence of a specific item from the collection.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>True if the item was successfully removed; otherwise, false.</returns>
        public bool Remove(T item)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            var index = _items.IndexOf(item);
            if (index < 0)
                return false;

            _items.RemoveAt(index);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            return true;
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            var item = _items[index];
            _items.RemoveAt(index);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        /// <summary>
        /// Removes multiple items from the collection.
        /// </summary>
        /// <param name="items">The items to remove.</param>
        /// <returns>The number of items successfully removed.</returns>
        public int RemoveRange(IEnumerable<T> items)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            if (items == null)
                return 0;

            var removedItems = new List<T>();
            foreach (var item in items)
            {
                if (_items.Remove(item))
                {
                    removedItems.Add(item);
                }
            }

            if (removedItems.Count > 0)
            {
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged("Item[]");
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            return removedItems.Count;
        }

        /// <summary>
        /// Replaces all items in the collection with the specified items.
        /// </summary>
        /// <param name="items">The new items.</param>
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            _items.Clear();
            if (items != null)
            {
                _items.AddRange(items);
            }

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Moves an item from one position to another.
        /// </summary>
        /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
        /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
        public void Move(int oldIndex, int newIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ObservableCollection<T>));

            if (oldIndex == newIndex)
                return;

            var item = _items[oldIndex];
            _items.RemoveAt(oldIndex);
            _items.Insert(newIndex, item);

            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator for the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Raises the CollectionChanged event.
        /// </summary>
        /// <param name="e">The event args.</param>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Disposes the collection and clears all event handlers.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CollectionChanged = null;
            PropertyChanged = null;
            _items.Clear();
        }
    }
}