using DiffEngine;

public static class VerifyUiSettings
{
    // Flip this to true when you want Verify to open the configured diff tool for snapshot failures.
    public static bool EnableDiffUi { get; } = false;

    public static void Initialize()
    {
        DiffRunner.Disabled = !EnableDiffUi;

        if (!EnableDiffUi)
        {
            return;
        }

        VerifyDiffPlex.Initialize();
    }
}
