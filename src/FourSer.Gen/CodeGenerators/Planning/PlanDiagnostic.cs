using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen.CodeGenerators.Planning;

internal readonly record struct PlanDiagnostic(
    OptimizationDiagnosticKind Kind,
    string Message,
    LocationInfo? Location = null)
{
    public Diagnostic ToDiagnostic()
    {
        var descriptor = OptimizationDiagnostics.GetDescriptor(Kind);
        var diagnosticLocation = Location is { } location
            ? Microsoft.CodeAnalysis.Location.Create(location.FilePath, location.SourceSpan, location.LineSpan)
            : Microsoft.CodeAnalysis.Location.None;

        return Diagnostic.Create(descriptor, diagnosticLocation, Message);
    }
}
