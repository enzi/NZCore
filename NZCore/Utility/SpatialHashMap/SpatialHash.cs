using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

[assembly: InternalsVisibleTo("NZCore.Tests")]

namespace NZCore
{
    public struct SpatialHashMember : IComponentData, IEnableableComponent
    {
        public float Radius;
        public byte CategoryMask;
    }

    public struct SpatialHashSettings : IComponentData
    {
        public float CellSize;
        public float MaxIndexedRadius;
        public int InitialCapacity;
        public int MaxRaySteps;

        public static SpatialHashSettings Default => new()
        {
            CellSize = 4f,
            MaxIndexedRadius = 2f,
            InitialCapacity = 1024,
            MaxRaySteps = 16_384
        };

        internal readonly bool IsValid =>
            math.isfinite(CellSize) && CellSize > 0f &&
            math.isfinite(MaxIndexedRadius) && MaxIndexedRadius >= 0f &&
            InitialCapacity >= 0 && MaxRaySteps > 0 &&
            SpatialHashUtility.TryGetNeighborReach(this, out _);
    }

    public struct SpatialHashRayInput
    {
        public float3 Start;
        public float3 End;
        public byte QueryMask;
        public Entity IgnoreEntity;
    }

    public struct SpatialHashHit
    {
        public Entity Entity;
        public float Fraction;
        public float Distance;
        public float3 Position;
    }

    public enum SpatialHashQueryStatus : byte
    {
        Success,
        InvalidInput,
        InvalidConfiguration,
        TraversalLimitExceeded,
        InsufficientOutputCapacity
    }

    internal struct SpatialHashEntry
    {
        public Entity Entity;
        public float3 Position;
        public float Radius;
        public byte CategoryMask;
    }

    public unsafe struct SpatialHashLookup : IComponentData
    {
        internal NativeParallelMultiHashMap<int2, SpatialHashEntry> Entries;
        internal NativeList<SpatialHashEntry> OversizedEntries;
        internal SpatialHashSettings Settings;
        internal byte Valid;

        public readonly bool IsValid => Valid != 0;
        public readonly int OversizedCount => OversizedEntries.IsCreated ? OversizedEntries.Length : 0;

        internal static SpatialHashLookup Create(in SpatialHashSettings settings, Allocator allocator)
        {
            var capacity = math.max(0, settings.InitialCapacity);
            return new SpatialHashLookup
            {
                Entries = new NativeParallelMultiHashMap<int2, SpatialHashEntry>(capacity, allocator),
                OversizedEntries = new NativeList<SpatialHashEntry>(capacity, allocator),
                Settings = settings,
                Valid = (byte)(settings.IsValid ? 1 : 0)
            };
        }

        internal void Dispose()
        {
            if (Entries.IsCreated)
                Entries.Dispose();
            if (OversizedEntries.IsCreated)
                OversizedEntries.Dispose();
        }

        public readonly SpatialHashQueryStatus RaycastClosest(
            in SpatialHashRayInput input,
            out SpatialHashHit hit)
        {
            hit = default;
            var collector = new HitCollector();
            var status = Traverse(input, ref collector);
            if (status == SpatialHashQueryStatus.Success && collector.HasClosest != 0)
                hit = collector.Closest;
            return status;
        }

        public readonly SpatialHashQueryStatus RaycastAll(
            in SpatialHashRayInput input,
            NativeArray<SpatialHashHit> output,
            out int hitCount)
        {
            hitCount = 0;
            var collector = new HitCollector
            {
                CollectAll = 1,
                Output = output
            };

            var status = Traverse(input, ref collector);
            if (status != SpatialHashQueryStatus.Success)
                return status;

            hitCount = collector.HitCount;
            if (collector.Overflowed != 0)
                return SpatialHashQueryStatus.InsufficientOutputCapacity;

            if (hitCount > 1)
            {
                NativeSortExtension.Sort(
                    (SpatialHashHit*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(output),
                    hitCount,
                    new HitComparer());
            }

            return SpatialHashQueryStatus.Success;
        }

        private readonly SpatialHashQueryStatus Traverse(
            in SpatialHashRayInput input,
            ref HitCollector collector)
        {
            if (Valid == 0 || !Settings.IsValid || !Entries.IsCreated || !OversizedEntries.IsCreated)
                return SpatialHashQueryStatus.InvalidConfiguration;

            if (!math.all(math.isfinite(input.Start)) || !math.all(math.isfinite(input.End)))
                return SpatialHashQueryStatus.InvalidInput;

            var segmentLength = math.length((double3)input.End - input.Start);
            if (!math.isfinite(segmentLength) || segmentLength > float.MaxValue)
                return SpatialHashQueryStatus.InvalidInput;
            collector.SegmentLength = (float)segmentLength;

            if (!SpatialHashUtility.TryGetCell(input.Start, Settings.CellSize, out var startCell) ||
                !SpatialHashUtility.TryGetCell(input.End, Settings.CellSize, out var endCell) ||
                !SpatialHashUtility.TryGetNeighborReach(Settings, out var reach))
                return SpatialHashQueryStatus.InvalidInput;

            VisitInitialNeighborhood(startCell, reach, input, ref collector);

            if (!startCell.Equals(endCell))
            {
                var delta = (double3)input.End - input.Start;
                var stepX = delta.x > 0d ? 1 : delta.x < 0d ? -1 : 0;
                var stepZ = delta.z > 0d ? 1 : delta.z < 0d ? -1 : 0;
                var tDeltaX = stepX == 0 ? double.PositiveInfinity : Settings.CellSize / math.abs(delta.x);
                var tDeltaZ = stepZ == 0 ? double.PositiveInfinity : Settings.CellSize / math.abs(delta.z);
                var tMaxX = FirstBoundaryFraction(input.Start.x, delta.x, startCell.x, stepX);
                var tMaxZ = FirstBoundaryFraction(input.Start.z, delta.z, startCell.y, stepZ);
                var current = startCell;
                var steps = 0;

                while (!current.Equals(endCell))
                {
                    if (steps++ >= Settings.MaxRaySteps)
                        return SpatialHashQueryStatus.TraversalLimitExceeded;

                    if (current.x == endCell.x)
                        tMaxX = double.PositiveInfinity;
                    if (current.y == endCell.y)
                        tMaxZ = double.PositiveInfinity;

                    if (tMaxX < tMaxZ)
                    {
                        current.x += stepX;
                        VisitNewColumn(current, reach, stepX, input, ref collector);
                        tMaxX += tDeltaX;
                    }
                    else if (tMaxZ < tMaxX)
                    {
                        current.y += stepZ;
                        VisitNewRow(current, reach, stepZ, input, ref collector);
                        tMaxZ += tDeltaZ;
                    }
                    else
                    {
                        current.x += stepX;
                        VisitNewColumn(current, reach, stepX, input, ref collector);
                        current.y += stepZ;
                        VisitNewRow(current, reach, stepZ, input, ref collector);
                        tMaxX += tDeltaX;
                        tMaxZ += tDeltaZ;
                    }
                }
            }

            for (var i = 0; i < OversizedEntries.Length; i++)
                VisitEntry(OversizedEntries[i], input, ref collector);

            return SpatialHashQueryStatus.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly double FirstBoundaryFraction(float start, double delta, int cell, int step)
        {
            if (step == 0)
                return double.PositiveInfinity;

            var boundary = ((double)cell + (step > 0 ? 1d : 0d)) * Settings.CellSize;
            return (boundary - start) / delta;
        }

        private readonly void VisitInitialNeighborhood(
            int2 center,
            int reach,
            in SpatialHashRayInput input,
            ref HitCollector collector)
        {
            var minX = math.max((long)int.MinValue, (long)center.x - reach);
            var maxX = math.min((long)int.MaxValue, (long)center.x + reach);
            var minZ = math.max((long)int.MinValue, (long)center.y - reach);
            var maxZ = math.min((long)int.MaxValue, (long)center.y + reach);

            for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                VisitCell(new int2((int)x, (int)z), input, ref collector);
        }

        private readonly void VisitNewColumn(
            int2 center,
            int reach,
            int step,
            in SpatialHashRayInput input,
            ref HitCollector collector)
        {
            var x = (long)center.x + (long)step * reach;
            if (x < int.MinValue || x > int.MaxValue)
                return;

            var minZ = math.max((long)int.MinValue, (long)center.y - reach);
            var maxZ = math.min((long)int.MaxValue, (long)center.y + reach);
            for (var z = minZ; z <= maxZ; z++)
                VisitCell(new int2((int)x, (int)z), input, ref collector);
        }

        private readonly void VisitNewRow(
            int2 center,
            int reach,
            int step,
            in SpatialHashRayInput input,
            ref HitCollector collector)
        {
            var z = (long)center.y + (long)step * reach;
            if (z < int.MinValue || z > int.MaxValue)
                return;

            var minX = math.max((long)int.MinValue, (long)center.x - reach);
            var maxX = math.min((long)int.MaxValue, (long)center.x + reach);
            for (var x = minX; x <= maxX; x++)
                VisitCell(new int2((int)x, (int)z), input, ref collector);
        }

        private readonly void VisitCell(
            int2 cell,
            in SpatialHashRayInput input,
            ref HitCollector collector)
        {
            if (!Entries.TryGetFirstValue(cell, out var entry, out var iterator))
                return;

            do
            {
                VisitEntry(entry, input, ref collector);
            }
            while (Entries.TryGetNextValue(out entry, ref iterator));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void VisitEntry(
            in SpatialHashEntry entry,
            in SpatialHashRayInput input,
            ref HitCollector collector)
        {
            if ((entry.CategoryMask & input.QueryMask) == 0 || entry.Entity == input.IgnoreEntity)
                return;

            if (SpatialHashUtility.IntersectSegmentSphere(input.Start, input.End, entry.Position, entry.Radius, out var fraction))
                collector.Add(entry.Entity, input.Start, input.End, fraction);
        }

        private struct HitCollector
        {
            public NativeArray<SpatialHashHit> Output;
            public SpatialHashHit Closest;
            public float SegmentLength;
            public int HitCount;
            public byte CollectAll;
            public byte HasClosest;
            public byte Overflowed;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(Entity entity, float3 start, float3 end, float fraction)
            {
                var hit = new SpatialHashHit
                {
                    Entity = entity,
                    Fraction = fraction,
                    Distance = SegmentLength * fraction,
                    Position = (float3)math.lerp((double3)start, (double3)end, fraction)
                };

                if (CollectAll == 0)
                {
                    if (HasClosest == 0 || HitComparer.CompareHits(hit, Closest) < 0)
                    {
                        Closest = hit;
                        HasClosest = 1;
                    }
                    return;
                }

                if (HitCount < Output.Length)
                    Output[HitCount] = hit;
                else
                    Overflowed = 1;

                if (HitCount < int.MaxValue)
                    HitCount++;
            }
        }

        private struct HitComparer : IComparer<SpatialHashHit>
        {
            public int Compare(SpatialHashHit x, SpatialHashHit y) => CompareHits(x, y);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int CompareHits(in SpatialHashHit x, in SpatialHashHit y)
            {
                if (x.Fraction < y.Fraction)
                    return -1;
                if (x.Fraction > y.Fraction)
                    return 1;
                if (x.Entity.Index != y.Entity.Index)
                    return x.Entity.Index < y.Entity.Index ? -1 : 1;
                if (x.Entity.Version == y.Entity.Version)
                    return 0;
                return x.Entity.Version < y.Entity.Version ? -1 : 1;
            }
        }
    }

    internal static class SpatialHashUtility
    {
        public static bool TryGetNeighborReach(in SpatialHashSettings settings, out int reach)
        {
            reach = 0;
            if (!math.isfinite(settings.CellSize) || settings.CellSize <= 0f ||
                !math.isfinite(settings.MaxIndexedRadius) || settings.MaxIndexedRadius < 0f)
                return false;

            var value = math.floor((double)settings.MaxIndexedRadius / settings.CellSize) + 1d;
            if (value < 1d || value > int.MaxValue)
                return false;

            reach = (int)value;
            return true;
        }

        public static bool TryGetCell(float3 position, float cellSize, out int2 cell)
        {
            cell = default;
            if (!math.all(math.isfinite(position)) || !math.isfinite(cellSize) || cellSize <= 0f)
                return false;

            var x = math.floor((double)position.x / cellSize);
            var z = math.floor((double)position.z / cellSize);
            if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue)
                return false;

            cell = new int2((int)x, (int)z);
            return true;
        }

        public static bool IntersectSegmentSphere(
            float3 start,
            float3 end,
            float3 center,
            float radius,
            out float fraction)
        {
            fraction = 0f;
            var offset = (double3)start - center;
            var radiusSquared = (double)radius * radius;
            if (math.lengthsq(offset) <= radiusSquared)
                return true;

            var direction = (double3)end - start;
            var a = math.lengthsq(direction);
            if (a == 0d)
                return false;

            var halfB = math.dot(offset, direction);
            var c = math.lengthsq(offset) - radiusSquared;
            var discriminant = halfB * halfB - a * c;
            if (discriminant < 0d)
                return false;

            var t = (-halfB - math.sqrt(discriminant)) / a;
            if (t < 0d || t > 1d)
                return false;

            fraction = (float)math.clamp(t, 0d, 1d);
            return true;
        }
    }
}
