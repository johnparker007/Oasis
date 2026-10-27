using Avalonia.Controls;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private void InitializeLifecycleMenus(MenuItem fileMenu)
    {
        MenuAction(fileMenu, "Close project", async () => await TryCloseProjectAsync());
        MenuAction(fileMenu, "Validate Face", () => Try(() =>
        {
            var diagnostics = ValidateActiveFace();
            SetStatus(diagnostics.Count == 0
                ? $"Face validation completed for '{Faces.FaceTitle}' with no diagnostics."
                : $"Face validation: {diagnostics.Count} diagnostic(s). " + string.Join(" · ", diagnostics.Select(d => $"{d.Severity}: {d.Message}")));
        }));
        MenuAction(fileMenu, "Open source Panel2D", () => Try(OpenFaceSourcePanel));
    }

    /// <summary>Closes the current project only when every retained authored document is clean.</summary>
    public async Task CloseProjectAsync()
    {
        if (HasUnsavedChanges)
            throw new InvalidOperationException("Save or undo all open document edits before closing the project.");

        if (Preview.Backend is not null)
            await Preview.ControlAsync("stop");

        Faces.CloseAllDocuments();
        _session.CloseProject();
        Machines.CloseProject();
        Clear3DForProjectChange();
        _faceActive = false;
        _machineActive = false;
        _selectedSource = null;
        _inspected = null;
        _browseLibrary = false;
        Refresh();
        SetStatus("Project closed.");
    }

    public IReadOnlyList<FaceValidationDiagnostic> ValidateActiveFace()
    {
        if (!_faceActive || Faces.Document is not { } document)
            throw new InvalidOperationException("Open a Face asset before validating it.");

        return Faces.Validate();
    }

    public void OpenFaceSourcePanel()
    {
        if (!_faceActive || Faces.Document is not { } face)
            throw new InvalidOperationException("Open a Face asset before opening its source Panel2D.");
        if (string.IsNullOrWhiteSpace(face.SourcePanel2DDocumentPath))
            throw new InvalidOperationException("This Face does not record a source Panel2D path.");

        _session.OpenPanel(face.SourcePanel2DDocumentPath);
        _selectedSource = face.SourceFaceShapeId;
        ShowPanel();
        Refresh();
    }

    private async Task TryCloseProjectAsync()
    {
        try { await CloseProjectAsync(); }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException
            or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        { SetStatus(error.Message); }
    }
}
