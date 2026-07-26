using System;
using System.Collections.Generic;
using NZCore;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class SpatialHashTests
{
    [Test]
    public void Settings_DefaultsMatchContract()
    {
        var settings = SpatialHashSettings.Default;
        Assert.That(settings.CellSize, Is.EqualTo(4f));
        Assert.That(settings.MaxIndexedRadius, Is.EqualTo(2f));
        Assert.That(settings.InitialCapacity, Is.EqualTo(1024));
        Assert.That(settings.MaxRaySteps, Is.EqualTo(16_384));
    }

    [Test]
    public void Build_FiltersMembersAndRebuildsFromCurrentWorldState()
    {
        using var world = new World("Spatial hash build test");
        var manager = world.EntityManager;
        var system = world.GetOrCreateSystem<SpatialHashBuildSystem>();
        var settings = SpatialHashSettings.Default;
        settings.InitialCapacity = 1;
        manager.SetComponentData(system, settings);

        var normal = CreateMember(manager, new float3(-0.25f, 0f, 0f), 1f, 1);
        var disabled = CreateMember(manager, new float3(1f, 0f, 0f), 1f, 2);
        manager.SetComponentEnabled<SpatialHashMember>(disabled, false);
        CreateMember(manager, new float3(2f, 0f, 0f), -1f, 1);
        CreateMember(manager, new float3(3f, 0f, 0f), 1f, 0);
        CreateMember(manager, new float3(4f, 0f, 0f), float.NaN, 1);
        var zeroRadius = CreateMember(manager, new float3(4f, 0f, 0f), 0f, 1);
        CreateMember(manager, new float3(8f, 0f, 0f), 3f, 1);

        UpdateAndComplete(system, world);
        var lookup = manager.GetComponentData<SpatialHashLookup>(system);
        Assert.That(lookup.Entries.Count(), Is.EqualTo(2));
        Assert.That(lookup.OversizedCount, Is.EqualTo(1));
        Assert.That(lookup.Entries.ContainsKey(new int2(-1, 0)), Is.True);
        Assert.That(lookup.Entries.Capacity, Is.GreaterThanOrEqualTo(6));

        manager.SetComponentData(normal, new LocalToWorld { Value = float4x4.Translate(new float3(12f, 0f, 0f)) });
        manager.SetComponentEnabled<SpatialHashMember>(disabled, true);
        manager.DestroyEntity(zeroRadius);

        UpdateAndComplete(system, world);
        lookup = manager.GetComponentData<SpatialHashLookup>(system);
        Assert.That(lookup.Entries.Count(), Is.EqualTo(2));
        Assert.That(lookup.Entries.ContainsKey(new int2(-1, 0)), Is.False);
        Assert.That(lookup.Entries.ContainsKey(new int2(3, 0)), Is.True);
        Assert.That(lookup.OversizedCount, Is.EqualTo(1));

        settings.CellSize = 0f;
        manager.SetComponentData(system, settings);
        UpdateAndComplete(system, world);
        lookup = manager.GetComponentData<SpatialHashLookup>(system);
        Assert.That(lookup.IsValid, Is.False);
        Assert.That(lookup.Entries.Count(), Is.Zero);
        Assert.That(lookup.OversizedCount, Is.Zero);
    }

    [Test]
    public void Raycast_HandlesClosedSegmentAndDegenerateCases()
    {
        using var lookup = TestLookup.Create(
            new TestSphere(1, new float3(0f, 1f, 0f), 1f, 1),
            new TestSphere(2, new float3(2f, 0f, 0f), 0f, 8),
            new TestSphere(3, new float3(0f, 0f, 0f), 1f, 2),
            new TestSphere(4, new float3(0f, 5f, 0f), 0.25f, 4));

        AssertHit(lookup.Value, new float3(-2f, 0f, 0f), new float3(2f, 0f, 0f), 1, 1, 0.5f);
        AssertHit(lookup.Value, new float3(0f), new float3(2f, 0f, 0f), 8, 2, 1f);
        AssertHit(lookup.Value, new float3(0f), new float3(2f, 0f, 0f), 2, 3, 0f);
        AssertHit(lookup.Value, new float3(0f, 4f, 0f), new float3(0f, 6f, 0f), 4, 4, 0.375f);
        AssertHit(lookup.Value, new float3(0f, 5f, 0f), new float3(0f, 5f, 0f), 4, 4, 0f);

        var status = lookup.Value.RaycastClosest(new SpatialHashRayInput
        {
            Start = new float3(3f),
            End = new float3(3f),
            QueryMask = 0xff
        }, out var miss);
        Assert.That(status, Is.EqualTo(SpatialHashQueryStatus.Success));
        Assert.That(miss.Entity, Is.EqualTo(Entity.Null));
    }

    [Test]
    public void Raycast_FiltersMaskAndIgnoredEntity()
    {
        using var lookup = TestLookup.Create(
            new TestSphere(10, new float3(0f), 0.5f, 1),
            new TestSphere(11, new float3(1f, 0f, 0f), 0.5f, 2));

        AssertHit(lookup.Value, new float3(-2f, 0f, 0f), new float3(2f, 0f, 0f), 2, 11, 0.625f);

        var status = lookup.Value.RaycastClosest(new SpatialHashRayInput
        {
            Start = new float3(-2f, 0f, 0f),
            End = new float3(2f, 0f, 0f),
            QueryMask = 1,
            IgnoreEntity = new Entity { Index = 10, Version = 1 }
        }, out var hit);
        Assert.That(status, Is.EqualTo(SpatialHashQueryStatus.Success));
        Assert.That(hit.Entity, Is.EqualTo(Entity.Null));
    }

    [Test]
    public void Raycast_TraversesNegativeGridCornersInBothDirections()
    {
        var settings = SpatialHashSettings.Default;
        settings.CellSize = 1f;
        settings.MaxIndexedRadius = 0.5f;
        using var lookup = TestLookup.Create(
            settings,
            new TestSphere(1, new float3(-1.1f, 0f, -1.1f), 0.2f, 1),
            new TestSphere(2, new float3(1f, 0f, 1f), 0f, 1));
        using var hits = new NativeArray<SpatialHashHit>(2, Allocator.Temp);

        var forward = new SpatialHashRayInput
        {
            Start = new float3(-2f, 0f, -2f),
            End = new float3(2f, 0f, 2f),
            QueryMask = 1
        };
        Assert.That(lookup.Value.RaycastAll(forward, hits, out var count), Is.EqualTo(SpatialHashQueryStatus.Success));
        Assert.That(count, Is.EqualTo(2));
        Assert.That(hits[0].Entity.Index, Is.EqualTo(1));
        Assert.That(hits[1].Entity.Index, Is.EqualTo(2));

        var reverse = forward;
        reverse.Start = forward.End;
        reverse.End = forward.Start;
        Assert.That(lookup.Value.RaycastAll(reverse, hits, out count), Is.EqualTo(SpatialHashQueryStatus.Success));
        Assert.That(count, Is.EqualTo(2));
        Assert.That(hits[0].Entity.Index, Is.EqualTo(2));
        Assert.That(hits[1].Entity.Index, Is.EqualTo(1));
    }

    [Test]
    public void Lookup_IsCallableFromBurstJob()
    {
        using var lookup = TestLookup.Create(new TestSphere(17, new float3(0f), 1f, 1));
        using var result = new NativeReference<SpatialHashHit>(Allocator.TempJob);
        new BurstRaycastJob
        {
            Lookup = lookup.Value,
            Input = new SpatialHashRayInput
            {
                Start = new float3(-2f, 0f, 0f),
                End = new float3(2f, 0f, 0f),
                QueryMask = 1
            },
            Result = result
        }.Schedule().Complete();
        Assert.That(result.Value.Entity.Index, Is.EqualTo(17));
    }

    [Test]
    public void RaycastAll_SortsDeterministicallyAndReportsRequiredCapacity()
    {
        using var lookup = TestLookup.Create(
            new TestSphere(5, new float3(0f), 0.5f, 1),
            new TestSphere(2, new float3(0f), 0.5f, 1),
            new TestSphere(9, new float3(-1f, 0f, 0f), 0.25f, 1));
        var input = new SpatialHashRayInput
        {
            Start = new float3(-2f, 0f, 0f),
            End = new float3(2f, 0f, 0f),
            QueryMask = 1
        };

        using (var tooSmall = new NativeArray<SpatialHashHit>(2, Allocator.Temp))
        {
            var status = lookup.Value.RaycastAll(input, tooSmall, out var required);
            Assert.That(status, Is.EqualTo(SpatialHashQueryStatus.InsufficientOutputCapacity));
            Assert.That(required, Is.EqualTo(3));
        }

        using var hits = new NativeArray<SpatialHashHit>(3, Allocator.Temp);
        Assert.That(lookup.Value.RaycastAll(input, hits, out var count), Is.EqualTo(SpatialHashQueryStatus.Success));
        Assert.That(count, Is.EqualTo(3));
        Assert.That(hits[0].Entity.Index, Is.EqualTo(9));
        Assert.That(hits[1].Entity.Index, Is.EqualTo(2));
        Assert.That(hits[2].Entity.Index, Is.EqualTo(5));
    }

    [Test]
    public void Raycast_ReturnsExplicitInputConfigurationAndTraversalErrors()
    {
        var settings = SpatialHashSettings.Default;
        settings.CellSize = 1f;
        settings.MaxRaySteps = 1;
        using var lookup = TestLookup.Create(settings, new TestSphere(1, new float3(3f, 0f, 0f), 1f, 1));

        var status = lookup.Value.RaycastClosest(new SpatialHashRayInput
        {
            Start = new float3(0f),
            End = new float3(4f, 0f, 0f),
            QueryMask = 1
        }, out _);
        Assert.That(status, Is.EqualTo(SpatialHashQueryStatus.TraversalLimitExceeded));

        status = lookup.Value.RaycastClosest(new SpatialHashRayInput
        {
            Start = new float3(float.NaN),
            End = new float3(0f),
            QueryMask = 1
        }, out _);
        Assert.That(status, Is.EqualTo(SpatialHashQueryStatus.InvalidInput));

        var invalid = lookup.Value;
        invalid.Valid = 0;
        status = invalid.RaycastClosest(default, out _);
        Assert.That(status, Is.EqualTo(SpatialHashQueryStatus.InvalidConfiguration));
    }

    [Test]
    public void SeededRaycasts_MatchBruteForceForForwardReverseAndVerticalSegments()
    {
        const int sphereCount = 200;
        const int rayCount = 250;
        var random = new System.Random(0x5eed);
        var spheres = new TestSphere[sphereCount];
        for (var i = 0; i < spheres.Length; i++)
        {
            spheres[i] = new TestSphere(
                i + 1,
                RandomPoint(random, 20f),
                (float)random.NextDouble() * 3f,
                (byte)(1 << random.Next(0, 3)));
        }

        using var lookup = TestLookup.Create(spheres);
        using var actual = new NativeArray<SpatialHashHit>(sphereCount, Allocator.Temp);
        var expected = new List<SpatialHashHit>(sphereCount);

        for (var rayIndex = 0; rayIndex < rayCount; rayIndex++)
        {
            var start = RandomPoint(random, 25f);
            var end = rayIndex % 10 == 0
                ? new float3(start.x, (float)(random.NextDouble() * 50d - 25d), start.z)
                : RandomPoint(random, 25f);
            if ((rayIndex & 1) != 0)
                (start, end) = (end, start);
            var mask = (byte)random.Next(1, 8);
            var input = new SpatialHashRayInput { Start = start, End = end, QueryMask = mask };

            Assert.That(lookup.Value.RaycastAll(input, actual, out var actualCount), Is.EqualTo(SpatialHashQueryStatus.Success));
            BruteForce(spheres, input, expected);
            Assert.That(actualCount, Is.EqualTo(expected.Count), $"ray {rayIndex}");
            for (var i = 0; i < actualCount; i++)
            {
                Assert.That(actual[i].Entity, Is.EqualTo(expected[i].Entity), $"ray {rayIndex}, hit {i}");
                Assert.That(actual[i].Fraction, Is.EqualTo(expected[i].Fraction).Within(0.00001f), $"ray {rayIndex}, hit {i}");
            }
        }
    }

    private static Entity CreateMember(EntityManager manager, float3 position, float radius, byte mask)
    {
        var entity = manager.CreateEntity(typeof(SpatialHashMember), typeof(LocalToWorld));
        manager.SetComponentData(entity, new SpatialHashMember { Radius = radius, CategoryMask = mask });
        manager.SetComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
        return entity;
    }

    private static void UpdateAndComplete(SystemHandle system, World world)
    {
        system.Update(world.Unmanaged);
        world.EntityManager.CompleteAllTrackedJobs();
    }

    private static void AssertHit(
        in SpatialHashLookup lookup,
        float3 start,
        float3 end,
        byte mask,
        int expectedEntity,
        float expectedFraction)
    {
        var status = lookup.RaycastClosest(new SpatialHashRayInput
        {
            Start = start,
            End = end,
            QueryMask = mask
        }, out var hit);
        Assert.That(status, Is.EqualTo(SpatialHashQueryStatus.Success));
        Assert.That(hit.Entity.Index, Is.EqualTo(expectedEntity));
        Assert.That(hit.Fraction, Is.EqualTo(expectedFraction).Within(0.00001f));
        Assert.That(hit.Distance, Is.EqualTo(math.distance(start, end) * expectedFraction).Within(0.00001f));
        Assert.That(hit.Position, Is.EqualTo(math.lerp(start, end, expectedFraction)).Using(Float3Comparer.Instance));
    }

    private static float3 RandomPoint(System.Random random, float extent) => new(
        (float)(random.NextDouble() * extent * 2d - extent),
        (float)(random.NextDouble() * extent * 2d - extent),
        (float)(random.NextDouble() * extent * 2d - extent));

    private static void BruteForce(
        IReadOnlyList<TestSphere> spheres,
        in SpatialHashRayInput input,
        List<SpatialHashHit> hits)
    {
        hits.Clear();
        foreach (var sphere in spheres)
        {
            if ((sphere.Mask & input.QueryMask) == 0 ||
                !BruteIntersect(input.Start, input.End, sphere.Position, sphere.Radius, out var fraction))
                continue;

            hits.Add(new SpatialHashHit
            {
                Entity = sphere.Entity,
                Fraction = fraction,
                Distance = math.distance(input.Start, input.End) * fraction,
                Position = math.lerp(input.Start, input.End, fraction)
            });
        }

        hits.Sort((a, b) =>
        {
            var fraction = a.Fraction.CompareTo(b.Fraction);
            return fraction != 0 ? fraction : a.Entity.Index.CompareTo(b.Entity.Index);
        });
    }

    private static bool BruteIntersect(float3 start, float3 end, float3 center, float radius, out float fraction)
    {
        fraction = 0f;
        var startOffset = start - center;
        var radiusSquared = radius * radius;
        if (math.lengthsq(startOffset) <= radiusSquared)
            return true;

        var direction = end - start;
        var a = math.lengthsq(direction);
        if (a == 0f)
            return false;

        var projection = math.dot(startOffset, direction);
        var discriminant = projection * projection - a * (math.lengthsq(startOffset) - radiusSquared);
        if (discriminant < 0f)
            return false;

        var value = (-projection - math.sqrt(discriminant)) / a;
        if (value < 0f || value > 1f)
            return false;
        fraction = math.clamp(value, 0f, 1f);
        return true;
    }

    private readonly struct TestSphere
    {
        public readonly Entity Entity;
        public readonly float3 Position;
        public readonly float Radius;
        public readonly byte Mask;

        public TestSphere(int entityIndex, float3 position, float radius, byte mask)
        {
            Entity = new Entity { Index = entityIndex, Version = 1 };
            Position = position;
            Radius = radius;
            Mask = mask;
        }
    }

    private sealed class TestLookup : IDisposable
    {
        public SpatialHashLookup Value;

        private TestLookup(SpatialHashLookup value) => Value = value;

        public static TestLookup Create(params TestSphere[] spheres) => Create(SpatialHashSettings.Default, spheres);

        public static TestLookup Create(SpatialHashSettings settings, params TestSphere[] spheres)
        {
            settings.InitialCapacity = math.max(settings.InitialCapacity, spheres.Length);
            var lookup = SpatialHashLookup.Create(settings, Allocator.Persistent);
            var normalCount = 0;
            foreach (var sphere in spheres)
            {
                if (sphere.Radius <= settings.MaxIndexedRadius)
                    normalCount++;
            }

            var keys = new NativeArray<int2>(normalCount, Allocator.Temp);
            var values = new NativeArray<SpatialHashEntry>(normalCount, Allocator.Temp);
            var normalIndex = 0;
            foreach (var sphere in spheres)
            {
                var entry = new SpatialHashEntry
                {
                    Entity = sphere.Entity,
                    Position = sphere.Position,
                    Radius = sphere.Radius,
                    CategoryMask = sphere.Mask
                };
                if (sphere.Radius > settings.MaxIndexedRadius)
                {
                    lookup.OversizedEntries.Add(entry);
                    continue;
                }

                Assert.That(SpatialHashUtility.TryGetCell(sphere.Position, settings.CellSize, out var cell), Is.True);
                keys[normalIndex] = cell;
                values[normalIndex] = entry;
                normalIndex++;
            }

            if (normalCount > 0)
                lookup.Entries.AddBatchUnsafe(keys, values);
            keys.Dispose();
            values.Dispose();
            return new TestLookup(lookup);
        }

        public void Dispose() => Value.Dispose();
    }

    private sealed class Float3Comparer : IEqualityComparer<float3>
    {
        public static readonly Float3Comparer Instance = new();

        public bool Equals(float3 x, float3 y) => math.all(math.abs(x - y) < 0.00001f);
        public int GetHashCode(float3 obj) => obj.GetHashCode();
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct BurstRaycastJob : IJob
    {
        [ReadOnly] public SpatialHashLookup Lookup;
        public SpatialHashRayInput Input;
        public NativeReference<SpatialHashHit> Result;

        public void Execute()
        {
            if (Lookup.RaycastClosest(Input, out var hit) == SpatialHashQueryStatus.Success)
                Result.Value = hit;
        }
    }
}
