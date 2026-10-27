namespace OasisEditor;

public abstract partial class ThreeDAuthoringSessionBase
{
    /// <summary>Moves an open asset of the same concrete kind, preserving its in-memory state and package guards.</summary>
    /// <param name="path">The full manifest path reported by <see cref="OpenDocuments"/>.</param>
    public void MoveDocumentTo(string path, ThreeDAuthoringSessionBase destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (ReferenceEquals(this, destination)) throw new InvalidOperationException("Choose a different authoring session.");
        if (GetType() != destination.GetType()) throw new InvalidOperationException("A 3D document can only move to a session for the same asset kind.");
        if (!RetainDocuments || !destination.RetainDocuments) throw new InvalidOperationException("Document retention must be enabled in both sessions.");
        if (!IsProjectCurrent || !destination.IsProjectCurrent || !SameTransferPath(ProjectSession.ProjectFilePath, destination.ProjectSession.ProjectFilePath))
            throw new InvalidOperationException("Documents can only move between sessions for the same current project.");
        var projectRoot = ProjectRoot;
        var projectFile = Path.GetFullPath(ProjectSession.ProjectFilePath!);

        var documentPath = Path.GetFullPath(path);
        if (_retainedAssets.Comparer.Equals(documentPath, destination.ManifestPath) || destination._retainedAssets.ContainsKey(documentPath))
            throw new InvalidOperationException("The destination session already owns this document.");
        if (!_retainedAssets.ContainsKey(documentPath) && !_retainedAssets.Comparer.Equals(ManifestPath, documentPath))
            throw new KeyNotFoundException("3D asset is not open in this session.");

        RetainCurrentAsset();
        destination.RetainCurrentAsset();
        var state = _retainedAssets[documentPath];
        foreach (var payload in state.History) destination.ValidateTransferPayload(payload);
        destination.ValidateTransferPayload(state.Payload);
        _retainedAssets.Remove(documentPath);
        destination._retainedAssets.Add(documentPath, state);
        if (_retainedAssets.Comparer.Equals(ManifestPath, documentPath)) ClearActiveAssetForTransfer();
        destination.RestoreTransferredAsset(documentPath, state, projectRoot, projectFile);
        NotifyDocumentChanged();
        destination.NotifyDocumentChanged();
    }

    private void RestoreTransferredAsset(string path, RetainedAsset state, string projectRoot, string projectFile)
    {
        ManifestPath = path; PackageDirectory = Path.GetDirectoryName(path); _boundLibraryRoot = state.Library;
        _boundProjectRoot = projectRoot; _boundProjectFile = projectFile;
        History.Clear(); History.AddRange(state.History); HistoryIndex = state.Index; SavedPayload = state.Saved;
        ReplacePackageHashes(state.Hashes); SetTransferredPayload(state.Payload);
    }

    private void ClearActiveAssetForTransfer()
    {
        ManifestPath = null; PackageDirectory = null; _boundProjectRoot = null; _boundProjectFile = null; _boundLibraryRoot = null;
        SavedPayload = string.Empty; History.Clear(); HistoryIndex = -1; _packageHashes.Clear(); ResetTransferredDocument();
    }

    private static bool SameTransferPath(string? left, string? right)
        => left is not null && right is not null && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

}
