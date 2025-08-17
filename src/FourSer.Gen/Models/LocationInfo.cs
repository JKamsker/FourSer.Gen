namespace FourSer.Gen.Models;

public record LocationInfo(string FilePath, int Start, int End)
{
    public static readonly LocationInfo None = new(string.Empty, 0, 0);
}