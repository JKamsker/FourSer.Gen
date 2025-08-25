using FourSer.Contracts;
using System;
using System.IO;

[assembly: DefaultSerializer(typeof(string), typeof(FourSer.Tests.GeneratorTestCases.ClassLevelOverAssembly.AssemblyStringSerializer))]

namespace FourSer.Tests.GeneratorTestCases.ClassLevelOverAssembly;

[GenerateSerializer]
[DefaultSerializer(typeof(string), typeof(ClassStringSerializer))]
public partial class Packet
{
    public string Name { get; set; }
}

public class AssemblyStringSerializer : ISerializer<string>
{
    public int GetPacketSize(string obj) => throw new NotImplementedException();
    public int Serialize(string obj, Span<byte> data) => throw new NotImplementedException();
    public void Serialize(string obj, Stream stream) => throw new NotImplementedException();
    public string Deserialize(ref ReadOnlySpan<byte> data) => throw new NotImplementedException();
    public string Deserialize(Stream stream) => throw new NotImplementedException();
}

public class ClassStringSerializer : ISerializer<string>
{
    public int GetPacketSize(string obj) => throw new NotImplementedException();
    public int Serialize(string obj, Span<byte> data) => throw new NotImplementedException();
    public void Serialize(string obj, Stream stream) => throw new NotImplementedException();
    public string Deserialize(ref ReadOnlySpan<byte> data) => throw new NotImplementedException();
    public string Deserialize(Stream stream) => throw new NotImplementedException();
}
