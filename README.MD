# NZCore
Helpful extensions for Entities, NativeContainer extensions and custom NativeContainers that I've used over the years in my codebase.
Credits for DynamicHashMap and batch add methods for NativeList/MultiHashMap go to [tertle](https://forum.unity.com/members/tertle.33474/).
There's also a new ResizeBufferCapacity by him but it's still very experimental and has a bug with subscenes data on new imports of a project.

Note about Entities 1.0:
Unity has listened to our requirements and Entities 1.0 has changed ComponentDataFromEntity to ComponentLookup and added methods to to get RefRO/RW structs of an IComponentData.
This makes UnsafeCDFE not as important as it once was. However, there's still a bit of value found because you
can directly get a ref value and not the struct wrapper and getting the pointers is easier, without relying on calling
UnsafeUtility.AddressOf on a RefRW/RO. 

Unity.Entities.Exposed namespace:

The main purpose is to get pointers and references from ComponentDataFromEntity

```
using Unity.Entities.Exposed;
```

Inside System - change:
```
  var Health_WriteLookup = GetComponentDataFromEntity<Health>(false); // old version
  var Health_WriteLookup = SystemAPI.GetComponentLookup<Health>(false); // new version
```
to:
```
  var Health_WriteLookup = EntityManager.GetUnsafeCDFE<Health>(false); // old version
  // the new version supports implicit conversion!
  // all you need to do now is change from ComponentLookup to UnsafeComponentLookup. It's that easy! :)
  // your job struct would declare: UnsafeComponentLookup<Health> Health_WriteLookup
  var job = new JobStruct
  {
    Health_WriteLookup = SystemAPI.GetComponentLookup<Health>(false);
  }.Schedule();
```
  
Now you can query health and get a ref:
```
ref var healthComp = ref Health_WriteLookup.GetRef(lookupEntity);
healthComp.health += 100;
```

NZNativeContainers

While optimizing I've found the need for special data containers to speed things up even further.
Often allocations, write performance or thread stalls starts being a bottleneck and this is where those custom containers help out.
These are:
- ParallelList
- ParallelListHashMap
- KeyValue
- ValueOnly
- UnsafeQueue

ParallelList:
A NativeList has the downside of thread stalls when writing in parallel. ParallelList is designed for maximum write performance without any requirement of Interlocked or atomic operations.

The usage is simple:
```
var parallelList = new ParallelList<TestStruct>(initialSize, Allocator.Persistent);
```
In OnUpdate
```
// Very similar to NativeStreams ForEachCount, however this uses, in most cases the chunk count, or in other words, the amount of times, 
// a parallelList is opened for writing. 
// As shown in the job below. It doesn't have to necessarily be the chunk count. For 
// This has nothing to do with the amount of threads or internal lists.
parallelList.SetChunkCount(count); 
```

```
int count = 250000;
int writeCount = 32;
var writeJobHandle = new TestJob_ParallelListWrite()
{
	writeCount = writeCount,
	paralleListWriter = parallelList.AsWriter()
}.ScheduleParallel(count, 64, default);
```

A simple parallel write job
```
[BurstCompile]
public struct TestJob_ParallelListWrite : IJobFor
{
	public int writeCount;
	public ParallelList<TestStruct>.Writer paralleListWriter;
	
	public void Execute(int index)
	{
		paralleListWriter.BeginForEachChunk(index);

		for (int i = 0; i < writeCount;i++)
		{
			paralleListWriter.Write(new TestStruct()
			{
				data1 = 1,
				data2 = 2,
				data3 = 3.0f
			});
		}
		
		paralleListWriter.EndForEachChunk();
	}
}
```

ParallelListHashMap:
To index a ParallelList a ParallelListHashMap can be built.
The inputs are a ParallelList key and value array.

KeyValue:
NativeHashMap has the downside of requiring a copy process for the key and value array.
This NativeContainer skips the copying and enables to index an existing key and value array.

ValueOnly:
The same principle as KeyValue with the exception that there's no key array because the key is *inside* the value array.
For that to work, the type and offset to the key has to be set.

Unity.Entities.Exposed namespace:
Helpful access to internal data of Entities.
This includes Blobs, Chunks, DynamicBuffers, EntityManager, FixedList, StorageInfo and World.
The MVP of this namespace is 
```
public static void SetChangeVersion<T>(this ArchetypeChunk chunk, ref ComponentTypeHandle<T> handle)
```
As you may know, acquiring the component array from a write handle bumps the version of the ComponentType, regardless if you write
to the array or not. This is problematic when implementing change filtering. When the version always gets bumped, a change is triggered everytime
making change filters totally useless.
To work around this, the trick is to acquire RO access from a write handle. No version is incremented then. After the conditional writes are done,
a call to SetChangeVersion with the handle bumps the version. That way any jobs that implement change filtering are correctly triggered.

NZNativeContainers.Extensions namespace:

A bunch of extensions for NativeList and NativeMultiHashMap.
Core parts were written by [tertle](https://forum.unity.com/members/tertle.33474/)

(GetExtendedList has been supserseded by ParallelList)
NativeList.ParallelWriter has an extension method GetExtendedList(int increaseCount = 10) to get a NativeListExtended version from it.
A NativeListExtended doesn't allocate memory on every Add, instead it reserves blocks of elements, defined by the increaseCount.
With this, several threads can write to a NativeList without getting stalled by Interlocked increasing the length.

Best usage is when the final length is known. If not, another helper extension, FillEmpty can be used to fill in those elements at the end of a Job.

NativeMultiHashMap can be allocated in advance and with a call to GetKeyAndValueLists(out keys, out values), 2 NativeLists can be created that 
can be used to directly write to the keys and values.
Paired with the NativeListExtended this is a very fast way to produce NativeMultiHashMap data.
At the end of writing, CalculateBuckets needs to be called.

Also provided is an extension method, GetRefValuesForKey which will return an enumerator that is able to get ref values from the NativeMultiHashMap.