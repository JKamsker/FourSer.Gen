using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FourSer.Tests.OptimizationTesting;

internal sealed class GeneratedCompilationResult
{
    public GeneratedCompilationResult(
        GeneratorDriverRunResult runResult,
        CSharpCompilation initialCompilation,
        CSharpCompilation finalCompilation,
        string generatedSource)
    {
        RunResult = runResult;
        InitialCompilation = initialCompilation;
        FinalCompilation = finalCompilation;
        GeneratedSource = generatedSource;
    }

    public GeneratorDriverRunResult RunResult { get; }

    public CSharpCompilation InitialCompilation { get; }

    public CSharpCompilation FinalCompilation { get; }

    public string GeneratedSource { get; }
}
