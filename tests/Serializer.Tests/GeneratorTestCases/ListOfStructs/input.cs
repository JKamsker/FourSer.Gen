using Serializer.Contracts;
using System.Collections.Generic;

namespace Serializer.Tests.GeneratorTestCases.ListOfStructs
{
    [GenerateSerializer]
    public partial struct MyStruct
    {
        public int A;
    }

    [GenerateSerializer]
    public partial class PacketWithListOfStructs
    {
        [SerializeCollection]
        public List<MyStruct> Structs { get; set; } = new();
    }
}
