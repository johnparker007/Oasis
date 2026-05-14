using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace OasisEditor;

internal sealed class PanelRuntimeVisualRegistry
{
    private readonly Dictionary<string, FrameworkElement> _visualsByObjectId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingObjectIds = new(StringComparer.Ordinal);

    public void Rebuild(Canvas canvas, Func<FrameworkElement, bool> persistedFilter)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(persistedFilter);

        _visualsByObjectId.Clear();
        foreach (var element in canvas.Children.OfType<FrameworkElement>().Where(persistedFilter))
        {
            if (!string.IsNullOrWhiteSpace(element.Uid))
            {
                _visualsByObjectId[element.Uid.Trim()] = element;
            }
        }
    }

    public bool TryGetVisual(string objectId, out FrameworkElement visual)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            visual = null!;
            return false;
        }

        return _visualsByObjectId.TryGetValue(objectId, out visual!);
    }

    public void LogMissingObjectIdOnce(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        if (_missingObjectIds.Add(objectId))
        {
            Debug.WriteLine($"[PanelRuntimeVisualRegistry] Runtime visual not found for objectId '{objectId}'.");
        }
    }
}
