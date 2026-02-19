# Asset Management

Tools for converting ScriptableObject assets into ECS-compatible data at bake time.

---

## BlobDatabase

Converts ScriptableObjects into ECS blob assets. At bake time, every asset of the registered type is found in the project, serialized into an unmanaged blob struct, and added to the world as an entity with a blob reference component. This avoids managed objects at runtime and gives you safe, burst-compatible access to asset data.

### Scene Setup

1. Create a `ScriptableObjectDatabaseVersion` asset via `NZCore/BlobDatabaseVersion`.
2. In a subscene, add a GameObject with `ScriptableObjectDatabaseAuthoring` and assign the version asset.

`ScriptableObjectDatabaseAuthoring` auto-discovers all types implementing `IConvertToBlob` via reflection and bakes them. The version asset is used as a dependency so the baker re-runs when assets change. `BlobDatabaseAssetPostprocessor` bumps the version automatically whenever a relevant `.asset` is created, deleted, or modified.

### Step 1 — Define the blob struct (Runtime asmdef)

```csharp
public struct UpgradeBlobAsset
{
    public FixedString128Bytes Name;
    public FixedString128Bytes Description;
    public UnityObjectRefForBlob<Sprite> Icon;
    public int Level;
}
```

### Step 2 — Define the blob reference component (Runtime asmdef)

```csharp
public struct UpgradeBlobReference : IBlobAssetReference<UpgradeBlobAsset>
{
    public BlobAssetReference<UpgradeBlobAsset> blob { get; set; }
    public Guid guid { get; set; }
}
```

`IBlobAssetReference<T>` requires `blob` and `guid`. The guid maps back to the source asset.

For blobs that are looked up by ID (AutoID), also implement `IBlobIndex`:

```csharp
public struct UpgradeBlobReference : IBlobIndex, IBlobAssetReference<UpgradeBlobAsset>
{
    public BlobAssetReference<UpgradeBlobAsset> blob { get; set; }
    public Guid guid { get; set; }
    public int BlobIndex => blob.IsCreated ? blob.Value.UpgradeId.Value : 0;
}
```

### Step 3 — Implement `IConvertToBlob` on the ScriptableObject (Authoring asmdef)

```csharp
[CreateAssetMenu(menuName = "Game/Upgrade")]
public class SO_Upgrade : ScriptableObjectWithAutoID,
    IConvertToBlob<SO_Upgrade, UpgradeBlobReference, UpgradeBlobAsset>
{
    [SerializeField] private UpgradeTypeValue _upgradeId;
    public override int AutoID { get => _upgradeId.Value; set => _upgradeId.Value = value; }

    public string upgradeName;
    public string upgradeDescription;
    public Sprite upgradeIcon;

    public void ToBlobData(GenericBlobBaker.ContextBase context, ref BlobBuilder blobBuilder, ref UpgradeBlobAsset blob)
    {
        blob.UpgradeId = _upgradeId;
        blob.Name = upgradeName;
        blob.Description = upgradeDescription;

        // For Unity object references (Sprite, Texture, etc.) use AddObjectRef
        // so the weak reference is patched correctly at runtime
        context.AddObjectRef(upgradeIcon, ref blob.Icon);

        // Optionally add extra components to the blob entity
        context.Baker.AddComponent(context.BlobEntity, new SomeExtraComponent());
    }
}
```

The `IConvertToBlob<TSoClass, TBlobReference, TBlobStruct>` interface provides a default `Bake` implementation that calls `GenericBlobBaker.Bake<...>`, so no baker boilerplate is needed.

### Two-blob variant

When a single asset needs two separate blobs, use `IBlobAssetReference<T1, T2>` and `IConvertToBlob<TSoClass, TBlobReference, T1, T2>`:

```csharp
public void ToBlobData(GenericBlobBaker.ContextBase context, ref BlobBuilder bb, ref BlobA blobA, ref BlobB blobB)
{
    // fill both blobs; use blobAssetReferenceIndex 0 or 1 for AddObjectRef/AddEntityRef
}
```

### Unity Object References in blobs

Use `UnityObjectRefForBlob<T>` as the field type in the blob struct and call `context.AddObjectRef` during `ToBlobData`. This registers a patch so the weak reference is resolved at runtime without managed memory.

```csharp
context.AddObjectRef(mySprite, ref blob.Icon);                    // single blob
context.AddObjectRef(mySprite, ref blob.Icon, blobAssetReferenceIndex: 1); // second blob
```

---

## Settings

Settings are ScriptableObjects that get baked into ECS entities as components. They are meant for global configuration data (tuning values, lookup tables, etc.) that needs to be accessible in burst jobs.

### Defining a settings asset

Inherit from `ScriptableObjectDatabase<T>` and implement `Bake`:

```csharp
[CreateAssetMenu(menuName = "Game/UpgradeSettings")]
public class UpgradeSettings : ScriptableObjectDatabase<UpgradeSettings>
{
    public int MaxUpgradeLevel;
    public float BaseMultiplier;

    public override void Bake(IBaker baker, Entity entity)
    {
        baker.AddComponent(entity, new UpgradeSettingsComponent
        {
            MaxUpgradeLevel = MaxUpgradeLevel,
            BaseMultiplier = BaseMultiplier
        });
    }
}
```

`ScriptableObjectDatabase<T>` implements both `ISettingsBaker` and `ISettingsDatabase`. `ScriptableObjectDatabaseCollector` auto-discovers all `ISettingsDatabase` implementations and `ScriptableObjectDatabaseAuthoring` bakes them — no manual registration needed.

**One asset per type** must exist in the project. `SettingsUtility.GetSettings<T>()` is used internally to load it.

---

## AutoID

AutoID assigns a stable, unique `int` ID to each ScriptableObject asset of a type. IDs are assigned automatically on import and are guaranteed to be unique within the type. ID `0` is reserved for "None" states.

### Using AutoID

Inherit from `ScriptableObjectWithAutoID` and expose the ID via a typed value wrapper:

```csharp
[Serializable]
public struct UpgradeTypeValue : IGenericValue<int>, IEquatable<UpgradeTypeValue>
{
    [SerializeField] private int _value;
    public int Value { get => _value; set => _value = value; }
    public bool Equals(int other) => _value == other;
    public bool Equals(UpgradeTypeValue other) => _value == other._value;
    public override int GetHashCode() => _value.GetHashCode();
}

[CreateAssetMenu(menuName = "Game/Upgrade")]
public class SO_Upgrade : ScriptableObjectWithAutoID
{
    [SerializeField] private UpgradeTypeValue _upgradeId;
    public override int AutoID { get => _upgradeId.Value; set => _upgradeId.Value = value; }
}
```

`AutoIDAssetPostProcessor` runs on every asset import. If the ID is `0` (new asset) or a duplicate, it scans all assets of that type, finds the lowest unused ID starting at `1`, and assigns it. The asset is then saved automatically.

### Auto-registration in a database

Apply `[RegisterInScriptableObjectDatabase]` to automatically keep a manager ScriptableObject's list in sync:

```csharp
[RegisterInScriptableObjectDatabase("UpgradeDatabase", "Upgrades")]
public class SO_Upgrade : ScriptableObjectWithAutoID { ... }
```

`AutoIDAssetPostProcessor` calls `ScriptableObjectDatabase.Update(type)` after processing. The manager asset named `UpgradeDatabase` must have a serialized array field called `Upgrades`. The list is rebuilt to match all assets of the type currently in the project.

Deletions are handled separately via `AutoIDAssetEvents.OnWillDeleteAsset`, which removes the asset from the list before it is destroyed.

### DefaultAutoID

For types where one asset should act as the default (e.g. a fallback "Hit" result), inherit from `ScriptableObjectWithDefaultAutoID`:

```csharp
public class SO_AttackResult : ScriptableObjectWithDefaultAutoID
{
    public override Type DefaultType => typeof(AttackResultTypeValue);
    public override int AutoID { get => _id.Value; set => _id.Value = value; }
}
```

Mark exactly one asset's `DefaultValue` checkbox as true. `DefaultAutoIDProcessor` generates a JSON file with the default ID for use in code generation. The class also needs `[PackagePath]` to specify where the JSON is written.