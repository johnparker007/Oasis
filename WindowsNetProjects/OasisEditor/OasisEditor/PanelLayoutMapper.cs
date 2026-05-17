using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OasisEditor;

public static class PanelLayoutMapper
{
    private const double LegacyReelPositionsPerRevolution = 96d;
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

    private static readonly DependencyProperty RuntimeVisualRegistryProperty =
        DependencyProperty.RegisterAttached(
            "RuntimeVisualRegistry",
            typeof(PanelRuntimeVisualRegistry),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(null));
    private static readonly DependencyProperty CachedLampOnBrushProperty =
        DependencyProperty.RegisterAttached(
            "CachedLampOnBrush",
            typeof(Brush),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(null));
    private static readonly DependencyProperty CachedLampOffBrushProperty =
        DependencyProperty.RegisterAttached(
            "CachedLampOffBrush",
            typeof(Brush),
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
                CanvasSelectionBehavior.SetIsSelectable(visual, !element.IsLocked);
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

        foreach (var objectId in visualStateChange.ValuesByObjectId.Keys)
        {
            if (tab.TryGetLampElement(objectId, out var sourceModel))
            {
                var lampVisualState = visualStateChange.ValuesByObjectId[objectId] switch
                {
                    LampVisualState state => state,
                    true => new LampVisualState(true, runtimeState.GetLampIntensity(objectId)),
                    _ => new LampVisualState(false, runtimeState.GetLampIntensity(objectId))
                };

                UpdateLampVisual(
                    canvas,
                    objectId,
                    lampVisualState.Intensity,
                    lampVisualState.IsLampTestOn || lampVisualState.Intensity > 0d,
                    sourceModel.OnColorHex,
                    sourceModel.OffColorHex,
                    sourceModel.AssetPath);
                continue;
            }

            if (tab.TryGetReelElement(objectId, out var reelModel))
            {
                var reelVisualState = visualStateChange.ValuesByObjectId[objectId] switch
                {
                    ReelVisualState state => state,
                    int value => new ReelVisualState(value),
                    _ => new ReelVisualState(runtimeState.GetReelPosition(objectId))
                };

                UpdateReelVisual(canvas, objectId, reelVisualState.Position, reelModel.Stops.GetValueOrDefault(1));
                continue;
            }

            if (tab.TryGetAlphaElement(objectId, out _) || tab.TryGetSevenSegmentElement(objectId, out _))
            {
                var segmentState = visualStateChange.ValuesByObjectId[objectId] is SegmentVisualState state
                    ? state
                    : new SegmentVisualState(runtimeState.GetSegmentCellMasks(objectId, 16));
                UpdateSegmentVisual(canvas, objectId, segmentState.CellMasks);
            }
        }
    }

    public static void UpdateSegmentVisual(Canvas canvas, string objectId, int[] cellMasks)
    {
        if (string.IsNullOrWhiteSpace(objectId) || cellMasks is null)
        {
            return;
        }

        var registry = GetOrCreateRuntimeVisualRegistry(canvas);
        if (!registry.TryGetVisual(objectId, out var visual)
            || visual is not Border border
            || border.Child is not SegmentDisplayVisualBase segmentVisual)
        {
            return;
        }

        segmentVisual.CellSegmentMasks = cellMasks.ToArray();
        segmentVisual.InvalidateVisual();
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

        var registry = GetOrCreateRuntimeVisualRegistry(canvas);
        if (!registry.TryGetVisual(objectId, out var visual))
        {
            registry.LogMissingObjectIdOnce(objectId);
            return;
        }

        var normalizedIntensity = Math.Clamp(intensity, 0d, 1d);
        var effectiveOpacity = !isOn
            ? 0d
            : normalizedIntensity > 0d
                ? Math.Max(0.1d, normalizedIntensity)
                : 1d;

        if (visual is Border border)
        {
            if (border.Child is Image image)
            {
                if (!string.IsNullOrWhiteSpace(assetPath) && image.Source is null)
                {
                    image.Source = PanelElementFactory.TryCreateImageSourceForRuntime(assetPath);
                }

                SetOpacityIfChanged(image, effectiveOpacity);
                if (!ReferenceEquals(border.Background, Brushes.Transparent))
                {
                    border.Background = Brushes.Transparent;
                }
                return;
            }

            var cachedBrush = isOn
                ? GetOrCreateCachedLampOnBrush(border, onColorHex)
                : GetOrCreateCachedLampOffBrush(border, offColorHex ?? onColorHex);
            if (!ReferenceEquals(border.Background, cachedBrush))
            {
                border.Background = cachedBrush;
            }

            // Text-based lamps use brush color to represent on/off state; keep full opacity
            // so they do not fade into the panel background when lamp test toggles.
            SetOpacityIfChanged(border, 1d);
        }
        else if (visual is Image image)
        {
            SetOpacityIfChanged(image, effectiveOpacity);
        }
    }


    public static void UpdateReelVisual(Canvas canvas, string objectId, int position, int stops)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var registry = GetOrCreateRuntimeVisualRegistry(canvas);
        if (!registry.TryGetVisual(objectId, out var visual)
            || visual is not Border border
            || border.Child is not Grid grid
            || grid.Children.Count == 0
            || grid.Children[0] is not Canvas reelCanvas
            || reelCanvas.Children.Count < 2
            || reelCanvas.Children[0] is not Image primaryImage
            || reelCanvas.Children[1] is not Image wrappedImage)
        {
            return;
        }

        var safeStops = Math.Max(1, stops);
        var bandHeight = primaryImage.Height;
        if (bandHeight <= 0d)
        {
            return;
        }

        var positionsPerRevolution = Math.Max(LegacyReelPositionsPerRevolution, safeStops);
        var stopHeight = bandHeight / safeStops;
        var subStepHeight = stopHeight / (positionsPerRevolution / safeStops);
        var rawPosition = ((position % positionsPerRevolution) + positionsPerRevolution) % positionsPerRevolution;
        var rawOffset = rawPosition * subStepHeight;
        var wrappedOffset = ((rawOffset % bandHeight) + bandHeight) % bandHeight;
        var top = -wrappedOffset;

        Canvas.SetTop(primaryImage, top);
        Canvas.SetTop(wrappedImage, top + bandHeight);
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
        GetOrCreateRuntimeVisualRegistry(canvas).Rebuild(canvas, GetIsPersistedElement);
    }

    private static PanelRuntimeVisualRegistry GetOrCreateRuntimeVisualRegistry(Canvas canvas)
    {
        if (canvas.GetValue(RuntimeVisualRegistryProperty) is not PanelRuntimeVisualRegistry registry)
        {
            registry = new PanelRuntimeVisualRegistry();
            canvas.SetValue(RuntimeVisualRegistryProperty, registry);
        }

        return registry;
    }

    private static Brush GetOrCreateCachedLampOnBrush(Border border, string? onColorHex)
    {
        if (border.GetValue(CachedLampOnBrushProperty) is Brush brush)
        {
            return brush;
        }

        brush = PanelElementFactory.TryCreateBrushForRuntime(onColorHex, Brushes.Transparent);
        border.SetValue(CachedLampOnBrushProperty, brush);
        return brush;
    }

    private static Brush GetOrCreateCachedLampOffBrush(Border border, string? offColorHex)
    {
        if (border.GetValue(CachedLampOffBrushProperty) is Brush brush)
        {
            return brush;
        }

        brush = PanelElementFactory.TryCreateBrushForRuntime(offColorHex, Brushes.Transparent);
        border.SetValue(CachedLampOffBrushProperty, brush);
        return brush;
    }

    private static void SetOpacityIfChanged(UIElement element, double opacity)
    {
        if (Math.Abs(element.Opacity - opacity) < 0.0001d)
        {
            return;
        }

        element.Opacity = opacity;
    }
}
