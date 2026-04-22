namespace FourSer.Tests.OptimizationTesting;

internal static class CoreOptimizationCases
{
    public static IEnumerable<OptimizationRepresentativeCase> GetCases()
    {
        yield return new OptimizationRepresentativeCase(
            "AllPrimitives",
            "FourSer.Tests.RuntimeCases.AllPrimitivesPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class AllPrimitivesPacket
            {
                public byte ByteValue { get; set; }
                public short ShortValue { get; set; }
                public int IntValue { get; set; }
                public long LongValue { get; set; }
                public float FloatValue { get; set; }
                public double DoubleValue { get; set; }
                public decimal DecimalValue { get; set; }

                public static AllPrimitivesPacket CreateSample()
                {
                    return new AllPrimitivesPacket
                    {
                        ByteValue = 0x2A,
                        ShortValue = -1234,
                        IntValue = 1_234_567,
                        LongValue = 9_876_543_210L,
                        FloatValue = 123.5f,
                        DoubleValue = 456.75,
                        DecimalValue = 789.125m,
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "StringHeavy",
            "FourSer.Tests.RuntimeCases.StringHeavyPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class StringHeavyPacket
            {
                public string First { get; set; } = string.Empty;
                public string Second { get; set; } = string.Empty;
                public string Third { get; set; } = string.Empty;
                public int Counter { get; set; }

                public static StringHeavyPacket CreateSample()
                {
                    return new StringHeavyPacket
                    {
                        First = "alpha-beta-gamma",
                        Second = "delta-epsilon-zeta",
                        Third = "eta-theta-iota-kappa",
                        Counter = 42,
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "NestedSerializable",
            "FourSer.Tests.RuntimeCases.NestedPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class NestedValue
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            [GenerateSerializer]
            public partial class NestedPacket
            {
                public short Prefix { get; set; }
                public NestedValue Value { get; set; } = new();
                public long Suffix { get; set; }

                public static NestedPacket CreateSample()
                {
                    return new NestedPacket
                    {
                        Prefix = 77,
                        Value = new NestedValue
                        {
                            Id = 1234,
                            Name = "nested",
                        },
                        Suffix = 9001,
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "CustomSerializer",
            "FourSer.Tests.RuntimeCases.CustomSerializerPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            public sealed class WrappedStringSerializer : ISerializer<string>
            {
                public int GetPacketSize(string obj) => sizeof(int) + (obj?.Length ?? 0);

                public int Serialize(string obj, Span<byte> data)
                {
                    var length = obj?.Length ?? 0;
                    BitConverter.TryWriteBytes(data, length);
                    for (var i = 0; i < length; i++)
                    {
                        data[sizeof(int) + i] = (byte)obj![i];
                    }

                    return sizeof(int) + length;
                }

                public void Serialize(string obj, Stream stream)
                {
                    var buffer = new byte[GetPacketSize(obj)];
                    Serialize(obj, buffer);
                    stream.Write(buffer);
                }

                public string Deserialize(ref ReadOnlySpan<byte> data)
                {
                    var length = BitConverter.ToInt32(data.Slice(0, sizeof(int)));
                    var chars = new char[length];
                    for (var i = 0; i < length; i++)
                    {
                        chars[i] = (char)data[sizeof(int) + i];
                    }

                    data = data.Slice(sizeof(int) + length);
                    return new string(chars);
                }

                public string Deserialize(Stream stream)
                {
                    Span<byte> header = stackalloc byte[sizeof(int)];
                    stream.ReadExactly(header);
                    var length = BitConverter.ToInt32(header);
                    var payload = new byte[length];
                    stream.ReadExactly(payload);
                    return new string(payload.Select(static value => (char)value).ToArray());
                }
            }

            [GenerateSerializer]
            public partial class CustomSerializerPacket
            {
                [Serializer(typeof(WrappedStringSerializer))]
                public string Name { get; set; } = string.Empty;

                public static CustomSerializerPacket CreateSample()
                {
                    return new CustomSerializerPacket
                    {
                        Name = "serializer",
                    };
                }
            }
            """);
    }
}
