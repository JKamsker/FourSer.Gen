using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Testing;

namespace FourSer.Analyzers.Test.Helpers;

public abstract class AnalyzerTestBase
{
    protected static ReferenceAssemblies ReferenceAssemblies { get; } =
        ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")));
}
