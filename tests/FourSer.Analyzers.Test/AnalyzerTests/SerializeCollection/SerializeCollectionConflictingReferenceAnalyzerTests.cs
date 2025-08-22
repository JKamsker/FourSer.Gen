// <copyright file="SerializeCollectionConflictingReferenceAnalyzerTests.cs" company="Four serpentine">
// Copyright (c) Four serpentine. All rights reserved.
// </copyright>

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection
{
    using System.Threading.Tasks;
    using FourSer.Analyzers.SerializeCollection;
    using FourSer.Analyzers.Test.Helpers;
    using Microsoft.CodeAnalysis.CSharp.Testing;
    using Microsoft.CodeAnalysis.Testing;
    using Xunit;

    public class SerializeCollectionConflictingReferenceAnalyzerTests : AnalyzerTestBase
    {
        [Fact]
        public async Task ConflictingReferences_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(CountSizeReference = ""Ref"")]
    public List<int> A { get; set; }

    [SerializeCollection({|#0:TypeIdProperty = ""Ref""|})]
    public List<int> B { get; set; }

    public int Ref { get; set; }
}";

            var expected = new DiagnosticResult(SerializeCollectionConflictingReferenceAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Ref", "CountSizeReference", "TypeIdProperty");

            await new CSharpAnalyzerTest<SerializeCollectionConflictingReferenceAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { testCode },
                    ExpectedDiagnostics = { expected },
                },
                ReferenceAssemblies = ReferenceAssemblies,
            }.RunAsync();
        }

        [Fact]
        public async Task NoConflictingReferences_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(CountSizeReference = ""Ref1"")]
    public List<int> A { get; set; }

    [SerializeCollection(TypeIdProperty = ""Ref2"")]
    public List<int> B { get; set; }

    public int Ref1 { get; set; }
    public int Ref2 { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionConflictingReferenceAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode } },
                ReferenceAssemblies = ReferenceAssemblies,
            }.RunAsync();
        }
    }
}
