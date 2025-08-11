using Serializer.Consumer;
using System;
using System.Collections.Generic;

var test = new Test
{
    A = 1,
    B = "Hello",
    C = new List<int> { 1, 2, 3 }
};

var size = Test.GetPacketSize(test);
var buffer = new byte[size];
var span = new Span<byte>(buffer);
Test.Serialize(test, span);
var readOnlySpan = new ReadOnlySpan<byte>(buffer);
var deserialized = Test.Deserialize(readOnlySpan);

Console.WriteLine($"A: {deserialized.A}");
Console.WriteLine($"B: {deserialized.B}");
Console.WriteLine($"C: {string.Join(", ", deserialized.C)}");
