using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using OasisEditor.Rendering;

namespace OasisEditor.Views;

public partial class PlayView : UserControl
{
    private DocumentTabViewModel? _subscribedDocument;
    private bool _isPreProcessInputHooked;
    private Key? _pendingTextInputKey;
    private readonly Dictionary<Key, string> _activeShortcutByKey = new();
    private double _skiaZoom = 1d;
    private Vector _skiaPan;
    private bool _isSkiaPanning;
    private Point _skiaPanStart;
    private Vector _skiaPanOrigin;
    private readonly IPanel2DRenderer _skiaRenderer = new Panel2DRenderer([new BackgroundElementRenderer(), new LampElementRenderer()]);

    public PlayView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshCanvasFromSelection();
        AttachPreProcessInputHandler();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachPreProcessInputHandler();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is MainWindowViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshCanvasFromSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SelectedDocument))
        {
            Dispatcher.Invoke(RefreshCanvasFromSelection);
        }
    }

    private void RefreshCanvasFromSelection()
    {
        var selected = ViewModel?.SelectedDocument;
        UpdateSelectedDocumentSubscription(selected);

        if (selected is null || selected.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            PlayCanvas.Children.Clear();
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        PlaySkiaSurface.InvalidateVisual();
    }


    private void OnPlaySkiaSurfacePaintSurface(object? sender, SKPaintSurfaceEventArgs eventArgs)
    {
        var canvas = eventArgs.Surface.Canvas;
        canvas.Clear(new SKColor(0x1E, 0x1E, 0x1E));

        var selected = ViewModel?.SelectedDocument;
        if (selected is null || selected.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        var viewport = new PanelViewportTransform(_skiaZoom, _skiaPan.X, _skiaPan.Y);
        canvas.Save();
        canvas.Translate((float)viewport.PanX, (float)viewport.PanY);
        canvas.Scale((float)viewport.NormalizedZoom, (float)viewport.NormalizedZoom);
        _skiaRenderer.Render(canvas, selected.GetPanelElements(), selected.RuntimeState, viewport);
        canvas.Restore();
    }

    private void OnPlaySkiaSurfaceMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isSkiaPanning = true;
        _skiaPanStart = eventArgs.GetPosition(PlaySkiaSurface);
        _skiaPanOrigin = _skiaPan;
        PlaySkiaSurface.CaptureMouse();
        eventArgs.Handled = true;
    }

    private void OnPlaySkiaSurfaceMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (!_isSkiaPanning)
        {
            return;
        }

        var current = eventArgs.GetPosition(PlaySkiaSurface);
        var delta = current - _skiaPanStart;
        _skiaPan = _skiaPanOrigin + (Vector)delta;
        PlaySkiaSurface.InvalidateVisual();
    }

    private void OnPlaySkiaSurfaceMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isSkiaPanning = false;
        PlaySkiaSurface.ReleaseMouseCapture();
        eventArgs.Handled = true;
    }

    private void OnPlaySkiaSurfaceMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        var zoomFactor = eventArgs.Delta > 0 ? 1.1d : 1d / 1.1d;
        var previousZoom = _skiaZoom;
        _skiaZoom = Math.Clamp(_skiaZoom * zoomFactor, 0.25d, 4d);
        if (Math.Abs(previousZoom - _skiaZoom) < 0.0001d)
        {
            return;
        }

        var pivot = eventArgs.GetPosition(PlaySkiaSurface);
        var worldX = (pivot.X - _skiaPan.X) / previousZoom;
        var worldY = (pivot.Y - _skiaPan.Y) / previousZoom;

        _skiaPan = new Vector(
            pivot.X - (worldX * _skiaZoom),
            pivot.Y - (worldY * _skiaZoom));
        PlaySkiaSurface.InvalidateVisual();
        eventArgs.Handled = true;
    }

    private async void OnPlayCanvasPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyDownAsync(eventArgs);
    }

    private async void OnPlayCanvasPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyUpAsync(eventArgs);
    }

    private async void OnRootPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyDownAsync(eventArgs);
    }

    private async void OnRootPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyUpAsync(eventArgs);
    }

    private async void OnPlayCanvasPreviewTextInput(object sender, TextCompositionEventArgs eventArgs)
    {
        await TryRouteTextInputAsync(eventArgs);
    }

    private async void OnRootPreviewTextInput(object sender, TextCompositionEventArgs eventArgs)
    {
        await TryRouteTextInputAsync(eventArgs);
    }

    private void OnPlayCanvasPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayCanvasFocused();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayCanvasFocused();
    }

    private async void OnPlayCanvasPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null || !TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        await ViewModel.TryHandlePlayViewPointerDownAsync(visualElementId, isFocused: true, CancellationToken.None);
    }

    private async void OnPlayCanvasPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null || !TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        await ViewModel.TryHandlePlayViewPointerUpAsync(visualElementId, isFocused: true, CancellationToken.None);
    }


    private void OnPlayCanvasMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseDown(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasMouseMove(object sender, MouseEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseMove(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseUp(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseWheel(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasLostMouseCapture(object sender, MouseEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleLostMouseCapture(PlayCanvas);
    }

    private async void OnPlayCanvasLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View focus loss", CancellationToken.None);
    }

    private async void OnPlayCanvasUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View close", CancellationToken.None);
    }

    private bool TryResolveVisualElementId(DependencyObject? source, out Guid visualElementId)
    {
        var selectable = CanvasSelectionBehavior.FindSelectableElement(source, PlayCanvas);
        if (selectable is not null && Guid.TryParse(selectable.Uid?.Trim(), out visualElementId))
        {
            return true;
        }

        visualElementId = Guid.Empty;
        return false;
    }

    private void UpdateSelectedDocumentSubscription(DocumentTabViewModel? selected)
    {
        if (ReferenceEquals(_subscribedDocument, selected))
        {
            return;
        }

        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelVisualStateChanged -= OnSelectedDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged -= OnSelectedDocumentPropertyChanged;
        }

        _subscribedDocument = selected;
        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelVisualStateChanged += OnSelectedDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged += OnSelectedDocumentPropertyChanged;
        }
    }

    private void OnSelectedDocumentPanelVisualStateChanged(PanelVisualStateChangedEvent visualStateChanged)
    {
        if (_subscribedDocument is null || visualStateChanged.DocumentId != _subscribedDocument.DocumentId)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            PlaySkiaSurface.InvalidateVisual();
        });
    }

    private void OnSelectedDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.PanelLayoutJson))
        {
            Dispatcher.Invoke(RefreshCanvasFromSelection);
        }
    }

    private void ApplyClickableCursorHints(DocumentTabViewModel selectedDocument)
    {
        var clickableObjectIds = new HashSet<Guid>(
            (ViewModel?.InputDefinitions ?? [])
                .Where(input => input.LinkedVisualElementId.HasValue)
                .Select(input => input.LinkedVisualElementId!.Value));

        foreach (var visual in PlayCanvas.Children.OfType<FrameworkElement>())
        {
            var uid = visual.Uid?.Trim();
            var isClickable = !string.IsNullOrWhiteSpace(uid)
                && Guid.TryParse(uid, out var visualId)
                && clickableObjectIds.Contains(visualId);
            visual.Cursor = isClickable
                ? Cursors.Hand
                : Cursors.Arrow;
        }
    }

    private void EnsurePlayCanvasFocused()
    {
        if (!PlayCanvas.IsKeyboardFocusWithin)
        {
            Keyboard.Focus(PlayCanvas);
        }
    }

    private async Task TryRouteKeyDownAsync(KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled || ViewModel is null || !IsKeyboardFocusWithin)
        {
            return;
        }

        var key = ResolveKey(eventArgs);
        var shortcut = MfmeShortcutKeyMapper.TryMapKeyToMfmeShortcut(key, out var mappedShortcut)
            ? mappedShortcut
            : key.ToString();
        var handled = await ViewModel.TryHandlePlayViewKeyDownAsync(shortcut, isFocused: true, eventArgs.IsRepeat, CancellationToken.None);
        if (handled)
        {
            _activeShortcutByKey[key] = shortcut;
            eventArgs.Handled = true;
            _pendingTextInputKey = null;
            return;
        }

        _pendingTextInputKey = key;
    }

    private async Task TryRouteKeyUpAsync(KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled || ViewModel is null || !IsKeyboardFocusWithin)
        {
            return;
        }

        var key = ResolveKey(eventArgs);
        var shortcut = MfmeShortcutKeyMapper.TryMapKeyToMfmeShortcut(key, out var mappedShortcut)
            ? mappedShortcut
            : key.ToString();
        if (_activeShortcutByKey.TryGetValue(key, out var activeShortcut))
        {
            shortcut = activeShortcut;
            _activeShortcutByKey.Remove(key);
        }

        var handled = await ViewModel.TryHandlePlayViewKeyUpAsync(shortcut, isFocused: true, CancellationToken.None);
        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private void AttachPreProcessInputHandler()
    {
        if (_isPreProcessInputHooked)
        {
            return;
        }

        InputManager.Current.PreProcessInput += OnPreProcessInput;
        _isPreProcessInputHooked = true;
    }

    private void DetachPreProcessInputHandler()
    {
        if (!_isPreProcessInputHooked)
        {
            return;
        }

        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        _isPreProcessInputHooked = false;
    }

    private void OnPreProcessInput(object? sender, PreProcessInputEventArgs eventArgs)
    {
        if (eventArgs.StagingItem.Input is not KeyEventArgs keyEventArgs)
        {
            return;
        }

        if (keyEventArgs.RoutedEvent == Keyboard.PreviewKeyDownEvent)
        {
            _ = TryRouteKeyDownAsync(keyEventArgs);
        }
        else if (keyEventArgs.RoutedEvent == Keyboard.PreviewKeyUpEvent)
        {
            _ = TryRouteKeyUpAsync(keyEventArgs);
        }
    }

    private static Key ResolveKey(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.System)
        {
            return eventArgs.SystemKey;
        }

        if (eventArgs.Key == Key.ImeProcessed)
        {
            return eventArgs.ImeProcessedKey;
        }

        return eventArgs.Key;
    }

    private async Task TryRouteTextInputAsync(TextCompositionEventArgs eventArgs)
    {
        if (eventArgs.Handled || ViewModel is null || !IsKeyboardFocusWithin || _pendingTextInputKey is null)
        {
            return;
        }

        var text = eventArgs.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var shortcut = MfmeShortcutKeyMapper.NormalizeShortcutForRouting(text.ToUpperInvariant());
        var handled = await ViewModel.TryHandlePlayViewKeyDownAsync(shortcut, isFocused: true, isRepeat: false, CancellationToken.None);
        if (!handled)
        {
            return;
        }

        _activeShortcutByKey[_pendingTextInputKey.Value] = shortcut;
        _pendingTextInputKey = null;
        eventArgs.Handled = true;
    }
}
