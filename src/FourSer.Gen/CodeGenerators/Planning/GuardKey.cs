namespace FourSer.Gen.CodeGenerators.Planning;

internal readonly record struct GuardKey(
    string Kind,
    string Identity,
    string? MemberName = null);
