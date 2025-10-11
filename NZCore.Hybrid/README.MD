# NZCore.Hybrid

NZCore.Hybrid provides a bridge between Unity's traditional GameObject/MonoBehaviour system and the Entity Component System (ECS). It enables seamless integration of GameObjects with Entities, allowing you to leverage the performance benefits of ECS while maintaining the flexibility of GameObject-based systems for presentation and complex behaviors.

## Overview

The Hybrid system allows you to:
- Spawn GameObjects from Entities
- Link GameObject transforms to Entity positions
- Manage GameObject lifecycles alongside Entity lifecycles
- Use pooling for efficient GameObject reuse
- Integrate with Unity's Addressable Asset System
- Control animations through ECS systems

## Architecture

### Assembly Structure

- **NZCore.Hybrid.Data**: Core data structures and components
- **NZCore.Hybrid.Systems**: ECS systems for hybrid functionality
- **NZCore.Hybrid.Authoring**: Authoring components for editor integration

### Core Components

#### Data Components

- **`HybridAnimator`**: Manages Playable Graph for animation control
- **`HybridObjectBuffer`**: Buffer for tracking GameObject references
- **`CreateHybridObjectRequest`**: Request structure for spawning hybrid objects
- **`TransformEntityMappingSingleton`**: Maps Entity transforms to GameObject transforms

#### Spawn Components

- **`HybridSpawnPrefab`**: Spawn GameObject from prefab reference
- **`HybridSpawnPrefabFromPool`**: Spawn GameObject from prefab using pooling
- **`HybridSpawnAddressable`**: Spawn GameObject from Addressable asset
- **`HybridSpawnAddressableFromPool`**: Spawn GameObject from Addressable with pooling

### Core Systems

#### HybridInstantiationSystem
Handles the creation and instantiation of GameObjects from Entity requests.

**Features:**
- Supports both prefab references and Addressable assets
- Position-based spawning with offset support
- Locator-based spawning (Head, Hands, Spine, Feet, Weapons)
- Automatic Transform-Entity mapping
- Playable Graph creation for animation

#### GameObjectPoolSystem
Manages GameObject pooling to reduce instantiation overhead.

**Features:**
- Automatic pool creation and management
- Prefab ID tracking for pool categorization
- Efficient reuse of deactivated GameObjects
- Pool cleanup and unloading

#### HybridDestroySystem
Handles cleanup of GameObjects when their associated Entities are destroyed.

**Features:**
- Automatic GameObject destruction on Entity cleanup
- Buffer-based destruction for multiple objects per Entity

#### LoadAddressablesSystem
Manages loading and unloading of Addressable assets.

**Features:**
- Async asset loading with caching
- Hash-based asset identification
- Automatic handle management
- Bulk unloading capabilities

#### TransformEntityMappingSystem
Synchronizes transforms between GameObjects and Entities.

**Features:**
- Bidirectional transform synchronization
- Efficient batch processing using TransformAccessArray
- Entity-to-GameObject lookup optimization
- Lifecycle management for tracked transforms

## Usage Examples

### Basic Hybrid Entity Setup

```cs
// Create a hybrid entity with a GameObject prefab
var entity = entityManager.CreateEntity();
entityManager.AddComponent<HybridSpawnPrefab>(entity);
entityManager.SetComponentData(entity, new HybridSpawnPrefab
{
    HybridEntity = entity,
    Prefab = new WeakObjectReference<GameObject>(myPrefab),
    SetTransform = 1,
    DestroyWithEntity = 1
});
```

### Using Addressable Assets

```cs
// Spawn from Addressable asset
var entity = entityManager.CreateEntity();
entityManager.AddComponent<HybridSpawnAddressable>(entity);
entityManager.SetComponentData(entity, new HybridSpawnAddressable
{
    HybridEntity = entity,
    AddressableHash = new Hash128(addressableGUID),
    SetTransform = 1,
    DestroyWithEntity = 1
});
```

### Request-Based Spawning

```cs
// Create spawn request
var requestSingleton = SystemAPI.GetSingleton<CreateHybridObjectRequestSingleton>();
var writer = requestSingleton.Requests.AsWriter();

writer.BeginForEachChunk(chunkIndex);
writer.Write(new CreateHybridObjectRequest
{
    PrefabToLoad = new WeakObjectReference<GameObject>(prefab),
    BindToEntity = targetEntity,
    SpawnMode = CreateHybridSpawnMode.Position,
    PositionData = new HybridPositionData
    {
        Position = spawnPosition,
        Offset = Vector3.zero
    }
});
writer.EndForEachChunk();
```

### Locator-Based Spawning

```cs
// Spawn at specific body locations
var request = new CreateHybridObjectRequest
{
    PrefabToLoad = new WeakObjectReference<GameObject>(weaponPrefab),
    BindToEntity = characterEntity,
    SpawnMode = CreateHybridSpawnMode.Locator,
    LocatorData = new HybridLocatorData
    {
        Entity = characterEntity,
        Locator = LocatorPosition.HandRight,
        AttachToParent = true
    }
};
```

### Animation Control

```cs
// Get the hybrid animator component
var hybridAnimator = SystemAPI.GetComponent<HybridAnimator>(entity);

// Change animation clip
hybridAnimator.ChangeClip(newClip, speed: 1.5f);

// Set animation time
hybridAnimator.SetTime(0.5f); // 50% through animation

// Reset to default controller
hybridAnimator.Reset();
```

## Authoring Workflow

### HybridEntity_Authoring Component

The authoring component provides editor integration for setting up hybrid entities:

```cs
public class HybridEntity_Authoring : MonoBehaviour
{
    public HybridEntityResourceType resourceType; // GameObject or Addressable
    public GameObject prefab;                      // Direct prefab reference
    public AssetReference addressable;             // Addressable asset reference
    public bool setTransform;                      // Sync transform with Entity
    public bool usePooling;                        // Use object pooling
    public bool DestroyWithEntity;                 // Destroy GameObject with Entity
}
```

### Locator Setup

Create a `Locators` component on your character GameObjects to define spawn points:

```cs
public class Locators : MonoBehaviour
{
    public Transform Head;
    public Vector3 HeadOffset;
    
    public Transform HandLeft;
    public Vector3 HandLeftOffset;
    
    public Transform HandRight;
    public Vector3 HandRightOffset;
    
    // ... other locator points
}
```

## Performance Considerations

### Pooling Strategy
- Use `HybridSpawnPrefabFromPool` for frequently spawned objects
- Pool objects are automatically managed by `GameObjectPoolSystem`
- Objects are deactivated when returned to pool, not destroyed

### Transform Synchronization
- Transform mapping uses `TransformAccessArray` for efficient batch processing
- Only entities with `setTransform = true` participate in synchronization
- Minimize the number of tracked transforms for better performance

### Addressable Loading
- Assets are loaded asynchronously to prevent frame drops
- Use `LoadAddressablesSystem` for centralized asset management
- Bulk unloading available through `UnloadAddressables()`

## Best Practices

1. **Use Pooling**: Enable pooling for frequently spawned objects like bullets, effects, or UI elements
2. **Locator Setup**: Define clear locator points for attachment-based spawning
3. **Asset Management**: Preload critical Addressable assets to avoid runtime delays
4. **Transform Sync**: Only enable transform synchronization when necessary
5. **Lifecycle Management**: Use `DestroyWithEntity` for objects that should be cleaned up with their Entity

## Dependencies

- Unity.Entities (1.0.16+)
- Unity.Addressables (1.21.20+)
- NZCore (core utilities and extensions)

## System Update Order

1. `LoadAddressablesSystem` (NZCoreInitializationSystemGroup)
2. `HybridInstantiationSystem` (NZCoreInitializationSystemGroup)
3. `TransformEntityMappingSystem` (TransformSystemGroup)
4. `GameObjectPoolSystem` (PresentationSystemGroup)
5. `HybridDestroySystem` (NZDestroySystemGroup)

## Common Issues and Solutions

### GameObjects Not Spawning
- Verify that the prefab or Addressable asset is properly assigned
- Check that the `WeakObjectReference` is valid
- Ensure the `LoadAddressablesSystem` has loaded the asset

### Transform Synchronization Issues
- Confirm `setTransform` is enabled on the spawn component
- Verify the Entity has a valid `LocalToWorld` component
- Check that the `TransformEntityMappingSystem` is running

### Memory Leaks
- Use pooling for frequently spawned objects
- Ensure `DestroyWithEntity` is set appropriately
- Call `GameObjectPoolSystem.Unload()` when changing scenes

### Animation Not Working
- Verify the prefab has an `Animator` component
- Check that the `HybridAnimator` component is properly initialized
- Ensure the animation clips are valid and accessible