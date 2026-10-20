// <copyright project="Unity.Entities" file="EntityDataAccess.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Entities;
using UnityEntities = Unity.Entities;

namespace NZCore.Internal
{
    // Engine-owned and only reached through pointers. Wraps Unity's access so the layout stays identical; all calls delegate to Unity.
    public unsafe struct EntityDataAccess
    {
        internal UnityEntities.EntityDataAccess Value;

        public EntityComponentStore* EntityComponentStore => (EntityComponentStore*)Value.EntityComponentStore;
        public ComponentDependencyManager* DependencyManager => (ComponentDependencyManager*)Value.DependencyManager;
        public bool IsInExclusiveTransaction => Value.IsInExclusiveTransaction;

        public bool IsComponentEnabled(Entity entity, TypeIndex typeIndex, ref LookupCache typeLookupCache) =>
            Value.IsComponentEnabled(entity, typeIndex, ref LookupCache.AsUnity(ref typeLookupCache));

        public void SetComponentEnabled(Entity entity, TypeIndex typeIndex, bool value, ref LookupCache typeLookupCache) =>
            Value.SetComponentEnabled(entity, typeIndex, value, ref LookupCache.AsUnity(ref typeLookupCache));

        public T GetSharedComponentData_Unmanaged<T>(Entity entity) where T : unmanaged, ISharedComponentData =>
            Value.GetSharedComponentData_Unmanaged<T>(entity);

        public T GetSharedComponentData_Unmanaged<T>(int sharedComponentIndex) where T : unmanaged =>
            Value.GetSharedComponentData_Unmanaged<T>(sharedComponentIndex);

        public EntityComponentStore.ArchetypeChanges BeginStructuralChanges() => new() { Value = Value.BeginStructuralChanges() };

        public void EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes) => Value.EndStructuralChanges(ref changes.Value);

        public void AddSharedComponentDataDuringStructuralChange_Unmanaged(NativeArray<Entity> entities, ComponentType componentType, void* componentData,
            void* defaultComponentData, in SystemHandle originSystem = default) =>
            Value.AddSharedComponentDataDuringStructuralChange_Unmanaged(entities, componentType, componentData, defaultComponentData, in originSystem);
    }
}