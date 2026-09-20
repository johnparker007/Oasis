using System.IO;
using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor.Features.MachineEditor.Services;

public sealed class ActiveMachineContext
{
    private readonly ProjectAssetReferenceResolver _resolver = new();
    private readonly ProjectAssetPathService _paths = new();

    public event EventHandler? Changed;

    public string? SelectedManifestPath { get; private set; }

    public MachineDocument? Document { get; private set; }

    public bool HasActiveMachine => Document is not null && !string.IsNullOrWhiteSpace(SelectedManifestPath);

    public (string? ManifestPath, MachineDocument? Document) GetActiveMachine() => (SelectedManifestPath, Document);

    public void SetActiveMachine(string manifestPath, MachineDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(document);

        SelectedManifestPath = Path.GetFullPath(manifestPath);
        Document = document;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearActiveMachine()
    {
        if (SelectedManifestPath is null && Document is null)
        {
            return;
        }

        SelectedManifestPath = null;
        Document = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryAutoSelectFromProject(EditorProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var manifestPaths = MachineAssetDiscovery.EnumerateMachineManifestPaths(project);
        if (manifestPaths.Count != 1)
        {
            return false;
        }

        var relativePath = manifestPaths[0];
        var document = _resolver.ResolveMachineDocument(project, relativePath);
        if (document is null)
        {
            return false;
        }

        var packagePath = _paths.ResolveProjectRelativePath(project, relativePath);
        var manifestPath = Directory.Exists(packagePath)
            ? Path.Combine(packagePath, MachineAssetDiscovery.MachineManifestFileName)
            : packagePath;

        SetActiveMachine(manifestPath, document);
        return true;
    }

    public void ApplyRuntimeToDocumentState(DocumentTabViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);
        if (Document is null)
        {
            return;
        }

        documentTab.RuntimeState.FruitMachinePlatform = Document.Runtime.Platform;
    }
}
