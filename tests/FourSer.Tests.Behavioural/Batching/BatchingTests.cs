namespace FourSer.Tests.Behavioural.Batching;

/// <summary>
/// Tests for batching feature - validates that batched deserialization produces
/// identical results to non-batched deserialization.
/// </summary>
public class BatchingTests
{
    // ========================================================================
    // Round-trip Tests (Serialize -> Deserialize)
    // ========================================================================

    [Fact]
    public void BatchTestAllPrimitives_RoundTrip_Span()
    {
        var original = new BatchTestAllPrimitives
        {
            B1 = 0xFF,
            SB1 = -128,
            S1 = -32768,
            US1 = 65535,
            I1 = int.MinValue,
            UI1 = uint.MaxValue,
            L1 = long.MinValue,
            UL1 = ulong.MaxValue,
            F1 = 3.14159f,
            D1 = 2.718281828
        };

        var size = BatchTestAllPrimitives.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestAllPrimitives.Serialize(original, buffer);
        var result = BatchTestAllPrimitives.Deserialize(buffer);

        Assert.Equal(original.B1, result.B1);
        Assert.Equal(original.SB1, result.SB1);
        Assert.Equal(original.S1, result.S1);
        Assert.Equal(original.US1, result.US1);
        Assert.Equal(original.I1, result.I1);
        Assert.Equal(original.UI1, result.UI1);
        Assert.Equal(original.L1, result.L1);
        Assert.Equal(original.UL1, result.UL1);
        Assert.Equal(original.F1, result.F1);
        Assert.Equal(original.D1, result.D1);
    }

    [Fact]
    public void BatchTestAllPrimitives_RoundTrip_Stream()
    {
        var original = new BatchTestAllPrimitives
        {
            B1 = 0xFF,
            SB1 = -128,
            S1 = -32768,
            US1 = 65535,
            I1 = int.MinValue,
            UI1 = uint.MaxValue,
            L1 = long.MinValue,
            UL1 = ulong.MaxValue,
            F1 = 3.14159f,
            D1 = 2.718281828
        };

        var size = BatchTestAllPrimitives.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestAllPrimitives.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestAllPrimitives.Deserialize(stream);

        Assert.Equal(original.B1, result.B1);
        Assert.Equal(original.SB1, result.SB1);
        Assert.Equal(original.S1, result.S1);
        Assert.Equal(original.US1, result.US1);
        Assert.Equal(original.I1, result.I1);
        Assert.Equal(original.UI1, result.UI1);
        Assert.Equal(original.L1, result.L1);
        Assert.Equal(original.UL1, result.UL1);
        Assert.Equal(original.F1, result.F1);
        Assert.Equal(original.D1, result.D1);
    }

    [Fact]
    public void BatchTestMixedWithString_RoundTrip_Span()
    {
        var original = new BatchTestMixedWithString
        {
            Before1 = 123,
            Before2 = 456,
            Middle = "Hello, World!",
            After1 = 789,
            After2 = 101112
        };

        var size = BatchTestMixedWithString.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestMixedWithString.Serialize(original, buffer);
        var result = BatchTestMixedWithString.Deserialize(buffer);

        Assert.Equal(original.Before1, result.Before1);
        Assert.Equal(original.Before2, result.Before2);
        Assert.Equal(original.Middle, result.Middle);
        Assert.Equal(original.After1, result.After1);
        Assert.Equal(original.After2, result.After2);
    }

    [Fact]
    public void BatchTestMixedWithString_RoundTrip_Stream()
    {
        var original = new BatchTestMixedWithString
        {
            Before1 = 123,
            Before2 = 456,
            Middle = "Hello, World!",
            After1 = 789,
            After2 = 101112
        };

        var size = BatchTestMixedWithString.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestMixedWithString.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestMixedWithString.Deserialize(stream);

        Assert.Equal(original.Before1, result.Before1);
        Assert.Equal(original.Before2, result.Before2);
        Assert.Equal(original.Middle, result.Middle);
        Assert.Equal(original.After1, result.After1);
        Assert.Equal(original.After2, result.After2);
    }

    [Fact]
    public void BatchTestMixedWithCollection_RoundTrip_Stream()
    {
        var original = new BatchTestMixedWithCollection
        {
            Before1 = 100,
            Before2 = 200,
            Items = new List<int> { 1, 2, 3, 4, 5 },
            After1 = 300,
            After2 = 400
        };

        var size = BatchTestMixedWithCollection.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestMixedWithCollection.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestMixedWithCollection.Deserialize(stream);

        Assert.Equal(original.Before1, result.Before1);
        Assert.Equal(original.Before2, result.Before2);
        Assert.Equal(original.Items, result.Items);
        Assert.Equal(original.After1, result.After1);
        Assert.Equal(original.After2, result.After2);
    }

    [Fact]
    public void BatchTestLeadingString_RoundTrip_Stream()
    {
        var original = new BatchTestLeadingString
        {
            Name = "TestName",
            Value1 = 111,
            Value2 = 222,
            Value3 = 333
        };

        var size = BatchTestLeadingString.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestLeadingString.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestLeadingString.Deserialize(stream);

        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.Value1, result.Value1);
        Assert.Equal(original.Value2, result.Value2);
        Assert.Equal(original.Value3, result.Value3);
    }

    [Fact]
    public void BatchTestTrailingCollection_RoundTrip_Stream()
    {
        var original = new BatchTestTrailingCollection
        {
            Header1 = 1000,
            Header2 = 2000,
            Header3 = 3000L,
            Data = new List<byte> { 0x01, 0x02, 0x03, 0x04 }
        };

        var size = BatchTestTrailingCollection.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestTrailingCollection.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestTrailingCollection.Deserialize(stream);

        Assert.Equal(original.Header1, result.Header1);
        Assert.Equal(original.Header2, result.Header2);
        Assert.Equal(original.Header3, result.Header3);
        Assert.Equal(original.Data, result.Data);
    }

    [Fact]
    public void BatchTestSinglePrimitive_RoundTrip_Stream()
    {
        var original = new BatchTestSinglePrimitive { Value = 42 };

        var size = BatchTestSinglePrimitive.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestSinglePrimitive.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestSinglePrimitive.Deserialize(stream);

        Assert.Equal(original.Value, result.Value);
    }

    [Fact]
    public void BatchTestExactThreshold_RoundTrip_Stream()
    {
        var original = new BatchTestExactThreshold
        {
            Value1 = int.MaxValue,
            Value2 = int.MinValue
        };

        var size = BatchTestExactThreshold.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestExactThreshold.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestExactThreshold.Deserialize(stream);

        Assert.Equal(original.Value1, result.Value1);
        Assert.Equal(original.Value2, result.Value2);
    }

    [Fact]
    public void BatchTestBelowThreshold_RoundTrip_Stream()
    {
        var original = new BatchTestBelowThreshold
        {
            Value1 = 12345,
            Value2 = -100,
            Value3 = 255
        };

        var size = BatchTestBelowThreshold.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestBelowThreshold.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestBelowThreshold.Deserialize(stream);

        Assert.Equal(original.Value1, result.Value1);
        Assert.Equal(original.Value2, result.Value2);
        Assert.Equal(original.Value3, result.Value3);
    }

    [Fact]
    public void BatchTestMultipleStrings_RoundTrip_Stream()
    {
        var original = new BatchTestMultipleStrings
        {
            Id = 999,
            Name = "John Doe",
            Age = 30,
            Email = "john@example.com",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var size = BatchTestMultipleStrings.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestMultipleStrings.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestMultipleStrings.Deserialize(stream);

        Assert.Equal(original.Id, result.Id);
        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.Age, result.Age);
        Assert.Equal(original.Email, result.Email);
        Assert.Equal(original.Timestamp, result.Timestamp);
    }

    [Fact]
    public void BatchTestManyBytes_RoundTrip_Stream()
    {
        var original = new BatchTestManyBytes
        {
            B1 = 1, B2 = 2, B3 = 3, B4 = 4, B5 = 5,
            B6 = 6, B7 = 7, B8 = 8, B9 = 9, B10 = 10,
            B11 = 11, B12 = 12, B13 = 13, B14 = 14, B15 = 15,
            B16 = 16, B17 = 17, B18 = 18, B19 = 19, B20 = 20
        };

        var size = BatchTestManyBytes.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestManyBytes.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestManyBytes.Deserialize(stream);

        Assert.Equal(original.B1, result.B1);
        Assert.Equal(original.B10, result.B10);
        Assert.Equal(original.B20, result.B20);
    }

    [Fact]
    public void BatchTestWithBooleans_RoundTrip_Stream()
    {
        var original = new BatchTestWithBooleans
        {
            Flag1 = true,
            Value = 12345,
            Flag2 = false,
            BigValue = 9876543210L,
            Flag3 = true
        };

        var size = BatchTestWithBooleans.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestWithBooleans.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestWithBooleans.Deserialize(stream);

        Assert.Equal(original.Flag1, result.Flag1);
        Assert.Equal(original.Value, result.Value);
        Assert.Equal(original.Flag2, result.Flag2);
        Assert.Equal(original.BigValue, result.BigValue);
        Assert.Equal(original.Flag3, result.Flag3);
    }

    [Fact]
    public void BatchTestNestedType_RoundTrip_Stream()
    {
        var original = new BatchTestNestedType
        {
            Before = 111,
            Nested = new BatchTestSinglePrimitive { Value = 222 },
            After = 333
        };

        var size = BatchTestNestedType.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestNestedType.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestNestedType.Deserialize(stream);

        Assert.Equal(original.Before, result.Before);
        Assert.Equal(original.Nested.Value, result.Nested.Value);
        Assert.Equal(original.After, result.After);
    }

    // ========================================================================
    // Stream vs Span Consistency Tests
    // ========================================================================

    [Fact]
    public void BatchTestAllPrimitives_Stream_Equals_Span()
    {
        var original = new BatchTestAllPrimitives
        {
            B1 = 0xAB, SB1 = -50, S1 = -1000, US1 = 5000,
            I1 = -100000, UI1 = 200000, L1 = -5000000000L, UL1 = 10000000000UL,
            F1 = 1.5f, D1 = 2.5
        };

        var size = BatchTestAllPrimitives.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestAllPrimitives.Serialize(original, buffer);

        var spanResult = BatchTestAllPrimitives.Deserialize(buffer);

        using var stream = new MemoryStream(buffer);
        var streamResult = BatchTestAllPrimitives.Deserialize(stream);

        Assert.Equal(spanResult.B1, streamResult.B1);
        Assert.Equal(spanResult.SB1, streamResult.SB1);
        Assert.Equal(spanResult.S1, streamResult.S1);
        Assert.Equal(spanResult.US1, streamResult.US1);
        Assert.Equal(spanResult.I1, streamResult.I1);
        Assert.Equal(spanResult.UI1, streamResult.UI1);
        Assert.Equal(spanResult.L1, streamResult.L1);
        Assert.Equal(spanResult.UL1, streamResult.UL1);
        Assert.Equal(spanResult.F1, streamResult.F1);
        Assert.Equal(spanResult.D1, streamResult.D1);
    }

    [Fact]
    public void BatchTestMixedWithString_Stream_Equals_Span()
    {
        var original = new BatchTestMixedWithString
        {
            Before1 = 999, Before2 = 888,
            Middle = "Test String",
            After1 = 777, After2 = 666
        };

        var size = BatchTestMixedWithString.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestMixedWithString.Serialize(original, buffer);

        var spanResult = BatchTestMixedWithString.Deserialize(buffer);

        using var stream = new MemoryStream(buffer);
        var streamResult = BatchTestMixedWithString.Deserialize(stream);

        Assert.Equal(spanResult.Before1, streamResult.Before1);
        Assert.Equal(spanResult.Before2, streamResult.Before2);
        Assert.Equal(spanResult.Middle, streamResult.Middle);
        Assert.Equal(spanResult.After1, streamResult.After1);
        Assert.Equal(spanResult.After2, streamResult.After2);
    }

    // ========================================================================
    // Edge Case Tests
    // ========================================================================

    [Fact]
    public void BatchTestMixedWithString_EmptyString_RoundTrip()
    {
        var original = new BatchTestMixedWithString
        {
            Before1 = 1, Before2 = 2,
            Middle = "",
            After1 = 3, After2 = 4
        };

        var size = BatchTestMixedWithString.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestMixedWithString.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestMixedWithString.Deserialize(stream);

        Assert.Equal(original.Middle, result.Middle);
    }

    [Fact]
    public void BatchTestMixedWithCollection_EmptyCollection_RoundTrip()
    {
        var original = new BatchTestMixedWithCollection
        {
            Before1 = 1, Before2 = 2,
            Items = new List<int>(),
            After1 = 3, After2 = 4
        };

        var size = BatchTestMixedWithCollection.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestMixedWithCollection.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestMixedWithCollection.Deserialize(stream);

        Assert.Empty(result.Items);
        Assert.Equal(original.After1, result.After1);
        Assert.Equal(original.After2, result.After2);
    }

    [Fact]
    public void BatchTestAllPrimitives_ZeroValues_RoundTrip()
    {
        var original = new BatchTestAllPrimitives(); // All defaults (zeros)

        var size = BatchTestAllPrimitives.GetPacketSize(original);
        var buffer = new byte[size];
        BatchTestAllPrimitives.Serialize(original, buffer);

        using var stream = new MemoryStream(buffer);
        var result = BatchTestAllPrimitives.Deserialize(stream);

        Assert.Equal(0, result.B1);
        Assert.Equal(0, result.I1);
        Assert.Equal(0UL, result.UL1);
        Assert.Equal(0f, result.F1);
        Assert.Equal(0d, result.D1);
    }
}
