using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OasisEditor;

public static class PanelLayoutMapper
{
    private static readonly DependencyProperty IsApplyingLayoutProperty =
        DependencyProperty.RegisterAttached(
            "IsApplyingLayout",
            typeof(bool),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsPersistedElementProperty =
        DependencyProperty.RegisterAttached(
            "IsPersistedElement",
            typeof(bool),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(false));

    private static readonly DependencyProperty ObjectVisualMapProperty =
        DependencyProperty.RegisterAttached(
            "ObjectVisualMap",
            typeof(Dictionary<string, FrameworkElement>),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(null));

    private static readonly DependencyProperty MissingVisualLogSetProperty =
        DependencyProperty.RegisterAttached(
            "MissingVisualLogSet",
            typeof(HashSet<string>),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(null));

    public static bool IsApplyingLayout(Canvas canvas)
    {
        return (bool)canvas.GetValue(IsApplyingLayoutProperty);
    }

    public static void ApplyPersistedLayout(Canvas canvas, string? layoutJson, PanelRuntimeState runtimeState)
    {
        canvas.SetValue(IsApplyingLayoutProperty, true);
        try
        {
            var persistedChildren = canvas.Children
                .OfType<FrameworkElement>()
                .Where(GetIsPersistedElement)
                .ToList();

            foreach (var child in persistedChildren)
            {
                canvas.Children.Remove(child);
            }

            var elements = Panel2DDocumentStorage.DeserializeLayout(layoutJson);
            foreach (var element in elements)
            {
                if (element.IsVisible is false)
                {
                    continue;
                }

                var visual = PanelElementFactory.CreateVisualFromElement(element, runtimeState);
                if (visual is null)
                {
                    continue;
                }

                SetIsPersistedElement(visual, true);
                var isSelectable = !element.IsLocked && element.ElementKind != PanelElementKind.Background;
                CanvasSelectionBehavior.SetIsSelectable(visual, isSelectable);
                CanvasSelectionBehavior.SetIsSelected(visual, false);
                Canvas.SetLeft(visual, Math.Max(0, element.X));
                Canvas.SetTop(visual, Math.Max(0, element.Y));
                canvas.Children.Add(visual);
            }

            RebuildObjectVisualMap(canvas);
            CanvasSelectionBehavior.ClearSelection(canvas);
            if (canvas.DataContext is DocumentTabViewModel tab)
            {
                tab.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            }
        }
        finally
        {
            canvas.SetValue(IsApplyingLayoutProperty, false);
        }
    }

    public static void SyncPanelLayout(Canvas canvas, DependencyProperty panelLayoutJsonProperty)
    {
        if (IsApplyingLayout(canvas))
        {
            return;
        }

        var layoutJson = canvas.DataContext is DocumentTabViewModel tab
            ? tab.GetPanelLayoutProjectionJson()
            : Panel2DDocumentStorage.SerializeLayout(
                canvas.Children
                    .OfType<FrameworkElement>()
                    .Where(GetIsPersistedElement)
                    .Select(PanelElementFactory.CreateElementFromVisual)
                    .Where(element => element is not null)
                    .Cast<PanelElementFile>()
                    .ToArray());
        canvas.SetValue(IsApplyingLayoutProperty, true);
        try
        {
            canvas.SetCurrentValue(panelLayoutJsonProperty, layoutJson);
            if (canvas.DataContext is DocumentTabViewModel tabViewModel)
            {
                tabViewModel.PanelLayoutJson = layoutJson;
            }
        }
        finally
        {
            canvas.SetValue(IsApplyingLayoutProperty, false);
        }
    }

    public static void ApplyVisualState(Canvas canvas, DocumentTabViewModel tab, PanelVisualStateChangedEvent visualStateChange, PanelRuntimeState runtimeState)
    {
        if (visualStateChange.ValuesByObjectId.Count == 0)
        {
            return;
        }

        var elementsByObjectId = tab.GetPanelElements()
            .Where(element => !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, element => element, StringComparer.Ordinal);

        foreach (var objectId in visualStateChange.ValuesByObjectId.Keys)
        {
            if (!elementsByObjectId.TryGetValue(objectId, out var sourceModel)
                || sourceModel.Kind != PanelElementKind.Lamp)
            {
                continue;
            }

            UpdateLampVisual(
                canvas,
                objectId,
                runtimeState.GetLampIntensity(objectId),
                visualStateChange.ValuesByObjectId[objectId] is true,
                sourceModel.OnColorHex,
                sourceModel.OffColorHex,
                sourceModel.AssetPath);
            runtimeState.SetLampIntensity(objectId, runtimeState.GetLampIntensity(objectId));
        }
    }

    public static void UpdateLampVisual(
        Canvas canvas,
        string objectId,
        double intensity,
        bool isOn,
        string? onColorHex,
        string? offColorHex,
        string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var objectVisualMap = GetOrCreateObjectVisualMap(canvas);
        if (!objectVisualMap.TryGetValue(objectId, out var visual))
        {
            var missingIds = GetOrCreateMissingVisualLogSet(canvas);
            if (missingIds.Add(objectId))
            {
                Debug.WriteLine($"[PanelLayoutMapper] UpdateLampVisual skipped; visual not found for objectId '{objectId}'.");
            }

            return;
        }

        var normalizedIntensity = Math.Clamp(intensity, 0d, 1d);
        var effectiveOpacity = isOn ? Math.Max(0.1d, normalizedIntensity) : 0d;

        if (visual is Border border)
        {
            if (border.Child is Image image)
            {
                if (!string.IsNullOrWhiteSpace(assetPath) && image.Source is null)
                {
                    image.Source = PanelElementFactory.TryCreateImageSourceForRuntime(assetPath);
                }

                image.Opacity = effectiveOpacity;
                border.Background = Brushes.Transparent;
                return;
            }

            var backgroundHex = isOn ? onColorHex : offColorHex ?? onColorHex;
            border.Background = PanelElementFactory.TryCreateBrushForRuntime(backgroundHex, Brushes.Transparent);
        }
        else if (visual is Image image)
        {
            image.Opacity = effectiveOpacity;
        }
    }

    private static bool GetIsPersistedElement(FrameworkElement element)
    {
        return (bool)element.GetValue(IsPersistedElementProperty);
    }

    private static void SetIsPersistedElement(FrameworkElement element, bool value)
    {
        element.SetValue(IsPersistedElementProperty, value);
    }

    private static void RebuildObjectVisualMap(Canvas canvas)
    {
        var map = GetOrCreateObjectVisualMap(canvas);
        map.Clear();
        foreach (var element in canvas.Children.OfType<FrameworkElement>().Where(GetIsPersistedElement))
        {
            if (!string.IsNullOrWhiteSpace(element.Uid))
            {
                map[element.Uid.Trim()] = element;
            }
        }
    }

    private static Dictionary<string, FrameworkElement> GetOrCreateObjectVisualMap(Canvas canvas)
    {
        if (canvas.GetValue(ObjectVisualMapProperty) is not Dictionary<string, FrameworkElement> map)
        {
            map = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
            canvas.SetValue(ObjectVisualMapProperty, map);
        }

        return map;
    }

    private static HashSet<string> GetOrCreateMissingVisualLogSet(Canvas canvas)
    {
        if (canvas.GetValue(MissingVisualLogSetProperty) is not HashSet<string> missingIds)
        {
            missingIds = new HashSet<string>(StringComparer.Ordinal);
            canvas.SetValue(MissingVisualLogSetProperty, missingIds);
        }

        return missingIds;
    }
}
