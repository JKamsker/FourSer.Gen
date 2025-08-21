using FourSer.Analyzers.PolymorphicOption;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption
{
    public class PolymorphicOptionIdCodeFixProviderTests
    {
        private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(int id, Type type) { }
    }
}";

        [Fact]
        public async Task DuplicateIds_RemovesDuplicate()
        {
            var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    [PolymorphicOption({|FSG3000:10|}, typeof(string))]
    public object A { get; set; }
}";

            var fixedCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    public object A { get; set; }
}";

            await new CSharpCodeFixTest<PolymorphicOptionIdAnalyzer, PolymorphicOptionIdCodeFixProvider, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
                FixedState = { Sources = { AttributesSource, fixedCode } },
            }.RunAsync();
        }
    }
}
