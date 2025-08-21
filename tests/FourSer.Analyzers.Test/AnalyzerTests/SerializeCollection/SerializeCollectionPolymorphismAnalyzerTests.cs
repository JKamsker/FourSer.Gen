using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection
{
    public class SerializeCollectionPolymorphismAnalyzerTests
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
                TestState = { Sources = { AttributesSource, testCode } },
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
                TestState = { Sources = { AttributesSource, testCode } },
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
                TestState = { Sources = { AttributesSource, testCode } },
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
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }
    }
}
