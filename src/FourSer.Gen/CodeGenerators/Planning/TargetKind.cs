namespace FourSer.Gen.CodeGenerators.Planning;

internal enum TargetKind
{
    None,
    Span,
    Stream,
    SequenceReader,
}

internal static class TargetKindExtensions
{
    public static bool UsesRefReadSource(this TargetKind targetKind)
    {
        return targetKind is TargetKind.Span or TargetKind.SequenceReader;
    }

    public static bool UsesRefWriteTarget(this TargetKind targetKind)
    {
        return targetKind == TargetKind.Span;
    }

    public static string GetReadSourceExpression(this TargetKind targetKind)
    {
        return targetKind switch
        {
            TargetKind.Span => "buffer",
            TargetKind.SequenceReader => "reader",
            TargetKind.Stream => "stream",
            _ => throw new NotSupportedException($"Target kind '{targetKind}' does not have a read source."),
        };
    }

    public static string GetWriteTargetExpression(this TargetKind targetKind)
    {
        return targetKind switch
        {
            TargetKind.Span => "data",
            TargetKind.Stream => "stream",
            _ => throw new NotSupportedException($"Target kind '{targetKind}' does not have a write target."),
        };
    }

    public static string GetReaderHelperName(this TargetKind targetKind)
    {
        return targetKind switch
        {
            TargetKind.Span => "global::FourSer.Gen.Helpers.RoSpanReaderHelpers",
            TargetKind.SequenceReader => "global::FourSer.Gen.Helpers.SequenceReaderHelpers",
            TargetKind.Stream => "global::FourSer.Gen.Helpers.StreamReaderHelpers",
            _ => throw new NotSupportedException($"Target kind '{targetKind}' does not have a reader helper."),
        };
    }

    public static string GetWriterHelperName(this TargetKind targetKind)
    {
        return targetKind switch
        {
            TargetKind.Span => "global::FourSer.Gen.Helpers.SpanWriterHelpers",
            TargetKind.Stream => "global::FourSer.Gen.Helpers.StreamWriterHelpers",
            _ => throw new NotSupportedException($"Target kind '{targetKind}' does not have a writer helper."),
        };
    }
}
