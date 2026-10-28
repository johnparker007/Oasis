namespace OasisEditor.Avalonia;

public sealed record UnsavedChangesTarget(UnsavedAssetInfo Asset, Func<Task> Save);

/// <summary>UI-independent decision and save policy. Discard authorizes a close;
/// it never deletes packages, rewrites files, or changes command histories.</summary>
public static class UnsavedChangesCoordinator
{
    public static async Task<UnsavedChangesChoice> ResolveAsync(string operation,
        IReadOnlyList<UnsavedChangesTarget> targets,
        Func<UnsavedChangesRequest, Task<UnsavedChangesChoice>> choose)
    {
        if (targets.Count == 0) return UnsavedChangesChoice.Save;
        var choice = await choose(new(operation, targets.Select(t => t.Asset).ToArray()));
        if(!Enum.IsDefined(choice))throw new InvalidOperationException("Choose Save, Discard, or Cancel.");
        if (choice == UnsavedChangesChoice.Save)
            foreach (var target in targets) await target.Save();
        return choice;
    }
}
