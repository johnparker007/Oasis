namespace OasisEditor.Avalonia.Tests;

/// <summary>Initializes Avalonia's process-wide headless platform exactly once across UI tests.</summary>
internal static class HeadlessTestHost
{
    private static readonly object Gate = new();
    private static bool initialized;

    public static void Initialize()
    {
        lock (Gate)
        {
            if (initialized) return;
            global::OasisEditor.Avalonia.Program.BuildHeadless().SetupWithoutStarting();
            initialized = true;
        }
    }
}
