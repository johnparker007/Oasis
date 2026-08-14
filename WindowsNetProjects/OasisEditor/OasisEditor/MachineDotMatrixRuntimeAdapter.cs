namespace OasisEditor;

public sealed class MachineDotMatrixRuntimeAdapter : IMachineDotMatrixRuntimeAdapter
{
    internal const int SupportedWidth = 96;
    internal const int SupportedHeight = 8;
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Action<Action> _uiDispatch;
    private readonly Action<string> _warningLogger;
    private readonly Dictionary<int, PendingDisplay> _pending = [];
    private bool _uiUpdateScheduled;

    public MachineDotMatrixRuntimeAdapter(Func<IEnumerable<DocumentTabViewModel>> documentProvider,
        Action<Action> uiDispatch, Action<string>? warningLogger = null)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
        _warningLogger = warningLogger ?? (_ => { });
    }

    public void ApplyDisplayState(int displayId, int width, int height, IReadOnlyList<int> dots, double brightness)
    {
        ArgumentNullException.ThrowIfNull(dots);
        lock (_pendingSync)
        {
            _pending[displayId] = new(width, height, dots.Select(dot => dot == 0 ? 0 : 1).ToArray(), brightness);
            if (_uiUpdateScheduled) return;
            _uiUpdateScheduled = true;
        }
        _uiDispatch(ApplyPendingOnUiThread);
    }

    private void ApplyPendingOnUiThread()
    {
        Dictionary<int, PendingDisplay> snapshot;
        lock (_pendingSync)
        {
            snapshot = new(_pending);
            _pending.Clear();
            _uiUpdateScheduled = false;
        }

        foreach (var (displayId, display) in snapshot)
        {
            if (display.Width != SupportedWidth || display.Height != SupportedHeight
                || display.Dots.Length != SupportedWidth * SupportedHeight)
            {
                _warningLogger($"Ignoring Fabric dot-matrix display {displayId}: Oasis VFD dot matrices require {SupportedWidth}x{SupportedHeight}, received {display.Width}x{display.Height} with {display.Dots.Length} dots.");
                continue;
            }

            foreach (var document in _documentProvider())
            {
                var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var element in document.GetPanelElements().Where(element =>
                             element.Kind == PanelElementKind.VfdDotMatrix
                             && !string.IsNullOrWhiteSpace(element.ObjectId)
                             && element.DisplayNumber.GetValueOrDefault() == displayId))
                {
                    if (document.RuntimeState.SetVfdDotMatrixDotsIfChanged(element.ObjectId!, display.Dots))
                        changedObjectIds.Add(element.ObjectId!);
                }
                if (changedObjectIds.Count > 0)
                    document.NotifyPanelVisualPreviewChanged(changedObjectIds);
            }
        }
    }

    private sealed record PendingDisplay(int Width, int Height, int[] Dots, double Brightness);
}
