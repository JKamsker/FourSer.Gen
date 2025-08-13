using System.Runtime.CompilerServices;
using VerifyDiffPlex;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyDiffPlex.Initialize();
    }
}
