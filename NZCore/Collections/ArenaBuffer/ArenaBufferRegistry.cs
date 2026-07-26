// <copyright project="NZCore" file="ArenaBufferRegistry.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NZCore
{
    /// <summary>Context markers for the registry's <see cref="SharedStatic{T}"/> key.</summary>
    public struct ArenaBufferRegistryContext
    {
    }

    /// <inheritdoc cref="ArenaBufferRegistryContext"/>
    public struct ArenaBufferRegistrationList
    {
    }

    /// <inheritdoc cref="ArenaBufferRegistryContext"/>
    public struct ArenaBufferReserveSystemCounter
    {
    }

    /// <summary>One registered arena buffer element type, in the layout the reserve system iterates.</summary>
    public struct ArenaTypeRegistration
    {
        /// <summary>The generated <c>*Ref</c> component holding the block state.</summary>
        public TypeIndex RefTypeIndex;

        public IntPtr Arena;

        /// <summary>Which allocator <see cref="Arena"/> points at, so untyped code can cast it correctly.</summary>
        public ArenaAllocatorMode Mode;

        /// <summary>
        /// Bytes per element of this type. Only <see cref="ArenaAllocatorMode.SharedChunkPaged"/> needs it -
        /// its arena is byte oriented and cannot know the sizes of the types sharing it - but the untyped
        /// release and reserve paths read it uniformly rather than branching.
        /// </summary>
        public int ElementSize;
    }

    /// <summary>
    /// Central list of every registered <see cref="IArenaBuffer"/> element type. The generated registration
    /// hook of each type calls <see cref="Register{TElement,TRef}"/> once at startup, which creates
    /// the type's <see cref="ArenaAllocator"/> and publishes it to <see cref="ArenaBufferStorage{T}"/>.
    ///
    /// The list is what lets a single untyped <c>ArenaBufferReserveSystem</c> serve every type: all generated
    /// ref components share the <see cref="ArenaBufferRefData"/> layout, so the system can reinterpret them
    /// from a <see cref="DynamicComponentTypeHandle"/> without knowing the concrete type.
    /// </summary>
    public static unsafe class ArenaBufferRegistry
    {
        private static bool initialized;
        private static bool appDomainUnloadRegistered;

        [NativeDisableUnsafePtrRestriction]
        private static UnsafeList<ArenaTypeRegistration>* registrations;

        // Baking runs managed and editor only, so a delegate per element type is the cheapest way to let
        // baker.AddArenaBuffer<T>() reach the concrete generated ref component.
        private static Dictionary<Type, Action<IBaker, Entity, int>> bakerAdders;

        // SharedStatic context types have to be real types, not static classes, hence the two markers.
        private static readonly SharedStatic<IntPtr> RegistrationsRef =
            SharedStatic<IntPtr>.GetOrCreate<ArenaBufferRegistryContext, ArenaBufferRegistrationList>();

        /// <summary>The registered types, readable from Burst compiled code.</summary>
        public static UnsafeList<ArenaTypeRegistration>* GetRegistrations()
        {
            return (UnsafeList<ArenaTypeRegistration>*)RegistrationsRef.Data;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            TypeManager.Initialize();

            registrations = (UnsafeList<ArenaTypeRegistration>*)Memory.Unmanaged.Allocate(
                UnsafeUtility.SizeOf<UnsafeList<ArenaTypeRegistration>>(), UnsafeUtility.AlignOf<UnsafeList<ArenaTypeRegistration>>(), Allocator.Persistent);
            *registrations = new UnsafeList<ArenaTypeRegistration>(8, Allocator.Persistent);

            RegistrationsRef.Data = (IntPtr)registrations;

            bakerAdders = new Dictionary<Type, Action<IBaker, Entity, int>>();

            if (!appDomainUnloadRegistered)
            {
                AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
                AppDomain.CurrentDomain.ProcessExit += OnDomainUnload;

                appDomainUnloadRegistered = true;
            }
        }

        /// <summary>
        /// Registers an element type and creates its arena. Called by the generated registration hook.
        /// Re-registering the same type reuses the existing arena, which is what happens when entering play
        /// mode with domain reload disabled.
        /// </summary>
        public static void Register<TElement, TRef>(int initialCapacity, ArenaAllocatorMode mode, Action<IBaker, Entity, int> bakerAdd)
            where TElement : unmanaged, IArenaBuffer
            where TRef : unmanaged, IComponentData
        {
            Initialize();

            bakerAdders[typeof(TElement)] = bakerAdd;

            var refTypeIndex = TypeManager.GetTypeIndex<TRef>();

            for (var i = 0; i < registrations->Length; i++)
            {
                if ((*registrations)[i].RefTypeIndex == refTypeIndex)
                {
                    // Already registered, keep the existing arena so live buffers stay valid.
                    ArenaBufferStorage<TElement>.Arena.Data = (*registrations)[i].Arena;
                    return;
                }
            }

            var elementSize = UnsafeUtility.SizeOf<TElement>();
            var elementAlign = UnsafeUtility.AlignOf<TElement>();

            IntPtr arena;

            switch (mode)
            {
                case ArenaAllocatorMode.SharedChunkPaged:
                    arena = GetOrCreateSharedArena();
                    break;
                case ArenaAllocatorMode.Contiguous:
                    arena = (IntPtr)ContiguousArenaAllocator.Create(elementSize, elementAlign, initialCapacity);
                    break;
                case ArenaAllocatorMode.ChunkPaged:
                    arena = (IntPtr)ChunkPagedArenaAllocator.Create(elementSize, elementAlign, initialCapacity);
                    break;
                default:
                    arena = (IntPtr)ArenaAllocator.Create(elementSize, elementAlign, initialCapacity);
                    break;
            }

            ArenaBufferStorage<TElement>.Arena.Data = arena;

            registrations->Add(new ArenaTypeRegistration
            {
                RefTypeIndex = refTypeIndex,
                Arena = arena,
                Mode = mode,
                ElementSize = elementSize
            });
        }

        // Every SharedChunkPaged type points at this one arena - that is what lets a chunk put buffers of
        // different types on the same page. Created on first use rather than at Initialize, so projects with
        // no shared types never allocate it.
        private static IntPtr sharedArena;

        private static IntPtr GetOrCreateSharedArena()
        {
            if (sharedArena == IntPtr.Zero)
            {
                sharedArena = (IntPtr)SharedArenaAllocator.Create();
            }

            return sharedArena;
        }

        // A SharedStatic rather than a plain static: the reserve system maintains this from Burst compiled
        // OnCreate/OnDestroy, and Burst cannot write managed statics.
        private static readonly SharedStatic<int> ReserveSystemCountRef =
            SharedStatic<int>.GetOrCreate<ArenaBufferRegistryContext, ArenaBufferReserveSystemCounter>();

        /// <summary>
        /// Number of live <c>ArenaBufferReserveSystem</c> instances. The leak check only runs when there is
        /// exactly one, because arenas are shared across Worlds and a second World's legitimate blocks would
        /// otherwise read as leaks.
        /// </summary>
        public static int ReserveSystemCount => ReserveSystemCountRef.Data;

        public static void AddReserveSystem()
        {
            ReserveSystemCountRef.Data++;
        }

        public static void RemoveReserveSystem()
        {
            ReserveSystemCountRef.Data--;
        }

        /// <summary>
        /// One shot release of the arena blocks held by a set of chunks, for callers that do not keep
        /// handles around. Builds and throws away a <see cref="ArenaBufferReleaseHandles"/>, so a destroy
        /// pipeline running every frame should hold its own instead.
        /// </summary>
        public static int ReleaseChunks(NativeArray<ArchetypeChunk> chunks, ref SystemState state)
        {
            var handles = ArenaBufferReleaseHandles.Create(ref state);
            var freed = 0;

            for (var i = 0; i < chunks.Length; i++)
            {
                freed += handles.ReleaseChunk(chunks[i]);
            }

            handles.Dispose();
            return freed;
        }

        public static bool TryGetBakerAdder(Type elementType, out Action<IBaker, Entity, int> adder)
        {
            Initialize();
            return bakerAdders.TryGetValue(elementType, out adder);
        }

        /// <summary>
        /// Drops every live block of every registered type without freeing the arenas themselves.
        ///
        /// Arenas are process global rather than per World, so anything that recycles Worlds in one process -
        /// EcsTestsFixture, or entering play mode with domain reload disabled - has to call this, otherwise
        /// blocks belonging to entities that no longer exist keep the arenas growing.
        /// </summary>
        public static void ResetAll()
        {
            if (!initialized)
            {
                return;
            }

            for (var i = 0; i < registrations->Length; i++)
            {
                var registration = (*registrations)[i];

                // Skipped here and reset once below, since every shared type names the same arena.
                if (registration.Mode == ArenaAllocatorMode.SharedChunkPaged)
                {
                    continue;
                }

                ArenaBufferDispatch.Reset(registration.Arena, registration.Mode);
            }

            if (sharedArena != IntPtr.Zero)
            {
                ((SharedArenaAllocator*)sharedArena)->Reset();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            // Runs before AfterAssembliesLoaded, so with domain reload disabled this clears the arenas of the
            // previous play session before anything re-registers or reserves.
            ResetAll();
        }

        private static void OnDomainUnload(object sender, EventArgs e)
        {
            if (!initialized)
            {
                return;
            }

            for (var i = 0; i < registrations->Length; i++)
            {
                var registration = (*registrations)[i];

                // Several registrations point at the one shared arena; it is destroyed once, below.
                if (registration.Mode == ArenaAllocatorMode.SharedChunkPaged)
                {
                    continue;
                }

                ArenaBufferDispatch.Destroy(registration.Arena, registration.Mode);
            }

            if (sharedArena != IntPtr.Zero)
            {
                SharedArenaAllocator.Destroy((SharedArenaAllocator*)sharedArena);
                sharedArena = IntPtr.Zero;
            }

            registrations->Dispose();
            Memory.Unmanaged.Free(registrations, Allocator.Persistent);
            registrations = null;

            RegistrationsRef.Data = IntPtr.Zero;
            bakerAdders = null;
            initialized = false;
        }
    }
}
