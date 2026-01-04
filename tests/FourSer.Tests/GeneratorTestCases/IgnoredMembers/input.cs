using System.Runtime.Serialization;

namespace FourSer.Tests.GeneratorTestCases.IgnoredMembers;

[GenerateSerializer]
public partial class IgnoredMembersPacket
{
    public byte Included;

    [IgnoreDataMember]
    public byte IgnoredByIgnoreDataMember;

    [Ignored]
    public byte IgnoredByIgnoredAttribute;
}
