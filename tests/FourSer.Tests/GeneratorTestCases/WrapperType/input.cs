namespace FourSer.Tests.GeneratorTestCases.WrapperType;

// only wraps type and implements ISerializable<T> but has no [GenerateSerializer] attribute
public class FourSerString : ISerializable<FourSerString>
{
    public string Value { get; set; }

    public static int GetPacketSize(FourSerString obj)
    {
        return StringEx.MeasureSize(obj.Value);
    }

    public static int Serialize(FourSerString obj, Span<byte> data)
    {
        var origLen = data.Length;
        StringEx.WriteString(ref data, obj.Value);
        return origLen - data.Length;
    }

    public static void Serialize(FourSerString obj, Stream stream)
    {
        StreamWriterHelpers.WriteString(stream, obj.Value);
    }

    public static FourSerString Deserialize(ref ReadOnlySpan<byte> data)
    {
        var str = StringEx.ReadString(ref data);
        return new FourSerString { Value = str };
    }

    public static FourSerString Deserialize(ReadOnlySpan<byte> data)
    {
        return Deserialize(ref data);
    }

    public static FourSerString Deserialize(Stream stream)
    {
        var str = StreamReaderHelpers.ReadString(stream);
        return new FourSerString { Value = str };
    }
}

[GenerateSerializer
public class WrapperUserClass
{
    public FourSerString Name { get; set; }
}
