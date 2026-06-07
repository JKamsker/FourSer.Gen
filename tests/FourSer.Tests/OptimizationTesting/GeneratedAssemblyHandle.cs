using System.Reflection;
using System.Runtime.Loader;

namespace FourSer.Tests.OptimizationTesting;

internal sealed class GeneratedAssemblyHandle : IDisposable
{
    private readonly AssemblyLoadContext _loadContext;
    private readonly Type _bridgeType;

    public GeneratedAssemblyHandle(AssemblyLoadContext loadContext, Assembly assembly)
    {
        _loadContext = loadContext;
        Assembly = assembly;
        RootType = assembly.GetType("FourSer.Tests.RuntimeBridge.PacketRuntimeBridge")
            ?? throw new InvalidOperationException("Packet runtime bridge was not emitted.");
        _bridgeType = RootType;
    }

    public Assembly Assembly { get; }

    public Type RootType { get; }

    public object CreateSample()
    {
        return InvokeBridge("CreateSample");
    }

    public byte[] SerializeSpan(object value)
    {
        return (byte[])InvokeBridge("SerializeSpan", value);
    }

    public byte[] SerializeStream(object value)
    {
        return (byte[])InvokeBridge("SerializeStream", value);
    }

    public byte[] RoundTripSpan(object value)
    {
        return (byte[])InvokeBridge("RoundTripSpan", value);
    }

    public byte[] RoundTripStream(object value)
    {
        return (byte[])InvokeBridge("RoundTripStream", value);
    }

    public void DeserializeSpan(byte[] data)
    {
        _ = InvokeBridge("DeserializeSpan", data);
    }

    public void DeserializeStream(byte[] data)
    {
        _ = InvokeBridge("DeserializeStream", data);
    }

    public MethodInfo GetRootMethod(string methodName, params Type[] parameterTypes)
    {
        var method = Assembly.GetTypes()
            .Select(type => type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null))
            .FirstOrDefault(candidate => candidate is not null);

        return method ?? throw new InvalidOperationException($"Could not find root method '{methodName}'.");
    }

    public void Dispose()
    {
        _loadContext.Unload();
    }

    private object InvokeBridge(string methodName, params object?[]? arguments)
    {
        var method = _bridgeType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Bridge method '{methodName}' was not found.");

        try
        {
            return method.Invoke(null, arguments)
                ?? throw new InvalidOperationException($"Bridge method '{methodName}' returned null.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
