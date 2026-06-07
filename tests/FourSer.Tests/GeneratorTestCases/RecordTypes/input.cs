using FourSer.Contracts;

namespace FourSer.Tests.Records
{
    [GenerateSerializer]
    public partial record RecordClass(int A, string B);

    [GenerateSerializer]
    public partial record struct RecordStruct(int C, string D);
}
