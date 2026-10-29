using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Dock.Model.Core;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private bool _closeWorkflowActive, _editorCloseApproved;
    private Window? _closeDialogOwner;
    private HashSet<string>? _approvedDiscardAssets;
    private Dictionary<string,(NativeAssetEditor Editor,long Revision)>? _approvedDiscardRevisions;
    private readonly HashSet<string> _discardCloseIds = [];
    private readonly HashSet<string> _queuedCloseIds = [];
    private bool _closeQueueScheduled;
    public bool IsCloseWorkflowActive => DocumentHost._closeWorkflowActive;
    public UnsavedChangesRequest? PendingUnsavedChanges { get; private set; }
    public Window? UnsavedChangesWindow { get; private set; }
    public string? CloseWorkflowError { get; private set; }
    public event Action<Window>? UnsavedChangesDialogOpened;
    public event Action<Window>? CloseErrorDialogOpened;

    private IReadOnlyList<UnsavedChangesTarget> ModifiedNativeAssets(HashSet<string>? ids = null)
    {
        foreach (var entry in _nativeDocuments.Values.Where(d => ids is null || ids.Contains(d.Asset.Id)))
            if (!entry.Editor.CommitInspectorText()) throw new InvalidOperationException("Correct invalid Inspector fields before closing.");
        return _nativeDocuments.Values.Where(d => (ids is null || ids.Contains(d.Asset.Id)) && d.Asset.IsDirty)
            .Select(d => new UnsavedChangesTarget(new(d.Asset.Id, d.Editor.ActiveTitle, d.Asset.Kind,
                (d.Asset.LibraryRoot is null ? "Project: " : "Library: ") + Path.GetRelativePath(
                    d.Asset.LibraryRoot ?? _session.RequireProjectDirectory(), d.Asset.Path), false),
                () => { SaveNativeAsset(d); return Task.CompletedTask; })).ToArray();
    }

    private void SaveNativeAsset(NativeAssetEditor entry)
    {
        if (!entry.Editor.CommitInspectorText()) throw new InvalidOperationException("Correct invalid Inspector fields before saving.");
        try { entry.Asset.Save(); }
        catch(Exception error) when(error is not (OutOfMemoryException or OperationCanceledException))
        { throw new IOException($"Could not save {entry.Asset.Kind} '{entry.Editor.ActiveTitle}': {error.Message}",error); }
    }

    private async Task<UnsavedChangesChoice> PromptUnsavedChangesAsync(UnsavedChangesRequest request)
    {
        if ((_closeDialogOwner ?? TopLevel.GetTopLevel(this)) is not Window owner) return UnsavedChangesChoice.Cancel;
        PendingUnsavedChanges = request;
        try
        {
            return await UnsavedChangesDialog.ShowAsync(owner, request, dialog =>
            { UnsavedChangesWindow = dialog; UnsavedChangesDialogOpened?.Invoke(dialog); });
        }
        finally { PendingUnsavedChanges = null; UnsavedChangesWindow = null; }
    }

    private async Task ShowCloseErrorAsync(Exception error, bool mentionUnsavedDocuments = true)
    {
        CloseWorkflowError = error.Message;
        SetStatus(error.Message);
        if ((_closeDialogOwner ?? TopLevel.GetTopLevel(this)) is not Window owner) return;
        var dialog = new Window { Name = "UnsavedChangesError", Title = "Could not complete the operation",
            Width = 520, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        content.Children.Add(new TextBlock { Text = error.Message + (mentionUnsavedDocuments ? "\n\nYour remaining unsaved documents are still open." : ""), TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        var ok = new Button { Name = "UnsavedChangesErrorOK", Content = "OK", IsDefault = true, IsCancel = true };
        ok.Click += (_, _) => dialog.Close(); content.Children.Add(ok); dialog.Content = content;
        UnsavedChangesWindow = dialog;
        try { var task = dialog.ShowDialog(owner); CloseErrorDialogOpened?.Invoke(dialog); await task; }
        finally { UnsavedChangesWindow = null; }
    }

    private async Task<bool> ConfirmCloseAsync(string operation, HashSet<string>? ids,
        Func<UnsavedChangesChoice, Task> commit)
    {
        if (_closeWorkflowActive || UnsavedChangesWindow is not null) return false;
        _closeWorkflowActive = true; CloseWorkflowError = null;
        try
        {
            await SynchronizeAssetDocumentsAsync();
            EnsureAssetWindowsIdle();
            _closeDialogOwner = ids is {Count:1} && _nativeDocuments.TryGetValue(ids.First(),out var selected)
                ? NativeWindowFor(selected) : null;
            if(ids is null && HasLocalUnsavedChanges)throw new InvalidOperationException("Wait for all asset edits to finish synchronizing before closing.");
            var targets = ModifiedNativeAssets(ids);
            var approvedIds = targets.Select(t => t.Asset.Id).ToHashSet(StringComparer.Ordinal);
            var revisions = _nativeDocuments.Values.Where(d=>approvedIds.Contains(d.Asset.Id))
                .ToDictionary(d=>d.Asset.Id,d=>(Editor:d,Revision:d.Revision));
            var choice = await UnsavedChangesCoordinator.ResolveAsync(operation, targets, PromptUnsavedChangesAsync);
            if (choice == UnsavedChangesChoice.Cancel) return false;
            await SynchronizeAssetDocumentsAsync();
            if(choice == UnsavedChangesChoice.Discard)ValidateDiscardRevisions(revisions);
            if (ModifiedNativeAssets(ids).Any(t => !approvedIds.Contains(t.Asset.Id)) ||
                (choice == UnsavedChangesChoice.Save && ModifiedNativeAssets(ids).Count != 0))
                throw new InvalidOperationException("The modified assets changed while confirming. Please try again.");
            _approvedDiscardAssets = choice == UnsavedChangesChoice.Discard ? approvedIds : null;
            _approvedDiscardRevisions = choice == UnsavedChangesChoice.Discard ? revisions : null;
            var cleanupProject = _session.ProjectFilePath; var cleanupLease = _cleanupLease; var cleanupLibraries = CleanupProtectedRoots();
            var releasedImports = choice == UnsavedChangesChoice.Discard
                ? _nativeDocuments.Values.Where(d => approvedIds.Contains(d.Asset.Id))
                    .SelectMany(d => d.Asset.Sessions.Project.CaptureCreatedFmlImportDirectories()).Distinct().ToArray() : [];
            await commit(choice);
            if (choice == UnsavedChangesChoice.Discard && cleanupProject is not null)
                CleanupFmlAfterDiscard(cleanupProject, cleanupLease, cleanupLibraries, releasedImports);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception error) when (error is not OutOfMemoryException)
        { await ShowCloseErrorAsync(error); return false; }
        finally { _closeDialogOwner = null; _approvedDiscardAssets = null; _approvedDiscardRevisions = null; _closeWorkflowActive = false; RefreshNativeDocumentTitles(); }
    }

    public async Task<bool> RequestCloseDocumentAsync(string kind, string path)
    {
        if (_documentOwner is {} host) return await host.RequestCloseDocumentAsync(kind, path);
        await SynchronizeAssetDocumentsAsync();
        var entry = FindNativeEditor(kind, path);
        return await RequestCloseNativeAssetsAsync([entry.Asset.Id]);
    }

    private Task<bool> RequestCloseNativeAssetsAsync(IEnumerable<string> documentIds)
    {
        var ids = documentIds.ToHashSet(StringComparer.Ordinal);
        if(_maximisedTab is {} maximised && ids.Contains(maximised.Pane.Id))
        { SetStatus("Restore the tab before closing it."); return Task.FromResult(false); }
        return ConfirmCloseAsync(ids.Count == 1 ? "closing this asset" : "closing these assets", ids, choice =>
        {
            if (choice == UnsavedChangesChoice.Discard) _discardCloseIds.UnionWith(ids);
            try
            {
                foreach (var id in ids)
                    if (_nativeDocuments.TryGetValue(id, out var entry))
                    {
                        _factory.CloseDockable(entry.Document);
                        if(_nativeDocuments.ContainsKey(id))throw new InvalidOperationException("The tab could not be closed. Restore it and try again.");
                    }
            }
            finally { _discardCloseIds.ExceptWith(ids); }
            return Task.CompletedTask;
        });
    }

    private void QueueNativeCloseRequest(string id)
    {
        if (_closeWorkflowActive) return;
        _queuedCloseIds.Add(id);
        if (_closeQueueScheduled) return;
        _closeQueueScheduled = true;
        Dispatcher.UIThread.Post(async () =>
        {
            _closeQueueScheduled = false;
            var ids = _queuedCloseIds.ToArray(); _queuedCloseIds.Clear();
            await RequestCloseNativeAssetsAsync(ids);
        });
    }

    public Task<bool> RequestCloseProjectAsync()
        => _documentOwner is {} host ? host.RequestCloseProjectAsync()
            : ConfirmCloseAsync("closing the project", null, choice => CloseProjectCoreAsync(choice == UnsavedChangesChoice.Discard));

    public async Task<bool> RequestOpenProjectAsync(string path)
    {
        if (_documentOwner is {} host) return await host.RequestOpenProjectAsync(path);
        var probe = new AuthoringSession(_session.WorkspacePath); probe.OpenProject(path);
        if (SameScenePath(_session.ProjectFilePath, probe.ProjectFilePath!)) return true;
        return await ConfirmCloseAsync("opening another project", null, choice =>
        { OpenProjectCore(path, choice == UnsavedChangesChoice.Discard); return Task.CompletedTask; });
    }

    public Task<bool> RequestCreateProjectAsync(string name)
        => _documentOwner is {} host ? host.RequestCreateProjectAsync(name)
            : ConfirmCloseAsync("creating another project", null, choice =>
            { CreateProjectCore(name, choice == UnsavedChangesChoice.Discard); return Task.CompletedTask; });

    public Task<bool> RequestEditorExitAsync()
        => _documentOwner is {} host ? host.RequestEditorExitAsync()
            : ConfirmCloseAsync("exiting Oasis Editor", null, _ =>
            {
                // Preserve layout/reopen paths before disposal, even on Discard.
                // Preserve the existing best-effort layout autosave behaviour.
                // Asset save failures already abort before reaching this commit.
                if (CanAutoSaveWorkspace && !IsProjectLauncherVisible) Try(SaveWorkspace);
                DisposeNativeDocuments();
                _editorCloseApproved = true;
                if (TopLevel.GetTopLevel(this) is Window owner) owner.Close();
                return Task.CompletedTask;
            });

    private async Task SaveAllFromUiAsync()
    {
        try { await DocumentHost.SaveAllDocumentsAsync(); }
        catch(Exception error) when(error is not OutOfMemoryException) { await DocumentHost.ShowCloseErrorAsync(error); }
    }

    public void AttachEditorCloseHandler(Window window)
    {
        window.Closing += (_, e) =>
        {
            if (_editorCloseApproved) return;
            e.Cancel = true;
            if (_closeWorkflowActive) return;
            Dispatcher.UIThread.Post(async () => await RequestEditorExitAsync());
        };
    }

    private void PreserveFloatingDocumentsOnHostClose(Window host,WindowClosingEventArgs e)
    {
        if(_editorCloseApproved || _nativeDisposing || _nativeLayoutOperation || _disposed)return;
        if(e.CloseReason is WindowCloseReason.OwnerWindowClosing or WindowCloseReason.ApplicationShutdown)
        {
            // Avalonia asks owned windows to close before raising the main
            // window's Closing event. Treat that cascade as one Editor exit.
            e.Cancel=true;
            if(!_closeWorkflowActive)Dispatcher.UIThread.Post(async()=>await RequestEditorExitAsync(),DispatcherPriority.Background);
            return;
        }
        var entries=_nativeDocuments.Values.Where(entry=>ReferenceEquals(NativeWindowFor(entry),host)).ToArray();
        if(entries.Length==0)return;
        if(_closeWorkflowActive){e.Cancel=true;return;}
        // Closing a floating host returns its documents to the workspace. Stop
        // Dock's destructive OnClose before it can ask to close the asset tabs.
        e.Cancel=true;
        Dispatcher.UIThread.Post(()=>Try(()=>
        {
            foreach(var entry in entries)
                if(_nativeDocuments.TryGetValue(entry.Asset.Id,out var current) && ReferenceEquals(current,entry))RedockNativeEditor(entry);
        }));
    }

    private void ValidateProjectCloseDecision(bool discardApproved)
    {
        if(discardApproved && _approvedDiscardRevisions is {} revisions)ValidateDiscardRevisions(revisions);
        if(!discardApproved && HasUnsavedChanges)
            throw new InvalidOperationException("Assets changed while closing. Please save or confirm again.");
        if(discardApproved && (HasLocalUnsavedChanges || ModifiedNativeAssets().Any(t=>_approvedDiscardAssets?.Contains(t.Asset.Id)!=true)))
            throw new InvalidOperationException("Additional assets changed while closing. Please confirm again.");
    }

    private void ValidateDiscardRevisions(Dictionary<string,(NativeAssetEditor Editor,long Revision)> revisions)
    {
        foreach(var pair in revisions)
            if(_nativeDocuments.TryGetValue(pair.Key,out var current) &&
                (!ReferenceEquals(current,pair.Value.Editor) || current.Revision!=pair.Value.Revision))
                throw new InvalidOperationException($"'{current.Editor.ActiveTitle}' changed while confirming. Please review its changes and try again.");
    }

    private bool CanDiscardNativeAsset(NativeAssetEditor entry)
        => _discardCloseIds.Contains(entry.Asset.Id) && _approvedDiscardRevisions is {} revisions &&
            revisions.TryGetValue(entry.Asset.Id,out var saved) && ReferenceEquals(saved.Editor,entry) && saved.Revision==entry.Revision;

    public void RespondToUnsavedChanges(UnsavedChangesChoice choice)
    {
        if (!Program.IsHeadless) throw new InvalidOperationException("Use the visible dialog in the desktop Editor.");
        var name = UnsavedChangesWindow?.Name == "UnsavedChangesError" ? "UnsavedChangesErrorOK" : choice switch
        { UnsavedChangesChoice.Save => "UnsavedChangesSave", UnsavedChangesChoice.Discard => "UnsavedChangesDiscard", _ => "UnsavedChangesCancel" };
        var button = UnsavedChangesWindow?.GetLogicalDescendants().OfType<Button>().SingleOrDefault(b=>b.Name==name)
            ?? throw new InvalidOperationException("There is no pending unsaved-changes dialog.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
