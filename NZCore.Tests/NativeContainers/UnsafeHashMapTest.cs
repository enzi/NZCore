// <copyright project="NZCore.Tests" file="UnsafeHashMapTest.cs">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.PerformanceTesting;
using NZCore;
using UnityEngine;

namespace NZCore.Tests.NativeContainers
{
    public class UnsafeHashMapTest
    {
        [TestCase]
        [Performance]
        public void Test_UnsafeHashMapSerialize()
        {
            UnsafeHashMap<int, char> hashMap = new UnsafeHashMap<int, char>(100, Allocator.Temp);

            {
                // add data
                hashMap.Add(100, 'c');
                hashMap.Add(200, 'd');
                hashMap.Add(300, 'e');
                hashMap.Add(400, 'f');
                hashMap.Add(500, 'g');
                hashMap.Add(600, 'h');
                hashMap.Add(700, 'i');
            }

            var serializer = new ByteSerializer(0, Allocator.Temp);
            hashMap.Serialize(ref serializer);
            
            var deserializer = new ByteDeserializer(serializer.Data.ToArray(Allocator.Temp));

            UnsafeHashMap<int, char> hashMap2 = default;
            
            hashMap2.Deserialize(ref deserializer, Allocator.Temp);

            var data = hashMap2.GetKeyValueArrays(Allocator.Temp);

            
            for (int i =0; i < data.Length;i++)
            {
                var key = data.Keys[i];
                var value = data.Values[i];
                Debug.Log($"{key}/{value}");
            }
            
        }
    }
}