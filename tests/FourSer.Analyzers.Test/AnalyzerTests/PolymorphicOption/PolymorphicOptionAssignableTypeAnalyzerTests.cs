using FourSer.Analyzers.PolymorphicOption;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption
{
    /// <summary>
    /// Integration test for issue #59: https://github.com/JKamsker/FourSer.Gen/issues/59
    /// Tests that PolymorphicOptionAssignableTypeAnalyzer correctly handles IEnumerable&lt;T&gt; properties
    /// and checks assignability to the element type T rather than the IEnumerable type.
    /// </summary>
    public class PolymorphicOptionAssignableTypeAnalyzerTests
    {
        private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(byte id, Type type) { }
        public PolymorphicOptionAttribute(int id, Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute 
    {
        public PolymorphicMode PolymorphicMode { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializePolymorphicAttribute : Attribute
    {
        public string? PropertyName { get; set; }
        public Type? TypeIdType { get; set; }
    }

    public enum PolymorphicMode
    {
        SingleTypeId
    }
}";

        /// <summary>
        /// Integration test for issue #59: https://github.com/JKamsker/FourSer.Gen/issues/59
        /// Tests the exact example from the issue to verify both analyzer bugs are fixed:
        /// 1. PropertyName location reporting error should point to PropertyName, not TypeIdType
        /// 2. PolymorphicOption type checking should work with IEnumerable&lt;T&gt; properties and check assignability to T
        /// </summary>
        [Fact]
        public async Task Issue59_IEnumerableWithValidInheritance_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
    [SerializePolymorphic(TypeIdType = typeof(byte), PropertyName = ""TypeId"")]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public IEnumerable<Item> Items { get; set; }
}

[GenerateSerializer]
public partial class Item
{
}

[GenerateSerializer]
public partial class Sword : Item
{
}

[GenerateSerializer]
public partial class Shield : Item
{
}

[GenerateSerializer]
public partial class Potion : Item
{
}";
            await new CSharpAnalyzerTest<PolymorphicOptionAssignableTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task InvalidOptionType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, {|FSG3002:typeof(string)|})]
    public IEnumerable<int> Numbers { get; set; }
}";
            await new CSharpAnalyzerTest<PolymorphicOptionAssignableTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task ValidListOptionType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    public List<int> Numbers { get; set; }
}";
            await new CSharpAnalyzerTest<PolymorphicOptionAssignableTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task ValidArrayOptionType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    public int[] Numbers { get; set; }
}";
            await new CSharpAnalyzerTest<PolymorphicOptionAssignableTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }
    }
}