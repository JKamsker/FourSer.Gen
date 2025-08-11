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
Test.Serialize(test, buffer);
var deserialized = Test.Deserialize(buffer);

Console.WriteLine($"A: {deserialized.A}");
Console.WriteLine($"B: {deserialized.B}");
Console.WriteLine($"C: {string.Join(", ", deserialized.C)}");
