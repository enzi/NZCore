// <copyright project="NZCore" file="EntityLock.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZSpellCasting;
using Unity.Entities;

namespace NZCore.Utility
{
    public ref struct EntityLockGuard
    {
        private readonly RefRW<SpinLockComponent> _lockComponent;
        private bool _acquired;
    
        public EntityLockGuard(ref UnsafeComponentLookup<SpinLockComponent> lockLookup, Entity entity)
        {
            _lockComponent = lockLookup.GetRefRW(entity, false);
            _acquired = true;
            _lockComponent.ValueRW.SpinLock.Acquire();
        }
    
        public void Dispose()
        {
            if (!_acquired)
            {
                return;
            }

            _lockComponent.ValueRW.SpinLock.Release();
            _acquired = false;
        }
    }

    public static class EntityLockExtensions
    {
        public static EntityLockGuard Lock(this ref UnsafeComponentLookup<SpinLockComponent> lockLookup, Entity entity)
        {
            return new EntityLockGuard(ref lockLookup, entity);
        }
    }
}