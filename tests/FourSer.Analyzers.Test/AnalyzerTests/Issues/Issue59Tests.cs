using System.Collections.Immutable;
using FourSer.Analyzers.PolymorphicOption;
using FourSer.Analyzers.SerializePolymorphic;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.Issues;

public class Issue59Tests
{
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
                TestState = { Sources = { testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
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
                    Sources = { testCode },
                    ExpectedDiagnostics = { expected }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
            }.RunAsync();
        }
    }
}
