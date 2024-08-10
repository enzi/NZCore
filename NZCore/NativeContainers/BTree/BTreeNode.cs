// <copyright project="NZCore" file="BTreeNode.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore.NativeContainers.BTree
{
    public struct BTreeNode<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private readonly int degree;
        public UnsafeList<BTreeNode<TKey, TValue>> Children { get; set; }
        public UnsafeList<BTreeEntry<TKey, TValue>> Entries { get; set; }

        public bool IsLeaf => Children.Length == 0;
        public bool HasReachedMaxEntries => Entries.Length == (2 * degree) - 1;
        public bool HasReachedMinEntries => Entries.Length == degree - 1;

        public BTreeNode(int degree)
        {
            this.degree = degree;

            Children = new UnsafeList<BTreeNode<TKey, TValue>>(degree, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            Entries = new UnsafeList<BTreeEntry<TKey, TValue>>(degree, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }
    }
}