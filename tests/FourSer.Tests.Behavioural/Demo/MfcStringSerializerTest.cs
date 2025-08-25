using System;
using System.IO;
using System.Text;
using Xunit;

namespace FourSer.Tests.Behavioural.Demo;

public class MfcStringSerializerTest
{
    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("World")]
    [InlineData("A longer string with various characters: αβγδ!@#$%")]
    [InlineData("Test with unicode: 🌍🚀✨")]
    public void MfcStringSerializer_RoundTrip_ShouldPreserveData(string testString)
    {
        var serializer = new MfcStringSerializer();
        
        // Test stream-based serialization/deserialization
        using var stream = new MemoryStream();
        serializer.Serialize(testString, stream);
        
        stream.Position = 0;
        var deserializedString = serializer.Deserialize(stream);
        
        Assert.Equal(testString, deserializedString);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("Short")]
    [InlineData("A longer string to test span-based serialization")]
    public void MfcStringSerializer_SpanBasedRoundTrip_ShouldPreserveData(string testString)
    {
        var serializer = new MfcStringSerializer();
        
        // Get required size
        int size = serializer.GetPacketSize(testString);
        
        // Test span-based serialization/deserialization
        Span<byte> buffer = new byte[size];
        int bytesWritten = serializer.Serialize(testString, buffer);
        
        Assert.Equal(size, bytesWritten);
        
        ReadOnlySpan<byte> readBuffer = buffer;
        var deserializedString = serializer.Deserialize(ref readBuffer);
        
        Assert.Equal(testString, deserializedString);
        Assert.Equal(0, readBuffer.Length); // All bytes should be consumed
    }
    
    [Fact]
    public void MfcStringSerializer_GetPacketSize_ShouldReturnCorrectSize()
    {
        var serializer = new MfcStringSerializer();
        var testString = "Test";
        
        int expectedSize = serializer.GetPacketSize(testString);
        
        using var stream = new MemoryStream();
        serializer.Serialize(testString, stream);
        
        Assert.Equal(expectedSize, stream.Length);
    }
}
