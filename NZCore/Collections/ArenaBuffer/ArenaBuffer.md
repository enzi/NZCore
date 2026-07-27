# ArenaBuffer

A DynamicBuffer replacement that packs the buffers of one element type into shared arena memory instead
of giving every entity its own scattered heap block. Four allocator modes exist; they differ only in how
memory is carved and how a record points at it.

## Shared design

- A source generated `*Ref : IComponentData` per element type holds the record. All of them mirror
  `ArenaBufferRefData`, so untyped code can reinterpret a chunk's array without knowing the type.
- `ArenaBufferReserveSystem` turns unreserved requests into real blocks. Baking cannot touch arena
  memory, so a baked record only carries the capacity it wants.
- There is no cleanup component. Freeing is the destroy pipeline's job via `ArenaBufferReleaseHandles`,
  and a record removed without releasing is reported as a leak.
- Arenas are process global, not per World. Tests must `ArenaBufferRegistry.ResetAll()`.

### The record (16 bytes, `LayoutKind.Explicit`)

| offset | field | |
|---|---|---|
| 0 | `IntPtr Block` | block **address** (Paged) |
| 0 | `int Handle` | packed **handle**, overlaid on the low half (every other mode) |
| 8 | `int Length` | |
| 12 | `int Capacity` | power of two |

`Unreserved` is `-1`. Testing the low half covers both readings: blocks are at least 8 byte aligned, so
no real address can have all-ones low bits.

Both fields are real fields, not a converting property. A property doing `(int)Block` cost the handle
modes **over 2x** on the append benchmark, because their read path resolves per element access.

## Modes

### Paged — `ArenaAllocator` (recommended default)

Fixed 1 MB pages, each carved into equal blocks of one power of two size class. Free blocks are threaded
into an intrusive chain stored inside the blocks themselves.

Pages never move, so the record stores the block **address** directly. Reaching elements costs nothing
beyond the load of a field the caller is already touching — no page table, nothing to decode.

- **Pros** Nothing ever invalidates another buffer's memory. Best write performance of any mode. Cheapest
  possible resolve. Simplest of the arena allocators.
- **Cons** Costs 4 bytes more per record than a handle would. Growth still moves the *grown* buffer into
  a larger size class.

#### Why storing addresses is safe here

A growth invalidates **only the buffer that grew**, never any other. Three separate properties have to
hold for that, and all three are load bearing:

1. **Pages are never moved or freed** while the arena is alive. `CarvePage` takes a fresh allocation per
   page; nothing relocates it. Growing a buffer allocates from a *different* size class's page and leaves
   every other page untouched.
2. **Growing the page table does not move the pages.** `GrowPageTable` reallocates only the `byte**`
   table of pointers and memcpys it. The pages it points at stay exactly where they were.
3. **The record is the single source of truth, and nothing caches it.** `ArenaAllocator.Reallocate` takes
   `ref ArenaBufferRefData` and writes the new address into `refData.Block` as part of the same call that
   moved the block. `ArenaBuffer<T>` re-reads `_ref->Block` on every access — there is deliberately **no
   cached base pointer**, so the next access after a growth sees the new address automatically.

Point 3 is why the cached base was removed when the mode switched from handles to addresses. Caching an
address that the allocator can change is exactly the bug the mode would otherwise invite, and there is
nothing left to cache anyway: the record is already in the cache line the caller is reading.

What *is* still invalidated, and is the caller's problem — identical to the DynamicBuffer rules:

- A raw pointer or `NativeArray` view taken from a buffer and held across an `Add` to **that same
  buffer**. Other buffers are unaffected.
- The `ArenaBuffer<T>` value itself across a structural change, because it holds `_ref`, a pointer into
  chunk memory.
- Everything, on `Reset()` or `Dispose()`, which free all pages. Teardown and tests only.

Contrast with Contiguous, where a growth of *any* buffer relocates the single backing block and so
invalidates *every* resolved address in the arena. That is the difference that makes Paged the safer
default even though Contiguous reads faster.

`PageShift = 20` (1 MB) matters: across three measured page sizes multi-type read throughput moved
monotonically — 16 KB `0.45x`, 64 KB `1.00x`, 1 MB `1.44x`.

### Contiguous — `ContiguousArenaAllocator`

One `Malloc`'d block, bump carved, `realloc`'d on exhaustion. `Resolve` is `_base + handle`, a single add.

- **Pros** Best sequential read and lookup performance — layout locality is maximal by construction.
- **Cons** Growth memcpys the **entire arena** and invalidates every resolved address. Handles survive
  (they are offsets), raw pointers and `NativeArray` views do not. Blast radius is global: growing *any*
  buffer invalidates *all* of them. Needs one unbroken address range; hard 2 GB ceiling.

Mitigation: size `InitialBytes` to the high water mark so growth never happens. Doubling makes the copy
cost amortized O(1) anyway — the hazard is the invalidation, not the time.

### ChunkPaged — `ChunkPagedArenaAllocator` (delete)

One page per entity chunk, buffers laid out in entity order inside it.

- **Cons** Its measured advantage was page size, not grouping. Once plain Paged got 1 MB pages,
  ChunkPaged fell to within 1% of it on reads and is level with DynamicBuffer on writes. Buffers that
  outgrow their chunk page are evicted to the general free lists, so the layout decays. No cell where it
  is the best choice.

Plain Paged is already chunk-contiguous by accident: the reserve system walks chunks in order and carves
in a contiguous run, so buffers land sequentially inside a page with no extra machinery.

### SharedChunkPaged — `SharedArenaAllocator` (delete)

Several element types share one page per entity chunk, tiled like components in a chunk.

- **Cons** Worse than DynamicBuffer at every size above 64. Byte oriented, so `Free` must rebuild block
  size from capacity and element size. **Known bug:** `ReserveShared` allocates page-granular
  (`AllocateChunkPage`) but `ReleaseAll` frees block-granular, corrupting the intrusive chain — crashes
  intermittently at large capacities.

## Measured

`ArenaBufferPerformanceTests`, 5000 entities, 4 byte element, 5 warmup / 20 measured samples, Burst jobs,
`PageShift = 20`. DynamicBuffer comparand has `InternalBufferCapacity(8)`. AMD Ryzen 9 9950X3D, Unity
6000.6.0b5, Entities 6.5.0. Medians in microseconds, lower is better.

### ChunkSum — sequential chunk iteration

| N | Contiguous | Paged | ChunkPaged | Shared | DynamicBuffer |
|---|---|---|---|---|---|
| 4 | 15.5 | **15.3** | 15.7 | 15.7 | 15.9 |
| 64 | 28.1 | **27.6** | 28.5 | 28.8 | 29.1 |
| 128 | **36.3** | 37.1 | 36.6 | 41.1 | 39.5 |
| 256 | 52.7 | **52.1** | 54.6 | 71.6 | 67.3 |
| 512 | **88.6** | 99.2 \* | 97.4 | 159.3 | 135.3 |
| 1024 | **173.8** | 196.1 | 187.8 | crash | 393.6 |

### LookupSum — random access by entity

| N | Contiguous | Paged | ChunkPaged | DynamicBuffer |
|---|---|---|---|---|
| 4 | 21.0 | 19.7 | 23.9 | **19.6** |
| 64 | 44.2 | **43.6** | 48.1 | 50.1 |
| 128 | **69.1** | 76.7 | 77.8 | 83.8 |
| 256 | **148.2** | 148.7 | 152.3 | 162.2 |
| 512 | **207.2** | 213.6 | 222.8 | 237.7 |
| 1024 | **303.0** | 367.0 | 362.0 | 556.7 |

### AddChurn — clear and refill

| N | Contiguous | Paged | ChunkPaged | DynamicBuffer |
|---|---|---|---|---|
| 4 | 11.4 | **10.7** | 14.6 | 14.0 |
| 64 | 138.2 | **122.6** | 183.0 | 179.9 |
| 128 | 275.5 \* | 449.4 \* | 424.2 | 412.0 |
| 256 | 581.0 | **509.8** | 749.2 | 759.9 |
| 512 | 1101.6 | **1002.2** | 1500.9 | 1471.7 |
| 1024 | 2148.2 | **1969.9** | 3093.5 | 3159.1 |

`*` bad samples, non-monotonic against their neighbours. Ignore those cells.

### Against DynamicBuffer (+ is faster)

| N | ChunkSum |  |  | LookupSum |  |  | AddChurn |  |  |
|---|---|---|---|---|---|---|---|---|---|
| | Contig | Paged | ChunkPg | Contig | Paged | ChunkPg | Contig | Paged | ChunkPg |
| 4 | +3% | +4% | +1% | −7% | −1% | −22% | +19% | +24% | −4% |
| 64 | +3% | +5% | +2% | +12% | +13% | +4% | +23% | +32% | −2% |
| 128 | +8% | +6% | +7% | +18% | +9% | +7% | — | — | −3% |
| 256 | +22% | +23% | +19% | +9% | +8% | +6% | +24% | +33% | +1% |
| 512 | +35% | +27% | +28% | +13% | +10% | +6% | +25% | +32% | −2% |
| 1024 | **+56%** | +50% | +52% | **+46%** | +34% | +35% | +32% | **+38%** | +2% |

Shared is omitted: it is slower than DynamicBuffer at every size above 64 (−4% at 128, −6% at 256,
−18% at 512) and crashes at 1024.

### Effect of the handle to pointer change (Paged only)

Contiguous was untouched in both runs and reproduced within 2% (`ChunkSum 1024` 170.2 → 173.8), which is
what makes this a valid cross run comparison.

| | handle | pointer | |
|---|---|---|---|
| AddChurn 4 | 14.7 | 10.7 | **27% faster** |
| AddChurn 64 | 195.0 | 122.6 | **37% faster** |
| AddChurn 256 | 679.6 | 509.8 | **25% faster** |
| AddChurn 1024 | 2486.4 | 1969.9 | **21% faster** |
| LookupSum 4 | 23.2 | 19.7 | 15% faster |
| LookupSum 64 | 70.8 | 43.6 | **38% faster** |
| LookupSum 1024 | 368.1 | 367.0 | unchanged |
| ChunkSum 1024 | 202.0 | 196.1 | unchanged |

The 4 extra bytes per record cost nothing measurable. Writes and small buffer lookups improved sharply,
large sequential reads did not move.

**Contiguous wins reads, Paged wins writes.** Below ~64 elements the arena's fixed per-buffer cost is
not recoverable and DynamicBuffer's `InternalBufferCapacity` inline storage is unbeatable — at 4 elements
it is not a buffer at all, it is chunk memory.

Per element cost: DynamicBuffer ~74 ps, Paged ~36 ps, Contiguous ~28 ps. DynamicBuffer *wins* the fixed
per-entity term and loses the marginal term by 2x, which is why the crossover is around 64–128 elements.

## Benchmarking rules

- Never compare absolute times across runs. An untouched mode anchors a cross-run comparison — verify the
  anchor reproduces before trusting anything else. A result that reproduces on a loaded machine is not
  reproduced.
- Keep access shapes symmetric between modes, and build handles and lookups outside the measured region.
- `readonly`/pure accessors let Burst hoist the resolve out of read loops; mutating accessors block it.
