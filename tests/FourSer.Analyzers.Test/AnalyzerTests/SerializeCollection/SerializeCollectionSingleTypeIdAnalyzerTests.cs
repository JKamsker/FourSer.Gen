using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection
{
    public class SerializeCollectionSingleTypeIdAnalyzerTests
    {
        private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class SerializeCollectionAttribute : Attribute
    {
        public PolymorphicMode PolymorphicMode { get; set; }
        public Type TypeIdType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(object id, Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class GenerateSerializerAttribute : Attribute { }
}";

        [Fact]
        public async Task TypeIdType_Matches_OptionIdType_NoDiagnostic()
        {
            var testCode = @"
    using System;
    using FourSer.Contracts;
    using System.Collections.Generic;

    public class Item { }
    public class Sword : Item { }

    [GenerateSerializer]
    public partial class Inventory
    {
        [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte))]
        [PolymorphicOption((byte)10, typeof(Sword))]
        public IEnumerable<Item> Items { get; set; } = new List<Item>();
    }";
            await new CSharpAnalyzerTest<SerializeCollectionSingleTypeIdAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task Default_TypeIdType_Matches_OptionIdType_NoDiagnostic()
        {
            var testCode = @"
    using System;
    using FourSer.Contracts;
    using System.Collections.Generic;

    public class Item { }
    public class Sword : Item { }

    [GenerateSerializer]
    public partial class Inventory
    {
        [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
        [PolymorphicOption(10, typeof(Sword))]
        public IEnumerable<Item> Items { get; set; } = new List<Item>();
    }";
            await new CSharpAnalyzerTest<SerializeCollectionSingleTypeIdAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task TypeIdType_Mismatch_OptionIdType_ReportsDiagnostic()
        {
            var testCode = @"
    using System;
    using FourSer.Contracts;
    using System.Collections.Generic;

    public class Item { }
    public class Sword : Item { }

    [GenerateSerializer]
    public partial class Inventory
    {
        [{|FSG1014:SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(int))|}]
        [PolymorphicOption((byte)10, typeof(Sword))]
        public IEnumerable<Item> Items { get; set; } = new List<Item>();
    }";
            await new CSharpAnalyzerTest<SerializeCollectionSingleTypeIdAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task User_Provided_Example_ReportsDiagnostic()
        {
            var testCode = @"
    using System;
    using FourSer.Contracts;
    using System.Collections.Generic;

    public class Item { }
    public class Sword : Item { }
    public class Shield : Item { }
    public class Potion : Item { }

    [GenerateSerializer]
    public partial class Inventory
    {
        [{|FSG1014:SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(object))|}]
        [PolymorphicOption((byte)10, typeof(Sword))]
        [PolymorphicOption((byte)20, typeof(Shield))]
        [PolymorphicOption((byte)30, typeof(Potion))]
        public IEnumerable<Item> Items { get; set; } = new List<Item>();
    }";
            await new CSharpAnalyzerTest<SerializeCollectionSingleTypeIdAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task Unset_TypeIdType_With_Byte_OptionIdType_NoDiagnostic()
        {
            var testCode = @"
    using System;
    using FourSer.Contracts;
    using System.Collections.Generic;

    public class Item { }
    public class Sword : Item { }

    [GenerateSerializer]
    public partial class Inventory
    {
        [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
        [PolymorphicOption((byte)10, typeof(Sword))]
        public IEnumerable<Item> Items { get; set; } = new List<Item>();
    }";
            await new CSharpAnalyzerTest<SerializeCollectionSingleTypeIdAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }
    }
}
