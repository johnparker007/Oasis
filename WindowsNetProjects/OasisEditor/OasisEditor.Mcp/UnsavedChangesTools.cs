using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    private readonly object _closeFlowSync = new();
    private Task<bool>? CloseFlowTask { get => capture.CloseWorkflowTask; set => capture.CloseWorkflowTask = value; }

    [McpServerTool(Name = "request_close"), Description("Start a real Editor close or project replacement workflow. Returns immediately while the actual unsaved-changes dialog remains open for inspection and response.")]
    public async Task<object> RequestClose(string operation, CancellationToken cancellationToken, string? kind = null, string? path = null, string? name = null)
    {
        if (operation is not ("document" or "project" or "open_project" or "create_project" or "editor"))
            throw new ArgumentException("Operation must be document, project, open_project, create_project, or editor.", nameof(operation));
        if (operation == "document" && (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(path)))
            throw new ArgumentException("Closing a document requires kind and path.");
        if (operation == "open_project" && string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Opening a project requires path.");
        if (operation == "create_project" && string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Creating a project requires name.");

        return await ui.Invoke(() =>
        {
            lock (_closeFlowSync)
            {
                if (CloseFlowTask is { IsCompleted: false } || capture.View.IsCloseWorkflowActive)
                    throw new InvalidOperationException("A close workflow is already running.");
                CloseFlowTask = operation switch
                {
                    "document" => capture.View.RequestCloseDocumentAsync(kind!, path!),
                    "project" => capture.View.RequestCloseProjectAsync(),
                    "open_project" => capture.View.RequestOpenProjectAsync(path!),
                    "create_project" => capture.View.RequestCreateProjectAsync(name!),
                    "editor" => capture.View.RequestEditorExitAsync(),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation))
                };
            }
            return InspectCloseWorkflowCore();
        }, cancellationToken);
    }

    [McpServerTool(Name = "inspect_close_workflow", ReadOnly = true), Description("Inspect the running close workflow, its pending asset list, and the real dialog buttons without answering it.")]
    public Task<object> InspectCloseWorkflow(CancellationToken cancellationToken)
        => ui.Invoke(InspectCloseWorkflowCore, cancellationToken);

    private object InspectCloseWorkflowCore()
    {
        var task = CloseFlowTask;
        var window = capture.View.UnsavedChangesWindow;
        var error = task is { IsFaulted: true }
            ? task.Exception?.GetBaseException().Message
            : task is { IsCanceled: true } ? "The close workflow was canceled."
            : capture.View.CloseWorkflowError;
        return new
        {
            running = capture.View.IsCloseWorkflowActive || task is { IsCompleted: false },
            result = task is { IsCompletedSuccessfully: true } ? task.Result : (bool?)null,
            error,
            pending = capture.View.PendingUnsavedChanges,
            dialogName = window?.Name,
            buttons = window is null ? Array.Empty<object>() : window.GetLogicalDescendants().OfType<Button>()
                .Select(button => (object)new
                {
                    name = button.Name,
                    content = button.Content?.ToString(),
                    isEnabled = button.IsEnabled
                }).ToArray()
        };
    }

    [McpServerTool(Name = "respond_unsaved_changes"), Description("Click the actual Save, Discard, Cancel, or close-error OK button in a pending headless Editor workflow.")]
    public Task<object> RespondUnsavedChanges(string choice, CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            var view = capture.View;
            var window = view.UnsavedChangesWindow;
            var isErrorDialog = window?.Name == "UnsavedChangesError";
            if (isErrorDialog)
            {
                if (!string.Equals(choice, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The close workflow is showing an error; respond with ok to acknowledge it.");
                view.RespondToUnsavedChanges(UnsavedChangesChoice.Cancel);
            }
            else
            {
                if (view.PendingUnsavedChanges is null || window is null)
                    throw new InvalidOperationException("There is no pending unsaved-changes dialog.");
                var response = choice.ToLowerInvariant() switch
                {
                    "save" => UnsavedChangesChoice.Save,
                    "discard" => UnsavedChangesChoice.Discard,
                    "cancel" => UnsavedChangesChoice.Cancel,
                    "ok" => throw new InvalidOperationException("There is no close error to acknowledge; respond with save, discard, or cancel."),
                    _ => throw new ArgumentException("Choice must be save, discard, cancel, or ok.", nameof(choice))
                };
                view.RespondToUnsavedChanges(response);
            }
            return InspectCloseWorkflowCore();
        }, cancellationToken);

    [McpServerTool(Name = "capture_unsaved_changes_dialog", ReadOnly = true), Description("Render the real pending unsaved-changes or close-error dialog from the isolated headless Avalonia window as a PNG. No desktop capture.")]
    public Task<ModelContextProtocol.Protocol.CallToolResult> CaptureUnsavedChangesDialog(CancellationToken cancellationToken, string theme = "Dark")
        => ui.Invoke(() =>
        {
            if (capture.View.UnsavedChangesWindow is null)
                throw new InvalidOperationException("There is no pending dialog to capture.");
            return new ModelContextProtocol.Protocol.CallToolResult
            {
                Content = [ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(capture.CaptureUnsavedChangesDialog(theme), "image/png")]
            };
        }, cancellationToken);
}
