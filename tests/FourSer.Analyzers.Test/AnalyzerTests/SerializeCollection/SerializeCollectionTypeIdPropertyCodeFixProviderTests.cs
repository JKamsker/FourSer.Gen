using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionTypeIdPropertyCodeFixProviderTests
{
    private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public string TypeIdProperty { get; set; }
    }
}";

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

        await new CSharpCodeFixTest<SerializeCollectionTypeIdPropertyAnalyzer, SerializeCollectionTypeIdPropertyCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
            FixedState = { Sources = { AttributesSource, fixedCode } },
        }.RunAsync();
    }

    [Fact]
    public async Task NotFound_WithBytePolymorphicOptions_CreatesByteProperty()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
using System;

public class MyData
{
    [SerializeCollection({|FSG1007:TypeIdProperty = ""TypeId""|}, PolymorphicMode = PolymorphicMode.SingleTypeId)]
    [PolymorphicOption((byte)1, typeof(string))]
    public List<string> A { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
using System;

public class MyData
{
    public byte TypeId { get; set; }
    [SerializeCollection(TypeIdProperty = ""TypeId"", PolymorphicMode = PolymorphicMode.SingleTypeId)]
    [PolymorphicOption((byte)1, typeof(string))]
    public List<string> A { get; set; }
}";

        var polymorphicOptionAttribute = @"
namespace FourSer.Contracts
{
    using System;
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(object typeId, Type type)
        {
        }
    }
}";

        var polymorphicModeEnum = @"
namespace FourSer.Contracts
{
    public enum PolymorphicMode
    {
        None,
        SingleTypeId,
        IndividualTypeIds
    }
}";

        var collectionAttribute = @"
namespace FourSer.Contracts
{
    using System;
    using System.Collections.Generic;

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public string TypeIdProperty { get; set; }
        public PolymorphicMode PolymorphicMode { get; set; }
    }
}";

        await new CSharpCodeFixTest<SerializeCollectionTypeIdPropertyAnalyzer, SerializeCollectionTypeIdPropertyCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { collectionAttribute, polymorphicOptionAttribute, polymorphicModeEnum, testCode } },
            FixedState = { Sources = { collectionAttribute, polymorphicOptionAttribute, polymorphicModeEnum, fixedCode } },
        }.RunAsync();
    }
}