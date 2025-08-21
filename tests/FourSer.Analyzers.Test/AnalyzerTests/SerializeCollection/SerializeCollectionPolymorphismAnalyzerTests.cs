using System.Collections.Immutable;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionPolymorphismAnalyzerTests
{
    [Fact]
    public async Task TypeIdTypeMismatch_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public string TypeId { get; set; }
    [SerializeCollection(TypeIdProperty = ""TypeId"", {|FSG1010:TypeIdType = typeof(int)|})]
    public List<int> A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionPolymorphismAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }

    [Fact]
    public async Task IndividualTypeIdsWithTypeIdProperty_ReportsDiagnostic()
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
        await new CSharpAnalyzerTest<SerializeCollectionPolymorphismAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }

    [Fact]
    public async Task ConflictingPolymorphicSettings_ReportsDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(TypeIdProperty = ""TypeId"")]
    [{|FSG1013:SerializePolymorphic(PropertyName = ""TypeId"")|}]
    public List<int> A { get; set; }
    public int TypeId { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionPolymorphismAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }

    [Fact]
    public async Task ValidUsage_NoDiagnostic()
    {
        var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int TypeId { get; set; }
    [SerializeCollection(TypeIdProperty = ""TypeId"", TypeIdType = typeof(int))]
    public List<int> A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionPolymorphismAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }

    [Fact]
    public async Task Property_Type_Option_Discriminator_Type_Mismatch_ReportsDiagnostic()
    {
        var testCode =
            // language=cs
            """
            using System;
            using FourSer.Contracts;
            using System.Collections.Generic;
            
            public partial class SingleTypeIdTest
            {
                public {|FSG1012:int|} AnimalType { get; set; }
            
                [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, {|FSG1012:TypeIdProperty = "AnimalType"|})]
                [PolymorphicOption((byte)1, typeof(CatBase))]
                [PolymorphicOption((byte)2, typeof(DogBase))]
                public List<AnimalBase> Animals { get; set; } = new();
            }
            
            public partial class AnimalBase
            {
                public int Age { get; set; }
            }
            
            public partial class CatBase : AnimalBase
            {
                public string Name { get; set; } = string.Empty;
            }
            
            public partial class DogBase : AnimalBase
            {
                public int Weight { get; set; }
            }
            """;
        
        await new CSharpAnalyzerTest<SerializeCollectionPolymorphismAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
    
    [Fact]
    public async Task Property_Type_Option_Discriminator_Type_Matches_NoDiagnostic()
    {
        var testCode =
            // language=cs
            """
            using System;
            using FourSer.Contracts;
            using System.Collections.Generic;

            public partial class SingleTypeIdTest
            {
                public byte AnimalType { get; set; }

                [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = "AnimalType")]
                [PolymorphicOption((byte)1, typeof(CatBase))]
                [PolymorphicOption((byte)2, typeof(DogBase))]
                public List<AnimalBase> Animals { get; set; } = new();
            }

            public partial class AnimalBase
            {
                public int Age { get; set; }
            }

            public partial class CatBase : AnimalBase
            {
                public string Name { get; set; } = string.Empty;
            }

            public partial class DogBase : AnimalBase
            {
                public int Weight { get; set; }
            }
            """;
        
        await new CSharpAnalyzerTest<SerializeCollectionPolymorphismAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}
