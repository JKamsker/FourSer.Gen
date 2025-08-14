using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class IncompatibleTypeAnalyzerTests
    {
        private const string GenerateSerializerAttributeSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : System.Attribute { }
}";

        private const string ISerializableSource = @"
namespace FourSer.Contracts
{
    public interface ISerializable<T> where T : ISerializable<T>
    {
        static abstract int GetPacketSize(T obj);
        static abstract T Deserialize(System.ReadOnlySpan<byte> data, out int bytesRead);
        static abstract int Serialize(T obj, System.Span<byte> data);
    }
}";

        [Fact]
        public async Task PropertyWithIncompatibleType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    public IncompatibleType {|FS0002:MyProperty|} { get; set; }
}

public class IncompatibleType { }
";

            var test = new CSharpAnalyzerTest<IncompatibleTypeAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { GenerateSerializerAttributeSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task PropertyWithCompatibleType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    public CompatibleType MyProperty { get; set; }
}

[GenerateSerializer]
public partial class CompatibleType : ISerializable<CompatibleType>
{
    public static int GetPacketSize(CompatibleType obj) => 0;
    public static CompatibleType Deserialize(System.ReadOnlySpan<byte> data, out int bytesRead) { bytesRead = 0; return new CompatibleType(); }
    public static int Serialize(CompatibleType obj, System.Span<byte> data) => 0;
}
";

            var test = new CSharpAnalyzerTest<IncompatibleTypeAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { GenerateSerializerAttributeSource, ISerializableSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };

            await test.RunAsync();
        }
    }
}
