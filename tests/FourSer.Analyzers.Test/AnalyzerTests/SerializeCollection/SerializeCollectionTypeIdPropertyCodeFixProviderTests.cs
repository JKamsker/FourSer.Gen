using System.IO;
using FourSer.Analyzers.SerializeCollection;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionTypeIdPropertyCodeFixProviderTests : AnalyzerTestBase
{
    // No need for AttributesSource, use real FourSer.Contracts

    [Fact]
    public async Task NotFound_CreatesProperty()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|FSG1007:TypeIdProperty = ""TypeId""|})]
    public List<int> A { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int TypeId { get; set; }
    [SerializeCollection(TypeIdProperty = ""TypeId"")]
    public List<int> A { get; set; }
}";

        await new
            CSharpCodeFixTest<SerializeCollectionTypeIdPropertyAnalyzer, SerializeCollectionTypeIdPropertyCodeFixProvider,
                DefaultVerifier>
            {
                TestState = { Sources = { testCode } },
                FixedState = { Sources = { fixedCode } },
                ReferenceAssemblies = ReferenceAssemblies
            }.RunAsync();
    }

    [Theory]
    [InlineData("(byte)", "byte")]
    [InlineData("(long)", "long")]
    [InlineData("(EnumYolo)", "EnumYolo")]
    public async Task NotFound_WithPolymorphicOptions_CreatesCorrectPropertyType(string typeCast, string typeName)
    {
        var testCode = @$"
using FourSer.Contracts;
using System.Collections.Generic;
using System;

public class MyData
{{
    [SerializeCollection({{|FSG1007:TypeIdProperty = ""TypeId""|}}, PolymorphicMode = PolymorphicMode.SingleTypeId)]
    [PolymorphicOption({typeCast}1, typeof(string))]
    public List<string> A {{ get; set; }}
}}";

        var fixedCode = @$"
using FourSer.Contracts;
using System.Collections.Generic;
using System;

public class MyData
{{
    public {typeName} TypeId {{ get; set; }}
    [SerializeCollection(TypeIdProperty = ""TypeId"", PolymorphicMode = PolymorphicMode.SingleTypeId)]
    [PolymorphicOption({typeCast}1, typeof(string))]
    public List<string> A {{ get; set; }}
}}";

        var additionalEnum =
            // lang=cs
            """
            namespace FourSer.Contracts;

            public enum EnumYolo
            {
                None,
                OneOption,
                AnotherOption
            }
            """;

        await new
            CSharpCodeFixTest<SerializeCollectionTypeIdPropertyAnalyzer, SerializeCollectionTypeIdPropertyCodeFixProvider,
                DefaultVerifier>
            {
                TestState = { Sources = { testCode, additionalEnum } },
                FixedState = { Sources = { fixedCode, additionalEnum } },
                ReferenceAssemblies = ReferenceAssemblies
            }.RunAsync();
    }
}
