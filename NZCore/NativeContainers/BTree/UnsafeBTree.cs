// <copyright project="NZCore" file="UnsafeBTree.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using Unity.Collections;

namespace NZCore.NativeContainers.BTree
{
    public unsafe struct UnsafeBTree<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public BTreeNode<TKey, TValue> Root;
        public int Degree;
        public int Height;

        private AllocatorManager.AllocatorHandle allocator;

        internal static UnsafeBTree<TKey, TValue>* Create<TAllocator>(int degree, ref TAllocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            if (degree < 2)
            {
                throw new ArgumentException($"BTree degree must be at least 2 not {degree}");
            }

            UnsafeBTree<TKey, TValue>* unsafeBTree = allocator.Allocate(default(UnsafeBTree<TKey, TValue>), 1);

            unsafeBTree->allocator = allocator.Handle;
            unsafeBTree->Root = new BTreeNode<TKey, TValue>(degree);
            unsafeBTree->Degree = degree;
            unsafeBTree->Height = 1;

            return unsafeBTree;
        }


        // public BTreeEntry<TKey, TValue> Search(TKey key)
        // {
        //     return this.SearchInternal(this.Root, key);
        // }

        // private BTreeEntry<TKey, TValue> SearchInternal(BTreeNode<TKey, TValue> node, TKey key)
        // {
        //     int i = node.Entries.TakeWhile(entry => key.CompareTo(entry.Key) > 0).Count();
        //
        //     if (i < node.Entries.Count && node.Entries[i].Key.CompareTo(key) == 0)
        //     {
        //         return node.Entries[i];
        //     }
        //
        //     return node.IsLeaf ? null : this.SearchInternal(node.Children[i], key);
        // }


        public static void Destroy(UnsafeBTree<TKey, TValue>* hashMap)
        {
            var allocator = hashMap->allocator;
            hashMap->Dispose();
            AllocatorManager.Free(allocator, hashMap);
        }

        public void Dispose()
        {
            //UnsafeBTree<MultipleArrayIndexer>.Destroy(next, ref allocator);
            //UnsafeList<MultipleArrayIndexer>.Destroy(buckets, ref allocator);
        }
    }
}