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

    public static int InvokeGetPacketSize(Type packetType, object? packet)
    {
        var getPacketSizeMethod = packetType.GetMethod(
            "GetPacketSize",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [packetType],
            modifiers: null)
            ?? throw new InvalidOperationException($"GetPacketSize({packetType.Name}) was not found.");

        try
        {
            return (int)(getPacketSizeMethod.Invoke(null, [packet])
                ?? throw new InvalidOperationException("GetPacketSize returned null."));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    public static object InvokeDeserializeStream(Type packetType, byte[] data)
    {
        var deserializeMethod = packetType.GetMethod(
            "Deserialize",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(Stream)],
            modifiers: null)
            ?? throw new InvalidOperationException($"Deserialize(Stream) was not found for '{packetType.Name}'.");

        using var stream = new MemoryStream(data, writable: false);
        try
        {
            return deserializeMethod.Invoke(null, [stream])
                ?? throw new InvalidOperationException("Deserialize(Stream) returned null.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    public static void InvokeSerializeStream(Type packetType, object? packet, Stream stream)
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
