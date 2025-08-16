using Serializer.Package.Tests;
using System;

var packet = new MyTestPacket { Id = 1, Name = "Test" };
var buffer = new byte[1024];
var span = new Span<byte>(buffer);

// Call the static generated method
var writtenBytes = MyTestPacket.Serialize(packet, span);

Console.WriteLine($"Serialized '{packet.Name}' into {writtenBytes} bytes.");

var readOnlySpan = new ReadOnlySpan<byte>(buffer);

// Call the static generated method
var newPacket = MyTestPacket.Deserialize(readOnlySpan);

Console.WriteLine($"Deserialized packet with Id: {newPacket.Id} and Name: {newPacket.Name}");

if (packet.Id != newPacket.Id || packet.Name != newPacket.Name)
{
    Console.WriteLine("Error: Deserialized packet does not match original.");
    return 1;
}

Console.WriteLine("Serialization and deserialization successful!");
return 0;
