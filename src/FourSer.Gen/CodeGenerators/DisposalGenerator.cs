using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates IDisposable implementation for generated types.
/// </summary>
public static class DisposalGenerator
{
    public static bool ShouldGenerateDispose(TypeToGenerate typeToGenerate)
    {
        // Only generate IDisposable when:
        // - the type doesn't already implement it elsewhere, and
        // - it is required because we own IMemoryOwner<T> instances (directly or via nested generated types).
        if (typeToGenerate.ImplementsIDisposable || typeToGenerate.HasDisposeMethod)
        {
            return false;
        }

        return typeToGenerate.RequiresDisposal;
    }

    public static void GenerateDispose(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        if (!ShouldGenerateDispose(typeToGenerate))
        {
            return;
        }

        sb.WriteLine();
        sb.WriteLine("public void Dispose()");
        using var _ = sb.BeginBlock();

        foreach (var member in typeToGenerate.Members)
        {
            if (member.IsMemoryOwner)
            {
                sb.WriteLineFormat("this.{0}?.Dispose();", member.Name);
                continue;
            }

            if (member.HasGenerateSerializerAttribute)
            {
                var disposableVar = $"disposable_{member.Name.ToCamelCase()}";
                sb.WriteLineFormat("if (this.{0} is System.IDisposable {1})", member.Name, disposableVar);
                using (sb.BeginBlock())
                {
                    sb.WriteLineFormat("{0}.Dispose();", disposableVar);
                }
                continue;
            }

            // Handle collections with disposable elements
            if (member.IsCollection && member.CollectionTypeInfo?.HasElementGenerateSerializerAttribute == true)
            {
                var itemVar = $"item_{member.Name.ToCamelCase()}";
                sb.WriteLineFormat("if (this.{0} != null)", member.Name);
                using (sb.BeginBlock())
                {
                    sb.WriteLineFormat("foreach (var {0} in this.{1})", itemVar, member.Name);
                    using (sb.BeginBlock())
                    {
                        var disposableVar = $"disposable_{itemVar}";
                        sb.WriteLineFormat("if ({0} is System.IDisposable {1})", itemVar, disposableVar);
                        using (sb.BeginBlock())
                        {
                            sb.WriteLineFormat("{0}.Dispose();", disposableVar);
                        }
                    }
                }
            }
        }
    }
}
