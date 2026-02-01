// <copyright project="NZCore" file="SingletonUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using Unity.Entities;

namespace NZCore
{
    public static class SingletonUtility
    {
        public static T CreateManagedSingleton<T>(this ref SystemState state)
            where T : class, IInitSingleton
        {
            var compType = ComponentType.ReadOnly<T>();
            var singletonEntity = state.EntityManager.CreateEntity(stackalloc[]
            {
                compType
            });
            
            T singletonData = Activator.CreateInstance<T>();
            singletonData.Init();
            state.EntityManager.SetComponentObject(singletonEntity, compType, singletonData);

            return singletonData;
        }
        
        public static void CreateSingleton<T>(this ref SystemState state)
            where T : unmanaged, IInitSingleton
        {
            var singletonEntity = state.EntityManager.CreateEntity(stackalloc[]
            {
                ComponentType.ReadOnly<T>()
            });
            
            T singletonData = default;
            singletonData.Init();
            state.EntityManager.SetComponentData(singletonEntity, singletonData);
        }
        
        public static void CreateSingleton<T>(this ref SystemState state, out Entity singletonEntity)
            where T : unmanaged, IInitSingleton
        {
            singletonEntity = state.EntityManager.CreateEntity(stackalloc[]
            {
                ComponentType.ReadOnly<T>()
            });
            
            T singletonData = default;
            singletonData.Init();
            state.EntityManager.SetComponentData(singletonEntity, singletonData);
        }
        
        public static void CreateSingleton<T>(this ref SystemState state, out T singletonData)
            where T : unmanaged, IInitSingleton
        {
            var singletonEntity = state.EntityManager.CreateEntity(stackalloc[]
            {
                ComponentType.ReadOnly<T>()
            });
            
            singletonData = default;
            singletonData.Init();
            state.EntityManager.SetComponentData(singletonEntity, singletonData);
        }

        public static void CreateSingleton<T>(this ref SystemState state, out Entity singletonEntity, out T singletonData)
            where T : unmanaged, IInitSingleton
        {
            singletonEntity = state.EntityManager.CreateEntity(stackalloc[]
            {
                ComponentType.ReadOnly<T>()
            });
            
            singletonData = default;
            singletonData.Init();
            state.EntityManager.SetComponentData(singletonEntity, singletonData);
        }

        public static void DisposeSingleton<T>(this ref SystemState state)
            where T : unmanaged, IComponentData, IDisposable
        {
            state.GetSingleton<T>().Dispose();
            state.EntityManager.DestroyEntity(state.GetSingletonEntity<T>());
        }
    }
}