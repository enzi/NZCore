## ScriptableObjects to Blob/Entity Baker

### Usage

Define the blob root data struct (Runtime asmdef)

```csharp
public struct UpgradeBlobAsset
{
    public FixedString128Bytes upgradeIcon;
    public FixedString128Bytes upgradeName;
    public FixedString128Bytes upgradeDescription;
    ...
}
```

Define your BlobAssetReference (Runtime asmdef)

```csharp
public struct UpgradeBlobReference : IBlobAssetReference<UpgradeBlobAsset>
{
    public BlobAssetReference<UpgradeBlobAsset> blob { get; set; }
    public Guid guid { get; set; }
}
```

Define the Scriptable Object (Authoring asmdef)

```csharp
[CreateAssetMenu(fileName = "Upgrade Item", menuName = "Create Upgrade", order = 1)]
public class SO_Upgrade : ScriptableObjectWithGuid, IConvertToBlob<UpgradeBlobAsset>
{
    public string upgradeName;
    public string upgradeDescription;
    public Sprite upgradeIcon;

    public void ToBlobData(IBaker baker, ref BlobBuilder blobBuilder, ref UpgradeBlobAsset blob, Entity blobReferenceEntity)
    {
        blob.upgradeName = upgradeName;
        blob.upgradeDescription = upgradeDescription;
        blob.upgradeIcon = upgradeIcon.name;
        
        // optional, add additonal entity data
        baker.AddComponent(blobReferenceEntity, new YourComp()
        {
        });

        var buffer = baker.AddBuffer<YourBuffer>(blobReferenceEntity);
    }
}
```

Implement the converters (Authoring asmdef)

```csharp
public class UpgradeConverter : ScriptableObjectConverterBase<SO_Upgrade> { }
public class UpgradeConversionSystem : ScriptableObjectConverter_Baker<UpgradeConverter, SO_Upgrade, UpgradeBlobAsset, UpgradeBlobReference> { }
```

Finally, create a gameobject in a subscene and add your converter (UpgradeConverter)
Enable "Auto Load" option so ScriptableObjects from project are automatically added.
