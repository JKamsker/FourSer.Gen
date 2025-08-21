using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionOnCorrectMemberCodeFixProviderTests
{
    private const string AttributesSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public class SerializeCollectionAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public class SerializePolymorphicAttribute : System.Attribute { }
}";

    [Fact]
    public async Task RemoveSerializeCollection_RemovesAttribute()
    {
        var testCode = @"
using FourSer.Contracts;

public class MyData
{
    [{|FSG1000:SerializeCollection|}]
    public int A { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;

public class MyData
{
    public int A { get; set; }
}";

        await new CSharpCodeFixTest<SerializeCollectionOnCorrectMemberAnalyzer, SerializeCollectionOnCorrectMemberCodeFixProvider, DefaultVerifier>
        {
            TestState =
            {
                Sources = { AttributesSource, testCode },
            },
            FixedState =
            {
                Sources = { AttributesSource, fixedCode },
            },
            CodeActionIndex = 0, // Remove attribute
        }.RunAsync();
    }

    [Fact]
    public async Task ReplaceWithPolymorphic_ReplacesAttribute()
    {
        var testCode = @"
using FourSer.Contracts;

public class MyData
{
    [{|FSG1000:SerializeCollection|}]
    public int A { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;

public class MyData
{
    [SerializePolymorphic]
    public int A { get; set; }
}";

        await new CSharpCodeFixTest<SerializeCollectionOnCorrectMemberAnalyzer, SerializeCollectionOnCorrectMemberCodeFixProvider, DefaultVerifier>
        {
            TestState =
            {
                Sources = { AttributesSource, testCode },
            },
            FixedState =
            {
                Sources = { AttributesSource, fixedCode },
            },
            CodeActionIndex = 1, // Replace with polymorphic
        }.RunAsync();
    }
}