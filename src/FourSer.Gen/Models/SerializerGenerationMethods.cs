namespace FourSer.Gen.Models;

[Flags]
public enum SerializerGenerationMethods
{
    None = 0,
    Stream = 1,
    BufferWriter = 2,
    SequenceReader = 4,
    PipeWriter = 8,
    PipeReader = 16,
}
