using System.Text;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates GetPacketSize method implementations
/// </summary>
public static class PacketSizeGenerator
{
    public static void GenerateGetPacketSize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.AppendLine($"    public static int GetPacketSize({typeToGenerate.Name} obj)");
        sb.AppendLine("    {");
        sb.AppendLine("        var size = 0;");

        foreach (var member in typeToGenerate.Members)
        {
            CodeGenerationHelper.GenerateMemberSizeCalculation(sb, member);
        }

        sb.AppendLine("        return size;");
        sb.AppendLine("    }");
}
}
