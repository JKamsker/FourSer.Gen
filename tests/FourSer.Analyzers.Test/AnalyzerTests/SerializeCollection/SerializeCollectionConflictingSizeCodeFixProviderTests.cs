using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionConflictingSizeCodeFixProviderTests
{
    private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public bool Unlimited { get; set; }
        public int CountSize { get; set; }
        public string CountSizeReference { get; set; }
    }
}";

    [Fact]
    public async Task UnlimitedWithCountSize_RemovesConflictingArgument()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(Unlimited = true, {|FSG1001:CountSize = 10|})]
    public List<int> A { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(Unlimited = true)]
    public List<int> A { get; set; }
}";

        await new CSharpCodeFixTest<SerializeCollectionConflictingSizeAnalyzer, SerializeCollectionConflictingSizeCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
            FixedState = { Sources = { AttributesSource, fixedCode } },
        }.RunAsync();
    }

    [Fact]
    public async Task UnlimitedWithCountSizeReference_RemovesConflictingArgument()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(Unlimited = true, {|FSG1003:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public int Size { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(Unlimited = true)]
    public List<int> A { get; set; }
    public int Size { get; set; }
}";

        await new CSharpCodeFixTest<SerializeCollectionConflictingSizeAnalyzer, SerializeCollectionConflictingSizeCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
            FixedState = { Sources = { AttributesSource, fixedCode } },
        }.RunAsync();
    }

    [Fact]
    public async Task CountSizeWithCountSizeReference_RemovesConflictingArgument()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(CountSize = 10, {|FSG1002:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public int Size { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(CountSize = 10)]
    public List<int> A { get; set; }
    public int Size { get; set; }
}";

        await new CSharpCodeFixTest<SerializeCollectionConflictingSizeAnalyzer, SerializeCollectionConflictingSizeCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
            FixedState = { Sources = { AttributesSource, fixedCode } },
        }.RunAsync();
    }
}