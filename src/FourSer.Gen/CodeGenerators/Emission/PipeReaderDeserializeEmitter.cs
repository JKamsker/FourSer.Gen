using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class PipeReaderDeserializeEmitter
{
    public static void Emit(IndentedStringBuilder sb, MethodPlan plan)
    {
        var type = plan.Facts.Type;
        var newKeyword = type.HasSerializableBaseType ? "new " : string.Empty;

        sb.WriteLineFormat("public static {0}{1} Deserialize(global::System.IO.Pipelines.PipeReader pipeReader)", newKeyword, type.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLine("if (pipeReader is null)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new System.ArgumentNullException(nameof(pipeReader));");
            }

            sb.WriteLine("while (true)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("var readResult = pipeReader.ReadAsync().GetAwaiter().GetResult();");
                sb.WriteLine("var buffer = readResult.Buffer;");
                sb.WriteLine("var reader = new global::System.Buffers.SequenceReader<byte>(buffer);");
                sb.WriteLine("try");
                using (sb.BeginBlock())
                {
                    sb.WriteLine("var obj = Deserialize(ref reader);");
                    sb.WriteLine("pipeReader.AdvanceTo(reader.Position);");
                    sb.WriteLine("return obj;");
                }

                sb.WriteLine("catch (System.IO.EndOfStreamException) when (!readResult.IsCompleted)");
                using (sb.BeginBlock())
                {
                    sb.WriteLine("pipeReader.AdvanceTo(buffer.Start, buffer.End);");
                }

                sb.WriteLine("catch (System.IO.EndOfStreamException)");
                using (sb.BeginBlock())
                {
                    sb.WriteLine("pipeReader.AdvanceTo(buffer.Start, buffer.End);");
                    sb.WriteLine("throw;");
                }
            }
        }
    }
}
