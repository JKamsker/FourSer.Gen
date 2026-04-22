# Optimizer Design

## Scope

The optimizer branch adds an internal planning pipeline between Roslyn discovery and generated C# emission. It optimizes serialization semantics, not Roslyn syntax trees, and keeps the generator's existing incremental discovery model plus `IndentedStringBuilder`-based source emission.

This branch targets `.NET 9+` consumers. The current `TargetCapabilityProvider` intentionally exposes a fixed `.NET 9` capability profile instead of probing per-compilation APIs.

## Architecture

`SerializerGenerator` remains the top-level orchestrator. Generator options are read from `AnalyzerConfigOptionsProvider.GlobalOptions` through `FourSerGeneratorOptionsProvider`, then passed into `PlanPipeline`.

`PlanPipeline` builds and transforms `MethodPlan` instances for:

- packet size generation
- span serialization
- stream serialization
- span deserialization
- stream deserialization

The pipeline is split into three layers under `src/FourSer.Gen/CodeGenerators`:

- `Planning`: semantic models, planning helpers, and plan builders
- `Optimization`: one pass per file, each rewriting explicit plan ops
- `Emission`: thin lowering helpers that turn plan ops into C#

## Planning Model

The plan layer uses a small string-backed IR. Ops may carry local names and emitted expressions as strings, but optimization passes reason over explicit metadata rather than parsing arbitrary C#.

Key internal models:

- `MethodPlan`
- `PlanFacts`
- `MethodKind`
- `TargetKind`
- `TargetCapabilities`
- `CollectionPlan`
- `PolymorphicPlan`
- `ConstructionPlan`
- `GuardKey`
- `GuardScope`
- `BulkLayoutMode`

Representative ops:

- local/control flow: `DeclareLocalOp`, `AssignOp`, `GuardOp`, `BarrierOp`
- scalar/string/count: `ScalarReadOp`, `ScalarWriteOp`, `StringReadOp`, `StringWriteOp`, `CountReadOp`, `CountWriteOp`, `SizeAddOp`
- polymorphism and nested serializers: `TypeIdResolveOp`, `TypeIdMutationOp`, `SerializeNestedOp`, `DeserializeNestedOp`, `CustomSerializerOp`, `PolymorphicSwitchOp`, `ConstructObjectOp`
- collection and batching: `CollectionReadOp`, `CollectionWriteOp`, `FixedBytesReadOp`, `FixedBytesWriteOp`, `BatchReadOp`, `BatchWriteOp`

## Pass Order

`PlanPipeline` runs passes in this order:

1. `NormalizePlanPass`
2. `CanonicalGuardPass`
3. `CountCachingPass`
4. `TypeIdCachingPass`
5. `ConstantSizeFoldPass`
6. `BatchPrimitiveRunsPass`
7. `StringFusionPass`
8. `CollectionFastPathPass`
9. `PolymorphicOptimizationPass`
10. `CleanupPlanPass`
11. `ValidatePlanPass`

Current responsibilities:

- normalization computes reusable semantic facts once
- conservative passes deduplicate guards and cache repeated count/type-id work
- batching replaces direct-emission batching logic with plan-level grouped runs
- stream string fusion emits one buffered write for string length plus UTF-8 payload
- cleanup removes redundant plan noise and can keep optional optimization comments
- validation rejects invalid plans before emission

## Optimization Levels

The generator exposes these MSBuild properties:

- `FourSerOptimizationLevel`
- `FourSerMinBatchBytes`
- `FourSerStackallocThreshold`
- `FourSerMaxBatchBytes`
- `FourSerEmitOptimizationComments`

`FourSerOptimizationLevel` values:

- `Off`: planning, validation, and cleanup only
- `Conservative`: adds guard/count/type-id caching, constant size folding, and batching
- `AggressivePortable`: adds stream string fusion and portable collection fast paths
- `AggressiveNativeLayout`: enables runtime-guarded native-layout fast paths with portable fallback

The package default is `AggressivePortable`.

## Target Capabilities

The current fixed capability profile is:

- `HasStreamReadExactly = true`
- `HasStreamWriteSpan = true`
- `HasCollectionsMarshalAsSpan = true`
- `HasCollectionsMarshalSetCount = true`
- `HasDecimalGetBitsSpan = true`
- `HasEnumerableTryGetNonEnumeratedCount = true`

The provider abstraction remains in place so per-compilation probing can be added later without rewriting the plan pipeline.

## Diagnostics

The optimizer-specific informational diagnostics are:

- `FSGOPT001`
- `FSGOPT002`
- `FSGOPT003`
- `FSGOPT004`

They are intended for "fast path skipped" and "fallback chosen" reporting when the optimizer actively evaluates an optimization and declines it.

## Current Boundaries

This branch preserves current observable behavior, including existing type-id property mutation side effects during serialization.

Still intentionally out of scope here:

- public contract changes in `FourSer.Contracts`
- `Memory<T>` / `ReadOnlyMemory<T>` support
- widening consumer TFM support below `.NET 9`
- replacing helper fallback code with many new runtime helper APIs
