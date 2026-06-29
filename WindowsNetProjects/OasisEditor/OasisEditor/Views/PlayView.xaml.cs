using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using OasisEditor.Rendering;
using OasisEditor.Progress;

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
    private PanelElementModel? _activeReelDragElement;
    private MachineInputReference? _activeFacePointerInputReference;
    private Point _reelDragStart;
    private double _reelDragStartTemporaryOffset;
    private bool _isRenderQueued;
    private bool _isRenderDirty;
    private int _selectionRefreshVersion;
    private Guid? _preparedFaceDocumentId;
    private string? _preparedFaceDocumentJson;
    private Guid? _preparingFaceDocumentId;
    private readonly Stopwatch _renderStopwatch = Stopwatch.StartNew();
    private readonly DispatcherTimer _renderThrottleTimer;
    private const double TargetFrameMillis = 16.0;
    private const double LegacyReelPositionsPerRevolution = 96d;
    private const double ReelDragSpeedScale = 3d;
    private readonly IPanel2DRenderer _skiaRenderer = new Panel2DRenderer([new BackgroundElementRenderer(), new LampElementRenderer(), new ReelElementRenderer(), new SevenSegmentElementRenderer(), new AlphaElementRenderer(), new VfdDotMatrixElementRenderer()], "PlayView");
    private readonly IFaceCompositor _faceCompositor = FaceCompositor.Shared;
    private readonly IFaceInputTargetResolver _faceInputTargetResolver = FaceInputTargetResolver.Instance;

    public PlayView()
    {
        InitializeComponent();
        _renderThrottleTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
        _renderThrottleTimer.Tick += OnRenderThrottleTick;
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

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _renderThrottleTimer.Stop();
        DetachPreProcessInputHandler();
        await ReleasePlayViewInputsAsync("Play View close");
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

        if (selected is null || selected.Document.DocumentType is not (EditorDocumentType.Panel2D or EditorDocumentType.Face))
        {
            PlayCanvas.Children.Clear();
            EmptyStateText.Text = "Open and select a Panel2D or Face document to use Play View.";
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;

        if (selected.Document.DocumentType == EditorDocumentType.Face)
        {
            _ = PrepareFacePlayViewAsync(selected, ++_selectionRefreshVersion);
            return;
        }

        _selectionRefreshVersion++;
        RequestRender();
    }

    private bool IsFacePlayViewPrepared(DocumentTabViewModel selected)
    {
        return _preparedFaceDocumentId == selected.DocumentId
            && string.Equals(_preparedFaceDocumentJson, selected.FaceDocumentJson, StringComparison.Ordinal);
    }

    private async Task PrepareFacePlayViewAsync(DocumentTabViewModel selected, int refreshVersion)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (IsFacePlayViewPrepared(selected))
        {
            RequestRender();
            return;
        }

        if (_preparingFaceDocumentId == selected.DocumentId)
        {
            return;
        }

        _preparingFaceDocumentId = selected.DocumentId;
        try
        {
            await WaitForActiveProgressOperationAsync(viewModel);
            if (refreshVersion != _selectionRefreshVersion || !ReferenceEquals(ViewModel?.SelectedDocument, selected))
            {
                return;
            }

            await viewModel.RunEditorProgressAsync(
                new EditorProgressRequest("Generating Face Play View", "Generating Face Play View...", EditorProgressMode.Indeterminate, ShowDelay: TimeSpan.Zero),
                async (progress, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    progress.ReportIndeterminate("Loading Face render assets...");
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    token.ThrowIfCancellationRequested();
                    WarmFacePlayViewRenderCache(selected);
                    progress.ReportIndeterminate("Finalizing Face Play View...");
                });

            _preparedFaceDocumentId = selected.DocumentId;
            _preparedFaceDocumentJson = selected.FaceDocumentJson;
        }
        catch (Exception ex)
        {
            viewModel.ReportEditorOperationError($"Generate Face Play View failed: {ex.Message}", OutputLogStatus.Error);
            EmptyStateText.Text = ex.Message;
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }
        finally
        {
            _preparingFaceDocumentId = null;
        }

        if (refreshVersion == _selectionRefreshVersion)
        {
            RequestRender();
        }
    }

    private static async Task WaitForActiveProgressOperationAsync(MainWindowViewModel viewModel)
    {
        while (viewModel.IsEditorProgressOperationActive)
        {
            await Task.Delay(50);
        }
    }

    private void WarmFacePlayViewRenderCache(DocumentTabViewModel selected)
    {
        using var surface = SKSurface.Create(new SKImageInfo(1, 1));
        if (surface is null)
        {
            return;
        }

        _faceCompositor.Render(
            surface.Canvas,
            selected.GetFaceDocument(),
            selected.RuntimeState,
            new PanelViewportTransform(_skiaZoom, _skiaPan.X, _skiaPan.Y));
    }

    private void OnPlaySkiaSurfacePaintSurface(object? sender, SKPaintSurfaceEventArgs eventArgs)
    {
        _isRenderDirty = false;
        var canvas = eventArgs.Surface.Canvas;
        canvas.Clear(new SKColor(0x1E, 0x1E, 0x1E));

        var selected = ViewModel?.SelectedDocument;
        if (selected is null || selected.Document.DocumentType is not (EditorDocumentType.Panel2D or EditorDocumentType.Face))
        {
            return;
        }

        var viewport = new PanelViewportTransform(_skiaZoom, _skiaPan.X, _skiaPan.Y);
        canvas.Save();
        canvas.Translate((float)viewport.PanX, (float)viewport.PanY);
        canvas.Scale((float)viewport.NormalizedZoom, (float)viewport.NormalizedZoom);
        if (selected.Document.DocumentType == EditorDocumentType.Face)
        {
            if (!IsFacePlayViewPrepared(selected))
            {
                return;
            }

            _faceCompositor.Render(canvas, selected.GetFaceDocument(), selected.RuntimeState, viewport);
        }
        else
        {
            _skiaRenderer.Render(canvas, selected.GetPanelElements(), selected.RuntimeState, viewport);
        }
        canvas.Restore();
    }

    private void RequestRender()
    {
        _isRenderDirty = true;
        if (_isRenderQueued || _renderThrottleTimer.IsEnabled)
        {
            return;
        }

        var elapsedMillis = _renderStopwatch.Elapsed.TotalMilliseconds;
        if (elapsedMillis < TargetFrameMillis)
        {
            _renderThrottleTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1.0, TargetFrameMillis - elapsedMillis));
            _renderThrottleTimer.Start();
            return;
        }

        QueueRenderNow();
    }

    private void OnRenderThrottleTick(object? sender, EventArgs e)
    {
        _renderThrottleTimer.Stop();
        if (_isRenderDirty)
        {
            QueueRenderNow();
        }
    }

    private void QueueRenderNow()
    {
        _isRenderQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _isRenderQueued = false;
            _renderStopwatch.Restart();
            PlaySkiaSurface.InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    private async void OnPlaySkiaSurfaceMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayViewFocused();

        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            var current = eventArgs.GetPosition(PlaySkiaSurface);
            if (ViewModel?.SelectedDocument?.Document.DocumentType == EditorDocumentType.Face)
            {
                if (ViewModel is not null && TryResolveSkiaFaceInputReference(current, out var inputReference))
                {
                    if (await ViewModel.TryHandlePlayViewPointerDownAsync(PlayInputTarget.ForMachineInput(inputReference), isFocused: true, CancellationToken.None))
                    {
                        _activeFacePointerInputReference = inputReference;
                        PlaySkiaSurface.CaptureMouse();
                    }

                    eventArgs.Handled = true;
                }

                return;
            }

            if (TryResolveSkiaReelElement(current, out var reelElement))
            {
                BeginReelDrag(reelElement, current);
                eventArgs.Handled = true;
                return;
            }

            if (ViewModel is not null && TryResolveSkiaVisualElementId(current, out var visualElementId))
            {
                await ViewModel.TryHandlePlayViewPointerDownAsync(visualElementId, isFocused: true, CancellationToken.None);
                eventArgs.Handled = true;
            }

            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isSkiaPanning = true;
        _skiaPanStart = eventArgs.GetPosition(PlaySkiaSurface);
        _skiaPanOrigin = _skiaPan;
        PlaySkiaSurface.Cursor = Cursors.SizeAll;
        PlaySkiaSurface.CaptureMouse();
        eventArgs.Handled = true;
    }

    private void OnPlaySkiaSurfaceMouseMove(object sender, MouseEventArgs eventArgs)
    {
        var current = eventArgs.GetPosition(PlaySkiaSurface);
        if (_activeReelDragElement is not null)
        {
            UpdateReelDrag(current);
            eventArgs.Handled = true;
            return;
        }

        if (_isSkiaPanning)
        {
            PlaySkiaSurface.Cursor = Cursors.SizeAll;
            var delta = current - _skiaPanStart;
            _skiaPan = _skiaPanOrigin + (Vector)delta;
            RequestRender();
            return;
        }

        UpdatePlaySkiaHoverCursor(current);
    }

    private async void OnPlaySkiaSurfaceMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            if (_activeReelDragElement is not null)
            {
                EndReelDrag();
                eventArgs.Handled = true;
                return;
            }

            if (_activeFacePointerInputReference is MachineInputReference activeFaceInputReference)
            {
                _activeFacePointerInputReference = null;
                if (ViewModel is not null)
                {
                    await ViewModel.TryHandlePlayViewPointerUpAsync(PlayInputTarget.ForMachineInput(activeFaceInputReference), isFocused: true, CancellationToken.None);
                }

                if (PlaySkiaSurface.IsMouseCaptured)
                {
                    PlaySkiaSurface.ReleaseMouseCapture();
                }

                eventArgs.Handled = true;
                return;
            }

            if (ViewModel?.SelectedDocument?.Document.DocumentType == EditorDocumentType.Face)
            {
                return;
            }

            if (ViewModel is not null && TryResolveSkiaVisualElementId(eventArgs.GetPosition(PlaySkiaSurface), out var visualElementId))
            {
                await ViewModel.TryHandlePlayViewPointerUpAsync(visualElementId, isFocused: true, CancellationToken.None);
                eventArgs.Handled = true;
            }

            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isSkiaPanning = false;
        PlaySkiaSurface.ReleaseMouseCapture();
        UpdatePlaySkiaHoverCursor(eventArgs.GetPosition(PlaySkiaSurface));
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
        RequestRender();
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
        EnsurePlayViewFocused();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayViewFocused();
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

    private async void OnPlaySkiaSurfaceLostMouseCapture(object sender, MouseEventArgs eventArgs)
    {
        if (_isSkiaPanning)
        {
            _isSkiaPanning = false;
            UpdatePlaySkiaHoverCursor(eventArgs.GetPosition(PlaySkiaSurface));
        }

        if (_activeReelDragElement is not null)
        {
            EndReelDrag(releaseMouseCapture: false);
        }

        if (_activeFacePointerInputReference is MachineInputReference activeFaceInputReference)
        {
            _activeFacePointerInputReference = null;
            if (ViewModel is not null)
            {
                await ViewModel.TryHandlePlayViewPointerUpAsync(PlayInputTarget.ForMachineInput(activeFaceInputReference), isFocused: true, CancellationToken.None);
            }
        }
    }

    private async void OnPlayCanvasLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        await ReleasePlayViewInputsAsync("Play View focus loss");
    }

    private async void OnRootLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        if (IsKeyboardFocusWithin)
        {
            return;
        }

        await ReleasePlayViewInputsAsync("Play View focus loss");
    }

    private async void OnPlayCanvasUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        await ReleasePlayViewInputsAsync("Play View close");
    }

    private async Task ReleasePlayViewInputsAsync(string reason)
    {
        _pendingTextInputKey = null;
        _activeShortcutByKey.Clear();

        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync(reason, CancellationToken.None);
    }

    private void UpdatePlaySkiaHoverCursor(Point current)
    {
        if (ViewModel?.SelectedDocument?.Document.DocumentType == EditorDocumentType.Face)
        {
            PlaySkiaSurface.Cursor = TryResolveSkiaFaceInputReference(current, out _) ? Cursors.Hand : Cursors.Arrow;
            return;
        }

        if (TryResolveSkiaReelElement(current, out _))
        {
            PlaySkiaSurface.Cursor = Cursors.ScrollNS;
            return;
        }

        var isClickable = TryResolveSkiaVisualElementId(current, out _);
        PlaySkiaSurface.Cursor = isClickable ? Cursors.Hand : Cursors.Arrow;
    }

    private void BeginReelDrag(PanelElementModel reelElement, Point current)
    {
        _activeReelDragElement = reelElement;
        _reelDragStart = current;
        _reelDragStartTemporaryOffset = ViewModel?.SelectedDocument?.RuntimeState.GetTemporaryReelOffset(reelElement.ObjectId) ?? 0d;
        PlaySkiaSurface.Cursor = Cursors.ScrollNS;
        PlaySkiaSurface.CaptureMouse();
    }

    private void UpdateReelDrag(Point current)
    {
        if (_activeReelDragElement is null || ViewModel?.SelectedDocument is not { } selected)
        {
            return;
        }

        var bandHeight = ResolveReelBandHeight(_activeReelDragElement);
        if (bandHeight <= 0d)
        {
            return;
        }

        var positionsPerRevolution = Math.Max(LegacyReelPositionsPerRevolution, _activeReelDragElement.Stops.GetValueOrDefault(1));
        var dragDelta = current.Y - _reelDragStart.Y;
        var temporaryOffset = _reelDragStartTemporaryOffset - (dragDelta * positionsPerRevolution * ReelDragSpeedScale / bandHeight);
        if (selected.RuntimeState.SetTemporaryReelOffsetIfChanged(_activeReelDragElement.ObjectId, temporaryOffset))
        {
            RequestRender();
        }
    }

    private void EndReelDrag(bool releaseMouseCapture = true)
    {
        var reelElement = _activeReelDragElement;
        _activeReelDragElement = null;

        if (reelElement is not null
            && ViewModel?.SelectedDocument is { } selected
            && selected.RuntimeState.ClearTemporaryReelOffsetIfChanged(reelElement.ObjectId))
        {
            RequestRender();
        }

        if (releaseMouseCapture && PlaySkiaSurface.IsMouseCaptured)
        {
            PlaySkiaSurface.ReleaseMouseCapture();
        }

        PlaySkiaSurface.Cursor = Cursors.Arrow;
    }

    private static double ResolveReelBandHeight(PanelElementModel reelElement)
    {
        var visibleScale = reelElement.VisibleScale;
        if (!visibleScale.HasValue || double.IsNaN(visibleScale.Value) || double.IsInfinity(visibleScale.Value))
        {
            return reelElement.Height;
        }

        return reelElement.Height / Math.Clamp(visibleScale.Value, 0.01d, 1d);
    }

    private bool TryResolveVisualElementId(DependencyObject? source, out Guid visualElementId)
    {
        while (source is not null && source != PlayCanvas)
        {
            if (source is FrameworkElement frameworkElement
                && Guid.TryParse(frameworkElement.Uid?.Trim(), out visualElementId))
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        visualElementId = Guid.Empty;
        return false;
    }


    private bool TryResolveSkiaFaceInputReference(Point screenPoint, out MachineInputReference inputReference)
    {
        var selected = ViewModel?.SelectedDocument;
        if (selected is null || selected.Document.DocumentType != EditorDocumentType.Face)
        {
            inputReference = default;
            return false;
        }

        var documentPoint = ToDocumentPoint(screenPoint);
        return _faceInputTargetResolver.TryResolveInputReference(selected.GetFaceElements(), documentPoint, out inputReference);
    }

    private bool TryResolveSkiaReelElement(Point screenPoint, out PanelElementModel reelElement)
    {
        var selected = ViewModel?.SelectedDocument;
        if (selected is null)
        {
            reelElement = new PanelElementModel();
            return false;
        }

        var documentPoint = ToDocumentPoint(screenPoint);
        foreach (var element in selected.GetPanelElements().Reverse())
        {
            if (element.Kind != PanelElementKind.Reel || !element.IsVisible)
            {
                continue;
            }

            if (ContainsDocumentPoint(element, documentPoint))
            {
                reelElement = element;
                return true;
            }
        }

        reelElement = new PanelElementModel();
        return false;
    }

    private bool TryResolveSkiaVisualElementId(Point screenPoint, out Guid visualElementId)
    {
        var selected = ViewModel?.SelectedDocument;
        if (selected is null)
        {
            visualElementId = Guid.Empty;
            return false;
        }

        var clickableObjectIds = new HashSet<Guid>(
            (ViewModel?.InputDefinitions ?? [])
                .Where(input => input.LinkedVisualElementId.HasValue)
                .Select(input => input.LinkedVisualElementId!.Value));

        if (clickableObjectIds.Count == 0)
        {
            visualElementId = Guid.Empty;
            return false;
        }

        var documentPoint = ToDocumentPoint(screenPoint);

        foreach (var element in selected.GetPanelElements().Reverse())
        {
            if (!element.IsVisible || !Guid.TryParse(element.ObjectId, out var elementId) || !clickableObjectIds.Contains(elementId))
            {
                continue;
            }

            if (ContainsDocumentPoint(element, documentPoint))
            {
                visualElementId = elementId;
                return true;
            }
        }

        visualElementId = Guid.Empty;
        return false;
    }

    private Point ToDocumentPoint(Point screenPoint)
    {
        var viewport = new PanelViewportTransform(_skiaZoom, _skiaPan.X, _skiaPan.Y);
        return viewport.ScreenToDocument(screenPoint);
    }

    private static bool ContainsDocumentPoint(PanelElementModel element, Point documentPoint)
    {
        return documentPoint.X >= element.X
            && documentPoint.X <= element.X + element.Width
            && documentPoint.Y >= element.Y
            && documentPoint.Y <= element.Y + element.Height;
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
            _subscribedDocument.FaceVisualStateChanged -= OnSelectedDocumentFaceVisualStateChanged;
            _subscribedDocument.PropertyChanged -= OnSelectedDocumentPropertyChanged;
        }

        _subscribedDocument = selected;
        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelVisualStateChanged += OnSelectedDocumentPanelVisualStateChanged;
            _subscribedDocument.FaceVisualStateChanged += OnSelectedDocumentFaceVisualStateChanged;
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
            RequestRender();
        });
    }

    private void OnSelectedDocumentFaceVisualStateChanged(FaceVisualStateChangedEvent visualStateChanged)
    {
        if (_subscribedDocument is null || visualStateChanged.DocumentId != _subscribedDocument.DocumentId)
        {
            return;
        }

        Dispatcher.Invoke(RefreshCanvasFromSelection);
    }

    private void OnSelectedDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.PanelLayoutJson) or nameof(DocumentTabViewModel.FaceDocumentJson))
        {
            Dispatcher.Invoke(RefreshCanvasFromSelection);
        }
    }

    private void EnsurePlayViewFocused()
    {
        if (!IsKeyboardFocusWithin)
        {
            Keyboard.Focus(this);
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
