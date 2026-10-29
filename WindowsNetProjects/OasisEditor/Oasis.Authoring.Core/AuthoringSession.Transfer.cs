namespace OasisEditor;

public sealed partial class AuthoringSession
{
    /// <summary>Moves an open Panel2D document, including its in-memory history, to another retained session for the same project.</summary>
    /// <param name="path">The full manifest path reported by <see cref="OpenDocuments"/>.</param>
    public void MoveDocumentTo(string path, AuthoringSession destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (ReferenceEquals(this, destination)) throw new InvalidOperationException("Choose a different authoring session.");
        if (!RetainDocuments || !destination.RetainDocuments) throw new InvalidOperationException("Document retention must be enabled in both sessions.");
        EnsureSameProject(_projectFilePath, destination._projectFilePath);

        var documentPath = Path.GetFullPath(path);
        if (_panelStates.Comparer.Equals(documentPath, destination._panelPath)
            || destination._panelStates.ContainsKey(documentPath))
            throw new InvalidOperationException("The destination session already owns this document.");
        if (!_panelStates.ContainsKey(documentPath) && !_panelStates.Comparer.Equals(_panelPath, documentPath))
            throw new KeyNotFoundException("Panel2D document is not open in this session.");
        var hasImportedInputs = _importedInputDefinitions.TryGetValue(documentPath, out var importedInputs);
        var hasFmlImports = _createdFmlImports.TryGetValue(documentPath, out var fmlImports);

        StorePanelState();
        destination.StorePanelState();
        var state = _panelStates[documentPath];

        _panelStates.Remove(documentPath);
        destination._panelStates.Add(documentPath, state);
        if (hasImportedInputs)
        {
            _importedInputDefinitions.Remove(documentPath);
            destination._importedInputDefinitions[documentPath] = importedInputs!;
        }
        if (hasFmlImports)
        { _createdFmlImports.Remove(documentPath); destination._createdFmlImports[documentPath] = fmlImports!; }
        destination._panelClipboardState = _panelClipboardState;
        if (_panelStates.Comparer.Equals(_panelPath, documentPath)) ClearActivePanelForTransfer();
        destination._panelPath = documentPath;
        destination.RestorePanelState(state);
        Changed?.Invoke();
        destination.Changed?.Invoke();
    }

    private void ClearActivePanelForTransfer()
    {
        _document = null; _panelPath = null; _diskHash = null; _savedContent = string.Empty;
        _selectedId = null; _selectedIds = []; ClearHistory();
    }

    private static void EnsureSameProject(string? source, string? destination)
    {
        if (source is null || destination is null || !SameCanonicalPath(source, destination))
            throw new InvalidOperationException("Documents can only move between sessions for the same project.");
    }

    private static bool SameCanonicalPath(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

public sealed partial class FaceAuthoringSession
{
    /// <summary>Moves an open Face document, including its saved package guards and selection, between sessions for the same project.</summary>
    /// <param name="path">The full manifest path reported by <see cref="OpenDocuments"/>.</param>
    public void MoveDocumentTo(string path, FaceAuthoringSession destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (ReferenceEquals(this, destination)) throw new InvalidOperationException("Choose a different authoring session.");
        if (!RetainDocuments || !destination.RetainDocuments) throw new InvalidOperationException("Document retention must be enabled in both sessions.");
        EnsureSameFaceProject(_project.ProjectFilePath, destination._project.ProjectFilePath);

        var documentPath = Path.GetFullPath(path);
        if (SamePath(destination._facePath, documentPath) || destination._retainedFaces.ContainsKey(documentPath))
            throw new InvalidOperationException("The destination session already owns this document.");
        if (!SamePath(_facePath, documentPath) && !_retainedFaces.ContainsKey(documentPath))
            throw new KeyNotFoundException("Face document is not open in this session.");
        var sourceProject = SamePath(_facePath, documentPath) ? _projectFileAtOpen : _retainedFaces[documentPath].Project;
        if (!SamePath(sourceProject, _project.ProjectFilePath)) throw new InvalidOperationException("The Face document is not bound to the current project.");

        RetainCurrentFace();
        destination.RetainCurrentFace();
        var state = _retainedFaces[documentPath];

        _retainedFaces.Remove(documentPath);
        destination._retainedFaces.Add(documentPath, state);
        if (SamePath(_facePath, documentPath)) ClearDocument();
        destination.RestoreTransferredFace(documentPath, state);
        Changed?.Invoke();
        destination.Changed?.Invoke();
    }

    private void RestoreTransferredFace(string path, RetainedFace state)
    {
        _facePath = path; _projectFileAtOpen = state.Project; _projectRootAtOpen = state.Root; _packageDirectory = state.Package;
        _history.Clear(); _history.AddRange(state.History); _historyIndex = state.Index; _savedPayload = state.Saved;
        _savedPackageFiles = new(state.Files, state.Files.Comparer); _savedGeneratedFiles = new(state.Generated, state.Generated.Comparer); _selectedId = state.Selection;
    }

    private static void EnsureSameFaceProject(string? source, string? destination)
    {
        if (source is null || destination is null || !SamePath(source, destination))
            throw new InvalidOperationException("Documents can only move between sessions for the same project.");
    }
}

public sealed partial class MachineAuthoringSession
{
    /// <summary>Moves an open Machine document, including behavior source and history, between sessions for the same project.</summary>
    /// <param name="path">The full manifest path reported by <see cref="OpenDocuments"/>.</param>
    public void MoveDocumentTo(string path, MachineAuthoringSession destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (ReferenceEquals(this, destination)) throw new InvalidOperationException("Choose a different authoring session.");
        if (!RetainDocuments || !destination.RetainDocuments) throw new InvalidOperationException("Document retention must be enabled in both sessions.");
        if (!IsProjectCurrent || !destination.IsProjectCurrent || !SamePath(projectFilePath, destination.projectFilePath))
            throw new InvalidOperationException("Documents can only move between sessions for the same current project.");

        var documentPath = Path.GetFullPath(path);
        if (SamePath(destination.machinePath, documentPath) || destination.retainedMachines.ContainsKey(documentPath))
            throw new InvalidOperationException("The destination session already owns this document.");
        if (!SamePath(machinePath, documentPath) && !retainedMachines.ContainsKey(documentPath))
            throw new KeyNotFoundException("Machine document is not open in this session.");

        RetainCurrentMachine();
        destination.RetainCurrentMachine();
        var state = retainedMachines[documentPath];

        retainedMachines.Remove(documentPath);
        destination.retainedMachines.Add(documentPath, state);
        if (SamePath(machinePath, documentPath)) ClearActiveMachineForTransfer();
        destination.RestoreTransferredMachine(documentPath, state);
        Changed?.Invoke();
        destination.Changed?.Invoke();
    }

    private void RestoreTransferredMachine(string path, RetainedMachine state)
    {
        machinePath = path; document = CloneDocument(state.Document); history.Clear(); history.AddRange(state.History.Select(CloneDocument));
        behaviorHistory.Clear(); behaviorHistory.AddRange(state.BehaviorHistory); historyIndex = state.Index; diskHash = state.DiskHash; savedPayload = state.Saved;
        behaviorSource = state.Behavior; savedBehaviorSource = state.SavedBehavior; behaviorDiskHash = state.BehaviorHash;
    }

    private void ClearActiveMachineForTransfer()
    {
        machinePath = null; document = null; diskHash = null; savedPayload = string.Empty;
        behaviorSource = savedBehaviorSource = string.Empty; behaviorDiskHash = null; ResetHistory();
    }
}
