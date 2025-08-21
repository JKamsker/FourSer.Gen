using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionPolymorphismCodeFixProviderTests
{
    private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public PolymorphicMode PolymorphicMode { get; set; }
        public string TypeIdProperty { get; set; }
        public Type TypeIdType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializePolymorphicAttribute : Attribute
    {
        public string PropertyName { get; set; }
        public Type TypeIdType { get; set; }
    }
}";

    [Fact]
    public async Task IndividualTypeIdsWithTypeIdProperty_RemovesTypeIdProperty()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, {|FSG1011:TypeIdProperty = ""TypeId""|})]
    public List<int> A { get; set; }
    public int TypeId { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds)]
    public List<int> A { get; set; }
    public int TypeId { get; set; }
}";

        await new CSharpCodeFixTest<SerializeCollectionPolymorphismAnalyzer, SerializeCollectionPolymorphismCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
            FixedState = { Sources = { AttributesSource, fixedCode } },
        }.RunAsync();
    }
}