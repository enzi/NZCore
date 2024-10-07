// <copyright project="NZCore" file="SingletonUtility.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using Unity.Entities;

namespace NZCore
{
    public static class SingletonUtility
    {
        public static void CreateSingleton<T>(this ref SystemState state)
            where T : unmanaged, IInitSingleton
        {
            var singletonEntity = state.EntityManager.CreateEntity();
            T singletonData = default;
            singletonData.Init();
            state.EntityManager.AddComponentData(singletonEntity, singletonData);
        }

        public static void CreateSingleton<T>(this ref SystemState state, out T singletonData)
            where T : unmanaged, IInitSingleton
        {
            var singletonEntity = state.EntityManager.CreateEntity();
            singletonData = default;
            singletonData.Init();
            state.EntityManager.AddComponentData(singletonEntity, singletonData);
        }

        public static void CreateSingleton<T>(this ref SystemState state, out Entity singletonEntity, out T singletonData)
            where T : unmanaged, IInitSingleton
        {
            singletonEntity = state.EntityManager.CreateEntity();
            singletonData = default;
            singletonData.Init();
            state.EntityManager.AddComponentData(singletonEntity, singletonData);
        }

        public static void DisposeSingleton<T>(this ref SystemState state)
            where T : unmanaged, IComponentData, IDisposable
        {
            state.GetSingleton<T>().Dispose();
            state.EntityManager.DestroyEntity(state.GetSingletonEntity<T>());
        }
    }
}