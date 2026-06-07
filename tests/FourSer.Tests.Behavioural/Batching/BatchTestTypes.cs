using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Batching;

// ============================================================================
// Test Types for Batching Feature
// ============================================================================

/// <summary>
/// All primitive unmanaged types - ideal batching case (single batch).
/// Total size: 1+1+2+2+4+4+8+8+4+8 = 42 bytes
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestAllPrimitives
{
    public byte B1 { get; set; }
    public sbyte SB1 { get; set; }
    public short S1 { get; set; }
    public ushort US1 { get; set; }
    public int I1 { get; set; }
    public uint UI1 { get; set; }
    public long L1 { get; set; }
    public ulong UL1 { get; set; }
    public float F1 { get; set; }
    public double D1 { get; set; }
}

/// <summary>
/// Primitives with a string in the middle - should produce two batches.
/// Batch1: Before1 (4) + Before2 (4) = 8 bytes
/// String: variable
/// Batch2: After1 (4) + After2 (4) = 8 bytes
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestMixedWithString
{
    public int Before1 { get; set; }
    public int Before2 { get; set; }
    public string Middle { get; set; } = string.Empty;
    public int After1 { get; set; }
    public int After2 { get; set; }
}

/// <summary>
/// Primitives with a collection in the middle - should produce two batches.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestMixedWithCollection
{
    public int Before1 { get; set; }
    public int Before2 { get; set; }

    [SerializeCollection(CountType = typeof(byte))]
    public List<int> Items { get; set; } = new();

    public int After1 { get; set; }
    public int After2 { get; set; }
}

/// <summary>
/// String first, then primitives - should produce one batch after string.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestLeadingString
{
    public string Name { get; set; } = string.Empty;
    public int Value1 { get; set; }
    public int Value2 { get; set; }
    public int Value3 { get; set; }
}

/// <summary>
/// Primitives, then collection at end - should produce one batch before collection.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestTrailingCollection
{
    public int Header1 { get; set; }
    public int Header2 { get; set; }
    public long Header3 { get; set; }

    [SerializeCollection(CountType = typeof(byte))]
    public List<byte> Data { get; set; } = new();
}

/// <summary>
/// Single primitive - should NOT batch (below minimum threshold).
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestSinglePrimitive
{
    public int Value { get; set; }
}

/// <summary>
/// Two small primitives - exactly at 8 byte threshold.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestExactThreshold
{
    public int Value1 { get; set; }
    public int Value2 { get; set; }
}

/// <summary>
/// Below threshold - 7 bytes, should NOT batch.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestBelowThreshold
{
    public int Value1 { get; set; }   // 4 bytes
    public short Value2 { get; set; } // 2 bytes
    public byte Value3 { get; set; }  // 1 byte = 7 total
}

/// <summary>
/// Multiple strings interleaved with primitives - multiple small batches.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestMultipleStrings
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}

/// <summary>
/// Many consecutive bytes - tests large batch with simple types.
/// Total: 20 bytes
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestManyBytes
{
    public byte B1 { get; set; }
    public byte B2 { get; set; }
    public byte B3 { get; set; }
    public byte B4 { get; set; }
    public byte B5 { get; set; }
    public byte B6 { get; set; }
    public byte B7 { get; set; }
    public byte B8 { get; set; }
    public byte B9 { get; set; }
    public byte B10 { get; set; }
    public byte B11 { get; set; }
    public byte B12 { get; set; }
    public byte B13 { get; set; }
    public byte B14 { get; set; }
    public byte B15 { get; set; }
    public byte B16 { get; set; }
    public byte B17 { get; set; }
    public byte B18 { get; set; }
    public byte B19 { get; set; }
    public byte B20 { get; set; }
}

/// <summary>
/// Boolean values mixed with other primitives.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestWithBooleans
{
    public bool Flag1 { get; set; }
    public int Value { get; set; }
    public bool Flag2 { get; set; }
    public long BigValue { get; set; }
    public bool Flag3 { get; set; }
}

/// <summary>
/// Nested serializable type - should break the batch.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestNestedType
{
    public int Before { get; set; }
    public BatchTestSinglePrimitive Nested { get; set; } = new();
    public int After { get; set; }
}

/// <summary>
/// Large primitive payload to force the pooled stream batch path.
/// </summary>
[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class LargeBatchWritePacket
{
    public int Value01 { get; set; }
    public int Value02 { get; set; }
    public int Value03 { get; set; }
    public int Value04 { get; set; }
    public int Value05 { get; set; }
    public int Value06 { get; set; }
    public int Value07 { get; set; }
    public int Value08 { get; set; }
    public int Value09 { get; set; }
    public int Value10 { get; set; }
    public int Value11 { get; set; }
    public int Value12 { get; set; }
    public int Value13 { get; set; }
    public int Value14 { get; set; }
    public int Value15 { get; set; }
    public int Value16 { get; set; }
    public int Value17 { get; set; }
    public int Value18 { get; set; }
    public int Value19 { get; set; }
    public int Value20 { get; set; }
    public int Value21 { get; set; }
    public int Value22 { get; set; }
    public int Value23 { get; set; }
    public int Value24 { get; set; }
    public int Value25 { get; set; }
    public int Value26 { get; set; }
    public int Value27 { get; set; }
    public int Value28 { get; set; }
    public int Value29 { get; set; }
    public int Value30 { get; set; }
    public int Value31 { get; set; }
    public int Value32 { get; set; }
    public int Value33 { get; set; }
    public int Value34 { get; set; }
    public int Value35 { get; set; }
    public int Value36 { get; set; }
    public int Value37 { get; set; }
    public int Value38 { get; set; }
    public int Value39 { get; set; }
    public int Value40 { get; set; }
    public int Value41 { get; set; }
    public int Value42 { get; set; }
    public int Value43 { get; set; }
    public int Value44 { get; set; }
    public int Value45 { get; set; }
    public int Value46 { get; set; }
    public int Value47 { get; set; }
    public int Value48 { get; set; }
    public int Value49 { get; set; }
    public int Value50 { get; set; }
    public int Value51 { get; set; }
    public int Value52 { get; set; }
    public int Value53 { get; set; }
    public int Value54 { get; set; }
    public int Value55 { get; set; }
    public int Value56 { get; set; }
    public int Value57 { get; set; }
    public int Value58 { get; set; }
    public int Value59 { get; set; }
    public int Value60 { get; set; }
    public int Value61 { get; set; }
    public int Value62 { get; set; }
    public int Value63 { get; set; }
    public int Value64 { get; set; }
    public int Value65 { get; set; }
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class FusedStringPacket
{
    public string Value { get; set; } = string.Empty;
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class BatchTestFixedCollectionSandwich
{
    public int Before1 { get; set; }
    public int Before2 { get; set; }

    [SerializeCollection(CountSize = 4)]
    public int[] Middle { get; set; } = new int[4];

    public int After1 { get; set; }
    public int After2 { get; set; }
}
