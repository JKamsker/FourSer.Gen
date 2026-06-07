using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class BufferWriterSerializeEmitter
{
    public static void Emit(IndentedStringBuilder sb, MethodPlan plan)
    {
        var type = plan.Facts.Type;
        sb.WriteLineFormat("public static void Serialize({0} obj, global::System.Buffers.IBufferWriter<byte> writer)", type.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLine("if (writer is null)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new System.ArgumentNullException(nameof(writer));");
            }

            sb.WriteLine("var packetSize = GetPacketSize(obj);");
            sb.WriteLine("var data = writer.GetSpan(packetSize);");
            sb.WriteLine("Serialize(obj, data.Slice(0, packetSize));");
            sb.WriteLine("writer.Advance(packetSize);");
        }
    }
}
