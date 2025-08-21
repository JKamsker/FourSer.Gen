using FourSer.Analyzers.PolymorphicOption;
using FourSer.Analyzers.SerializePolymorphic;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.Issues;

public class Issue59Tests
{
    private const string AttributesSource =
        // language=csharp
        """

        using System;
        using System.Collections.Generic;

        namespace FourSer.Contracts
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
            public class GenerateSerializerAttribute : Attribute
            {
            }

            public enum PolymorphicMode
            {
                None,
                SingleTypeId,
                IndividualTypeIds
            }

            [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
            public class SerializeCollectionAttribute : Attribute
            {
                public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;
                public Type? TypeIdType { get; set; }
                public string? TypeIdProperty { get; set; }
            }

            [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
            public class SerializePolymorphicAttribute : Attribute
            {
                public string? PropertyName { get; set; }
                public Type? TypeIdType { get; set; }

                public SerializePolymorphicAttribute(string? propertyName = null)
                {
                    PropertyName = propertyName;
                }
            }

            [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
            public class PolymorphicOptionAttribute : Attribute
            {
                public object Id { get; }
                public Type Type { get; }

                public PolymorphicOptionAttribute(int id, Type type) { Id = id; Type = type; }
                public PolymorphicOptionAttribute(byte id, Type type) { Id = id; Type = type; }
                public PolymorphicOptionAttribute(ushort id, Type type) { Id = id; Type = type; }
                public PolymorphicOptionAttribute(long id, Type type) { Id = id; Type = type; }
                public PolymorphicOptionAttribute(object id, Type type) { Id = id; Type = type; }
            }
        }
        """;

    public class PolymorphicOptionAssignableTypeAnalyzerTests
    {
        /// <summary>
        /// This integration test verifies fixes for issue #59.
        /// It ensures that polymorphic collection analysis works correctly.
        /// See: https://github.com/FourSer/FourSer/issues/59
        /// </summary>
        [Fact]
        public async Task Issue59_PolymorphicCollectionAnalysis_NoDiagnostics()
        {
            var testCode =
                // language=csharp
                """
                using FourSer.Contracts;
                using System.Collections.Generic;

                [GenerateSerializer]
                public partial class Inventory
                {
                    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
                    [SerializePolymorphic(TypeIdType = typeof(byte), PropertyName = "TypeId")]
                    [PolymorphicOption((byte)10, typeof(Sword))]
                    [PolymorphicOption((byte)20, typeof(Shield))]
                    [PolymorphicOption((byte)30, typeof(Potion))]
                    public IEnumerable<Item> Items { get; set; } = new List<Item>();

                    public byte TypeId { get; set; }
                }

                [GenerateSerializer]
                public partial class Item { }

                [GenerateSerializer]
                public partial class Sword : Item { }

                [GenerateSerializer]
                public partial class Shield : Item { }

                [GenerateSerializer]
                public partial class Potion : Item { }
                """;

            await new CSharpAnalyzerTest<PolymorphicOptionAssignableTypeAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources =
                    {
                        AttributesSource,
                        testCode
                    }
                },
            }.RunAsync();
        }
    }

    public class SerializePolymorphicPropertyNameAnalyzerTests
    {
        /// <summary>
        /// This integration test verifies fixes for issue #59.
        /// It ensures that the diagnostic for an invalid property name is reported on the correct location.
        /// See: https://github.com/FourSer/FourSer/issues/59
        /// </summary>
        [Fact]
        public async Task Issue59_PolymorphicCollectionAnalysis_WithInvalidPropertyName()
        {
            var testCode =
                // language=csharp
                """
                using FourSer.Contracts;
                using System.Collections.Generic;

                [GenerateSerializer]
                public partial class Inventory
                {
                    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
                    [SerializePolymorphic(TypeIdType = typeof(byte), PropertyName = {|#0:"InvalidTypeId"|})]
                    [PolymorphicOption((byte)10, typeof(Sword))]
                    [PolymorphicOption((byte)20, typeof(Shield))]
                    [PolymorphicOption((byte)30, typeof(Potion))]
                    public IEnumerable<Item> Items { get; set; } = new List<Item>();

                    public byte TypeId { get; set; }
                }

                [GenerateSerializer]
                public partial class Item { }

                [GenerateSerializer]
                public partial class Sword : Item { }

                [GenerateSerializer]
                public partial class Shield : Item { }

                [GenerateSerializer]
                public partial class Potion : Item { }
                """;

            var expected = new DiagnosticResult(SerializePolymorphicPropertyNameAnalyzer.NotFoundRule)
                .WithLocation(0)
                .WithArguments("InvalidTypeId");

            await new CSharpAnalyzerTest<SerializePolymorphicPropertyNameAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources =
                    {
                        AttributesSource,
                        testCode
                    },
                    ExpectedDiagnostics = { expected }
                },
            }.RunAsync();
        }
    }
}
