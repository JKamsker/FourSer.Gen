using FluentAssertions;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace FourSer.Tests;

public class ModelTests
{
    [Fact]
    public void Models_ShouldBe_Cacheable()
    {
        const string source = @"
using System.Collections.Generic;
namespace MyCode
{
    public class MyType
    {
        public List<int> Values { get; set; }
    }
}";

        var compilation1 = CSharpCompilation.Create("MyCompilation1",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var compilation2 = CSharpCompilation.Create("MyCompilation2",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var typeSymbol1 = compilation1.GetTypeByMetadataName("MyCode.MyType");
        var typeSymbol2 = compilation2.GetTypeByMetadataName("MyCode.MyType");

        var collectionTypeInfo1 = new CollectionTypeInfo(typeSymbol1.ToDisplayString(), "int", true, false, false, false, null, false);
        var collectionTypeInfo2 = new CollectionTypeInfo(typeSymbol2.ToDisplayString(), "int", true, false, false, false, null, false);

        collectionTypeInfo1.Should().Be(collectionTypeInfo2);
    }
}
