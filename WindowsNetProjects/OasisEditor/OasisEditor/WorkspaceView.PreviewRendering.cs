using Avalonia.Threading;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private NativePreviewOutputs? _previewOutputs;
    private RuntimeOutputSnapshot _previewFrame = RuntimeOutputSnapshot.Empty;
    private void ConnectPreviewRendering(IEmulationBackend backend, MachineDocument machine)
    {
        var outputs = new NativePreviewOutputs(backend, machine);
        Interlocked.Exchange(ref _previewOutputs, outputs)?.Dispose();
        Dispatcher.UIThread.Post(() => _viewport.SetRuntimeOutputProvider(() => _previewFrame));
    }
    private void RefreshPreviewRendering()
    {
        if (Is3DActive) { RefreshMountedFaceOutputs(); return; }
        if (_previewOutputs is not { } outputs) return;
        var frame = outputs.Capture(_faceActive ? null : _session.RenderDocument, _faceActive ? _faces.Document : null);
        if (frame.Fingerprint == _previewFrame.Fingerprint) return;
        _previewFrame = frame;
        _viewport.RefreshRuntimeOutputs();
    }
    private void ClearPreviewRendering()
    {
        Interlocked.Exchange(ref _previewOutputs, null)?.Dispose();
        _previewFrame = RuntimeOutputSnapshot.Empty;
        _viewport.SetRuntimeOutputProvider(null);
        RefreshMountedFaceOutputs();
    }
}
