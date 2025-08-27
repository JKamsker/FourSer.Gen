using FluentAssertions;
using FourSer.Gen.Models;
using FourSer.Gen.Models.Raw;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

using System.Collections.Immutable;
using System.Linq;

namespace FourSer.Tests;

public class ModelTests
{
    [Fact]
    public void Models_ShouldBe_Cacheable_And_Transformation_Is_Correct()
    {
        const string source = @"
using System.Collections.Generic;
namespace MyCode
{
    public class MyType
    {
        public List<int> Values { get; set; }
        public IEnumerable<string> Names { get; set; }
    }
}";

        var compilation1 = CSharpCompilation.Create("MyCompilation1",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location) });

        var compilation2 = CSharpCompilation.Create("MyCompilation2",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location) });

        var typeSymbol1 = compilation1.GetTypeByMetadataName("MyCode.MyType");
        var typeSymbol2 = compilation2.GetTypeByMetadataName("MyCode.MyType");

        // This is a simplified way to get the RawTypeToGenerate, in reality the generator does more.
        // For this test, we are focusing on the transformation of CollectionTypeInfo.
        var rawMember1_1 = new RawMemberToGenerate(typeSymbol1.GetMembers("Values").First(), (ITypeSymbol)typeSymbol1.GetMembers("Values").First().GetSymbolType(), true, null, null, null, true, new RawCollectionTypeInfo(typeSymbol1.GetMembers("Values").First().GetSymbolType(), ((INamedTypeSymbol)typeSymbol1.GetMembers("Values").First().GetSymbolType()).TypeArguments.First(), false, null, false), false, false, null);
        var rawMember1_2 = new RawMemberToGenerate(typeSymbol1.GetMembers("Names").First(), (ITypeSymbol)typeSymbol1.GetMembers("Names").First().GetSymbolType(), false, null, null, null, true, new RawCollectionTypeInfo(typeSymbol1.GetMembers("Names").First().GetSymbolType(), ((INamedTypeSymbol)typeSymbol1.GetMembers("Names").First().GetSymbolType()).TypeArguments.First(), false, "System.Collections.Generic.List", true), false, false, null);
        var rawType1 = new RawTypeToGenerate(typeSymbol1, new EquatableArray<RawMemberToGenerate>(new []{rawMember1_1, rawMember1_2}.ToImmutableArray()), default, false, null, default);

        var rawMember2_1 = new RawMemberToGenerate(typeSymbol2.GetMembers("Values").First(), (ITypeSymbol)typeSymbol2.GetMembers("Values").First().GetSymbolType(), true, null, null, null, true, new RawCollectionTypeInfo(typeSymbol2.GetMembers("Values").First().GetSymbolType(), ((INamedTypeSymbol)typeSymbol2.GetMembers("Values").First().GetSymbolType()).TypeArguments.First(), false, null, false), false, false, null);
        var rawMember2_2 = new RawMemberToGenerate(typeSymbol2.GetMembers("Names").First(), (ITypeSymbol)typeSymbol2.GetMembers("Names").First().GetSymbolType(), false, null, null, null, true, new RawCollectionTypeInfo(typeSymbol2.GetMembers("Names").First().GetSymbolType(), ((INamedTypeSymbol)typeSymbol2.GetMembers("Names").First().GetSymbolType()).TypeArguments.First(), false, "System.Collections.Generic.List", true), false, false, null);
        var rawType2 = new RawTypeToGenerate(typeSymbol2, new EquatableArray<RawMemberToGenerate>(new []{rawMember2_1, rawMember2_2}.ToImmutableArray()), default, false, null, default);

        var transformedType1 = ModelTransformer.Transform(rawType1);
        var transformedType2 = ModelTransformer.Transform(rawType2);

        transformedType1.Should().Be(transformedType2);

        var valuesMember = transformedType1.Members[0];
        valuesMember.IsList.Should().BeTrue();
        valuesMember.CollectionTypeInfo.Should().NotBeNull();
        valuesMember.CollectionTypeInfo.Value.IsGenericIList.Should().BeTrue();
        valuesMember.CollectionTypeInfo.Value.IsGenericIEnumerable.Should().BeTrue();
        valuesMember.CollectionTypeInfo.Value.CollectionAddMethodName.Should().Be("Add");

        var namesMember = transformedType1.Members[1];
        namesMember.IsList.Should().BeFalse();
        namesMember.CollectionTypeInfo.Should().NotBeNull();
        namesMember.CollectionTypeInfo.Value.IsGenericIList.Should().BeFalse();
        namesMember.CollectionTypeInfo.Value.IsGenericIEnumerable.Should().BeTrue();
        namesMember.CollectionTypeInfo.Value.CollectionAddMethodName.Should().Be("Add");
    }
}

public static class SymbolExtensions
{
    public static ITypeSymbol GetSymbolType(this ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol p => p.Type,
            IFieldSymbol f => f.Type,
            _ => null
        };
    }
}

