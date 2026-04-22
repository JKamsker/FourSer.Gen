using System.Reflection;
using FourSer.Tests.OptimizationTesting;

namespace FourSer.Tests;

internal static class OptimizationFailureTestHelpers
{
    public static Type GetPacketType(GeneratedAssemblyHandle assembly, string rootTypeName)
    {
        return assembly.Assembly.GetType(rootTypeName)
            ?? throw new InvalidOperationException($"Type '{rootTypeName}' was not emitted.");
    }

    public static void InvokeSerializeStream(Type packetType, object packet, Stream stream)
    {
        var serializeMethod = packetType.GetMethod(
            "Serialize",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [packetType, typeof(Stream)],
            modifiers: null)
            ?? throw new InvalidOperationException($"Serialize({packetType.Name}, Stream) was not found.");

        try
        {
            serializeMethod.Invoke(null, [packet, stream]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
