namespace OasisEditor;

public sealed partial class ReelAuthoringSession
{
    /// <summary>Moves an open Reel document, including its history, Library binding and package guards, between same-project sessions.</summary>
    /// <param name="path">The full manifest path reported by <see cref="OpenDocuments"/>.</param>
    public void MoveDocumentTo(string path, ReelAuthoringSession destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (ReferenceEquals(this, destination)) throw new InvalidOperationException("Choose a different authoring session.");
        if (!RetainDocuments || !destination.RetainDocuments) throw new InvalidOperationException("Document retention must be enabled in both sessions.");
        if (!IsProjectCurrent || !destination.IsProjectCurrent || !SameReelProject(_projectSession.ProjectFilePath, destination._projectSession.ProjectFilePath))
            throw new InvalidOperationException("Documents can only move between sessions for the same current project.");
        var bindingRoot = CurrentProjectRoot();
        var bindingFile = Path.GetFullPath(_projectSession.ProjectFilePath!);

        var documentPath = Path.GetFullPath(path);
        if (PathComparer.Equals(destination._filePath, documentPath) || destination._retainedReels.ContainsKey(documentPath))
            throw new InvalidOperationException("The destination session already owns this document.");
        if (!PathComparer.Equals(_filePath, documentPath) && !_retainedReels.ContainsKey(documentPath))
            throw new KeyNotFoundException("Reel document is not open in this session.");

        RetainCurrentReel();
        destination.RetainCurrentReel();
        var state = _retainedReels[documentPath];
        _retainedReels.Remove(documentPath);
        destination._retainedReels.Add(documentPath, state);
        if (PathComparer.Equals(_filePath, documentPath)) ClearActiveReelForTransfer();
        destination.RestoreTransferredReel(documentPath, state, bindingRoot, bindingFile);
        Changed?.Invoke();
        destination.Changed?.Invoke();
    }

    private void RestoreTransferredReel(string path, RetainedReel state, string bindingRoot, string bindingFile)
    {
        _filePath = path; _document = state.Document; _history.Clear(); _history.AddRange(state.History); _historyIndex = state.Index;
        _savedPayload = state.Saved; ReplaceHashes(state.Hashes); _libraryRoot = state.Library;
        _boundProjectRoot = bindingRoot; _boundProjectFile = bindingFile;
    }

    private void ClearActiveReelForTransfer()
    {
        _filePath = null; _boundProjectRoot = null; _boundProjectFile = null; _libraryRoot = null; _document = null; _savedPayload = null;
        _history.Clear(); _historyIndex = -1; _packageHashes.Clear();
    }

    private static bool SameReelProject(string? left, string? right)
        => left is not null && right is not null && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
