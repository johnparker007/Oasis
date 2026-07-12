using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using OasisEditor.Rendering;

namespace OasisEditor.Views;

public partial class SkiaFaceEditView : UserControl
{
    private const double TargetFrameMillis = 16.0;
    private const double DragSelectionStartThreshold = 4d;
    private static readonly ConcurrentDictionary<string, SKImage?> CachedArtworkImages = new(StringComparer.OrdinalIgnoreCase);
    private DocumentTabViewModel? _subscribedDocument;
    private bool _isPanning;
    private bool _isLeftMouseDown;
    private bool _isDragSelecting;
    private bool _isMovingSelection;
    private bool _isCommittingMoveSelection;
    private Point _leftMouseDownStart;
    private Point _dragSelectionCurrent;
    private FaceElementModel? _moveSourceElement;
    private IReadOnlyList<FaceElementMoveSnapshot> _moveSnapshots = [];
    private bool _isRenderQueued;
    private bool _isRenderDirty;
    private Point _panStart;
    private Vector _panOrigin;
    private readonly Stopwatch _renderStopwatch = Stopwatch.StartNew();
    private readonly DispatcherTimer _renderThrottleTimer;

    public SkiaFaceEditView()
    {
        InitializeComponent();
        _renderThrottleTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
        _renderThrottleTimer.Tick += OnRenderThrottleTick;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DocumentTabViewModel? Document => DataContext as DocumentTabViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDocumentSubscription(Document);
        RequestRender();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _renderThrottleTimer.Stop();
        UpdateDocumentSubscription(null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateDocumentSubscription(e.NewValue as DocumentTabViewModel);
        RequestRender();
    }

    private void UpdateDocumentSubscription(DocumentTabViewModel? next)
    {
        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelChanged -= OnDocumentChanged;
            _subscribedDocument.FaceVisualStateChanged -= OnDocumentFaceVisualStateChanged;
            _subscribedDocument.SelectionChanged -= OnDocumentSelectionChanged;
            _subscribedDocument.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _subscribedDocument = next;
        if (_subscribedDocument is null)
        {
            return;
        }

        _subscribedDocument.PanelChanged += OnDocumentChanged;
        _subscribedDocument.FaceVisualStateChanged += OnDocumentFaceVisualStateChanged;
        _subscribedDocument.SelectionChanged += OnDocumentSelectionChanged;
        _subscribedDocument.PropertyChanged += OnDocumentPropertyChanged;
    }

    private void OnDocumentChanged(PanelChangeEvent _)
    {
        RequestRender();
    }

    private void OnDocumentFaceVisualStateChanged(FaceVisualStateChangedEvent _)
    {
        RequestRender();
    }

    private void OnDocumentSelectionChanged(object? sender, DocumentSelectionChangedEventArgs eventArgs)
    {
        RequestRender();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(DocumentTabViewModel.FaceZoom)
            or nameof(DocumentTabViewModel.FacePanX)
            or nameof(DocumentTabViewModel.FacePanY)
            or nameof(DocumentTabViewModel.HierarchySelectedPanelSelection))
        {
            RequestRender();
        }
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
            FaceSkiaSurface.InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    private void OnFaceSkiaSurfacePaintSurface(object? sender, SKPaintSurfaceEventArgs eventArgs)
    {
        _isRenderDirty = false;
        var canvas = eventArgs.Surface.Canvas;
        canvas.Clear(new SKColor(0x1E, 0x1E, 0x1E));

        var document = Document;
        if (document is null || document.Document.DocumentType != EditorDocumentType.Face)
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        canvas.Save();
        canvas.Translate((float)viewport.PanX, (float)viewport.PanY);
        canvas.Scale((float)viewport.NormalizedZoom, (float)viewport.NormalizedZoom);
        DrawFaceElements(canvas, document, viewport);
        DrawSelectionOutline(canvas, document, viewport);
        DrawDragSelectionRect(canvas, viewport);
        canvas.Restore();
    }

    private static void DrawFaceElements(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xC1, 0x07, 0x66), IsAntialias = true };
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0xFF, 0xD5, 0x4F), StrokeWidth = (float)(1.5d / viewport.NormalizedZoom), IsAntialias = true };
        using var hiddenPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x80, 0x80, 0x80), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };

        foreach (var element in document.GetFaceElements().OfType<FaceArtworkElement>())
        {
            DrawArtworkElement(canvas, element, viewport, hiddenPaint);
        }

        foreach (var element in document.GetFaceElements().Where(element => element is not FaceArtworkElement))
        {
            var rect = SKRect.Create((float)element.X, (float)element.Y, (float)Math.Max(0d, element.Width), (float)Math.Max(0d, element.Height));
            if (element is FaceReelDisplayElement reelDisplay)
            {
                DrawReelElement(canvas, document, reelDisplay, rect, hiddenPaint);
                continue;
            }

            if (element is FaceSevenSegmentDisplayElement sevenSegmentDisplay)
            {
                DrawSevenSegmentElement(canvas, document, sevenSegmentDisplay, rect, hiddenPaint);
                continue;
            }

            if (element is FaceAlphaDisplayElement alphaDisplay)
            {
                DrawAlphaElement(canvas, document, alphaDisplay, rect, hiddenPaint);
                continue;
            }

            if (element.IsVisible)
            {
                canvas.DrawRect(rect, fillPaint);
                canvas.DrawRect(rect, strokePaint);
            }
            else
            {
                canvas.DrawRect(rect, hiddenPaint);
            }
        }

    }

    private static void DrawSelectionOutline(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        foreach (var item in document.SelectionState.Items.Where(item => item.Domain == EditorSelectionDomain.FaceElement))
        {
            if (!document.TryGetFaceElementByObjectId(item.ObjectId, out var selectedElement)) continue;
            var isPrimary = document.SelectionState.PrimaryItem == item;
            using var selectionPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = isPrimary ? new SKColor(0xFF, 0xEA, 0x00) : new SKColor(0x4F, 0xC3, 0xF7),
                StrokeWidth = (float)((isPrimary ? 3d : 2d) / viewport.NormalizedZoom),
                IsAntialias = true
            };
            canvas.DrawRect((float)selectedElement.X, (float)selectedElement.Y, (float)Math.Max(0d, selectedElement.Width), (float)Math.Max(0d, selectedElement.Height), selectionPaint);
        }
    }

    private static void DrawReelElement(SKCanvas canvas, DocumentTabViewModel document, FaceReelDisplayElement element, SKRect rect, SKPaint hiddenPaint)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            canvas.DrawRect(rect, hiddenPaint);
            return;
        }

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(element, document.RuntimeState);
        ReelElementRenderer.RenderReelDisplay(canvas, rect, element.AssetPath, position, element.Stops.GetValueOrDefault(1), element.VisibleScale);
    }

    private static void DrawSevenSegmentElement(SKCanvas canvas, DocumentTabViewModel document, FaceSevenSegmentDisplayElement element, SKRect rect, SKPaint hiddenPaint)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            canvas.DrawRect(rect, hiddenPaint);
            return;
        }

        var masks = FaceRuntimeStateResolver.Instance.GetSevenSegmentCellMasks(element, document.RuntimeState);
        var brightness = FaceRuntimeStateResolver.Instance.GetSevenSegmentCellBrightness(element, document.RuntimeState);
        SevenSegmentElementRenderer.RenderSegmentDisplay(canvas, rect, masks, brightness, element.OnColorHex, element.OffColorHex);
    }

    private static void DrawAlphaElement(SKCanvas canvas, DocumentTabViewModel document, FaceAlphaDisplayElement element, SKRect rect, SKPaint hiddenPaint)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            canvas.DrawRect(rect, hiddenPaint);
            return;
        }

        var masks = FaceRuntimeStateResolver.Instance.GetAlphaCellMasks(element, document.RuntimeState);
        var brightness = FaceRuntimeStateResolver.Instance.GetAlphaCellBrightness(element, document.RuntimeState);
        AlphaElementRenderer.RenderAlphaDisplay(
            canvas,
            rect,
            masks,
            brightness,
            element.SegmentDisplayType,
            element.OnColorHex,
            element.OffColorHex,
            element.ShowDecimalPoint,
            element.ShowCommaTail,
            element.IsReversed);
    }

    private static void DrawArtworkElement(SKCanvas canvas, FaceArtworkElement element, PanelViewportTransform viewport, SKPaint hiddenPaint)
    {
        var destination = SKRect.Create((float)element.X, (float)element.Y, (float)Math.Max(0d, element.Width), (float)Math.Max(0d, element.Height));
        if (destination.Width <= 0f || destination.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            canvas.DrawRect(destination, hiddenPaint);
            return;
        }

        if (TryGetArtworkImage(element.AssetPath, out var image))
        {
            var source = ResolveArtworkSourceRect(element, image);
            canvas.DrawImage(image, source, destination);
            return;
        }

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0x33, 0x33, 0x33), IsAntialias = true };
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x66, 0x66, 0x66), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };
        canvas.DrawRect(destination, fillPaint);
        canvas.DrawRect(destination, strokePaint);
    }

    private static SKRect ResolveArtworkSourceRect(FaceArtworkElement element, SKImage image)
    {
        var sourceRegion = element.SourceRegion;
        var sourceBounds = element.Provenance?.SourceElementBounds;
        if (sourceRegion is null || sourceBounds is null || sourceBounds.Width <= 0d || sourceBounds.Height <= 0d)
        {
            return SKRect.Create(0f, 0f, image.Width, image.Height);
        }

        var scaleX = image.Width / sourceBounds.Width;
        var scaleY = image.Height / sourceBounds.Height;
        var x = (sourceRegion.X - sourceBounds.X) * scaleX;
        var y = (sourceRegion.Y - sourceBounds.Y) * scaleY;
        var width = sourceRegion.Width * scaleX;
        var height = sourceRegion.Height * scaleY;
        var left = (float)Math.Clamp(x, 0d, image.Width);
        var top = (float)Math.Clamp(y, 0d, image.Height);
        var right = (float)Math.Clamp(x + width, left, image.Width);
        var bottom = (float)Math.Clamp(y + height, top, image.Height);
        return new SKRect(left, top, right, bottom);
    }

    private static bool TryGetArtworkImage(string? assetPath, out SKImage image)
    {
        image = default!;
        if (!TryResolveAssetPath(assetPath, out var resolvedPath))
        {
            return false;
        }

        var cached = CachedArtworkImages.GetOrAdd(resolvedPath, LoadArtworkImage);
        if (cached is null)
        {
            return false;
        }

        image = cached;
        return true;
    }

    private static SKImage? LoadArtworkImage(string resolvedPath)
    {
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        using var codec = SKCodec.Create(resolvedPath);
        if (codec is null)
        {
            return null;
        }

        using var bitmap = SKBitmap.Decode(codec);
        return bitmap is null ? null : SKImage.FromBitmap(bitmap);
    }

    private static bool TryResolveAssetPath(string? assetPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var candidate = assetPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            resolvedPath = candidate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(PanelElementFactory.ProjectDirectoryPath))
        {
            return false;
        }

        var relativePath = candidate
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        resolvedPath = Path.GetFullPath(Path.Combine(PanelElementFactory.ProjectDirectoryPath, relativePath));
        return true;
    }

    private void OnFaceSkiaSurfaceMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        var document = Document;
        if (document is null || document.Document.DocumentType != EditorDocumentType.Face)
        {
            return;
        }

        if (eventArgs.ChangedButton == MouseButton.Right)
        {
            ShowAddElementContextMenu(eventArgs.GetPosition(FaceSkiaSurface));
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            var pointer = eventArgs.GetPosition(FaceSkiaSurface);
            if (TryGetSelectedElementAtPoint(document, pointer, out var selectedElement)
                && FaceSelectionInteractionService.CanStartGroupMoveFrom(selectedElement, document.SelectionState))
            {
                _isLeftMouseDown = true;
                _isDragSelecting = false;
                _isMovingSelection = true;
                _moveSourceElement = selectedElement;
                _moveSnapshots = FaceElementBulkMoveService.CaptureMovableSelection(document);
                _leftMouseDownStart = pointer;
                _dragSelectionCurrent = pointer;
                FaceSkiaSurface.CaptureMouse();
                eventArgs.Handled = true;
                return;
            }

            _isLeftMouseDown = true;
            _isDragSelecting = false;
            _isMovingSelection = false;
            _leftMouseDownStart = pointer;
            _dragSelectionCurrent = pointer;
            FaceSkiaSurface.CaptureMouse();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = true;
        _panStart = eventArgs.GetPosition(FaceSkiaSurface);
        _panOrigin = new Vector(document.FacePanX, document.FacePanY);
        FaceSkiaSurface.Cursor = Cursors.SizeAll;
        FaceSkiaSurface.CaptureMouse();
        eventArgs.Handled = true;
    }

    private void OnFaceSkiaSurfaceMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_isLeftMouseDown)
        {
            _dragSelectionCurrent = eventArgs.GetPosition(FaceSkiaSurface);
            if (_isMovingSelection)
            {
                UpdateMoveSelectionPreview(_dragSelectionCurrent);
                return;
            }

            if (!_isDragSelecting && Panel2DViewportInteractionService.ShouldStartDragSelection(_leftMouseDownStart, _dragSelectionCurrent, DragSelectionStartThreshold))
            {
                _isDragSelecting = true;
            }

            if (_isDragSelecting) RequestRender();
            return;
        }

        var document = Document;
        if (!_isPanning || document is null)
        {
            return;
        }

        FaceSkiaSurface.Cursor = Cursors.SizeAll;
        var delta = eventArgs.GetPosition(FaceSkiaSurface) - _panStart;
        document.FacePanX = _panOrigin.X + delta.X;
        document.FacePanY = _panOrigin.Y + delta.Y;
        RequestRender();
    }

    private void OnFaceSkiaSurfaceMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            var document = Document;
            if (document is not null)
            {
                if (_isDragSelecting)
                {
                    HandleDragSelection(document, _leftMouseDownStart, _dragSelectionCurrent);
                }
                else if (_isMovingSelection)
                {
                    if (HasDraggedSelection(document, _leftMouseDownStart, _dragSelectionCurrent))
                    {
                        _isCommittingMoveSelection = true;
                        try
                        {
                            HandleMoveSelection(document, _leftMouseDownStart, _dragSelectionCurrent);
                        }
                        finally
                        {
                            _isCommittingMoveSelection = false;
                        }
                    }
                    else
                    {
                        HandleSelectionClick(_leftMouseDownStart);
                    }
                }
                else
                {
                    HandleSelectionClick(_leftMouseDownStart);
                }
            }

            _isLeftMouseDown = false;
            _isDragSelecting = false;
            _isMovingSelection = false;
            _moveSourceElement = null;
            _moveSnapshots = [];
            if (FaceSkiaSurface.IsMouseCaptured) FaceSkiaSurface.ReleaseMouseCapture();
            RequestRender();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        EndPan();
        eventArgs.Handled = true;
    }

    private void OnFaceSkiaSurfaceMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        var document = Document;
        if (document is null)
        {
            return;
        }

        var transform = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY)
            .WithZoomAt(eventArgs.GetPosition(FaceSkiaSurface), eventArgs.Delta);

        document.FaceZoom = transform.Zoom;
        document.FacePanX = transform.PanX;
        document.FacePanY = transform.PanY;
        RequestRender();
        eventArgs.Handled = true;
    }

    private void OnFaceSkiaSurfaceLostMouseCapture(object sender, MouseEventArgs eventArgs)
    {
        if (_isPanning)
        {
            EndPan(releaseMouseCapture: false);
        }

        if (_isLeftMouseDown && _isMovingSelection && !_isCommittingMoveSelection)
        {
            CancelActiveMovePreview();
        }
    }

    private void ShowAddElementContextMenu(Point screenPoint)
    {
        var contextMenu = new ContextMenu
        {
            PlacementTarget = FaceSkiaSurface,
            Placement = PlacementMode.MousePoint
        };

        var menuItem = new MenuItem { Header = "Add Lamp Window" };
        menuItem.Click += (_, _) => AddLampWindowAt(screenPoint);
        contextMenu.Items.Add(menuItem);
        contextMenu.IsOpen = true;
    }

    private void AddLampWindowAt(Point screenPoint)
    {
        var document = Document;
        if (document is null || document.Document.DocumentType != EditorDocumentType.Face)
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        var element = FaceElementFactory.CreateLampWindow(viewport.ScreenToDocument(screenPoint));
        var command = FaceMutationCommands.CreateAddLampWindowCommand(document.DocumentId, document, element);
        document.CommandService.Execute(command);
        RequestRender();
    }

    private void HandleSelectionClick(Point screenPoint)
    {
        var document = Document;
        if (document is null)
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        var selection = FaceSelectionService.SelectFromPoint(document.GetFaceElements(), viewport.ScreenToDocument(screenPoint), document.HierarchySelectedPanelSelection);
        var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (selection is not { } selected)
        {
            if (!isCtrl) document.SelectionState.Clear();
            RequestRender();
            return;
        }

        var item = new EditorSelectionItem(EditorSelectionDomain.FaceElement, selected.ObjectId);
        if (isCtrl)
        {
            document.SelectionState.Toggle(item);
        }
        else if (!document.SelectionState.Items.Contains(item))
        {
            document.SelectionState.Replace(item);
        }
        else
        {
            document.SelectionState.SetPrimary(item);
        }
        RequestRender();
    }

    private static bool HasDraggedSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        return Panel2DViewportInteractionService.HasDocumentDelta(viewport.ScreenToDocument(startScreenPoint), viewport.ScreenToDocument(endScreenPoint));
    }

    private void HandleDragSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        var rect = Panel2DSelectionBoundsService.CreateNormalizedDocumentRect(viewport.ScreenToDocument(startScreenPoint), viewport.ScreenToDocument(endScreenPoint));
        var items = FaceSelectionInteractionService.SelectItemsFromRect(document.GetFaceElements(), rect);
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) document.SelectionState.AddRange(items);
        else document.SelectionState.Replace(items);
    }

    private void DrawDragSelectionRect(SKCanvas canvas, PanelViewportTransform viewport)
    {
        if (!_isLeftMouseDown || !_isDragSelecting) return;
        var rect = Panel2DSelectionBoundsService.CreateNormalizedDocumentRect(viewport.ScreenToDocument(_leftMouseDownStart), viewport.ScreenToDocument(_dragSelectionCurrent));
        using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0x4F, 0xC3, 0xF7, 0x40) };
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x4F, 0xC3, 0xF7, 0xD0), StrokeWidth = (float)(1.5d / viewport.NormalizedZoom), IsAntialias = true };
        canvas.DrawRect((float)rect.Left, (float)rect.Top, (float)rect.Width, (float)rect.Height, fill);
        canvas.DrawRect((float)rect.Left, (float)rect.Top, (float)rect.Width, (float)rect.Height, stroke);
    }

    private void HandleMoveSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        if (_moveSnapshots.Count == 0) return;
        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        var start = viewport.ScreenToDocument(startScreenPoint);
        var end = viewport.ScreenToDocument(endScreenPoint);
        if (!Panel2DViewportInteractionService.HasDocumentDelta(start, end)) return;
        var updated = FaceElementBulkMoveService.ComputeMovedElements(_moveSnapshots, start, end);
        var originals = _moveSnapshots.ToDictionary(snapshot => snapshot.ObjectId, snapshot => snapshot.OriginalElement);
        document.CommandService.Execute(FaceMutationCommands.CreateBulkUpdateElementsCommand(document.DocumentId, document, updated, originals, updated.Count == 1 ? "Move face element" : "Move face elements"));
    }

    private void UpdateMoveSelectionPreview(Point currentScreenPoint)
    {
        var document = Document;
        if (document is null || _moveSnapshots.Count == 0) return;
        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        var updated = FaceElementBulkMoveService.ComputeMovedElements(_moveSnapshots, viewport.ScreenToDocument(_leftMouseDownStart), viewport.ScreenToDocument(currentScreenPoint));
        if (FaceElementPreviewMutationService.TryApplyPreviews(document, updated)) RequestRender();
    }

    private static bool TryGetSelectedElementAtPoint(DocumentTabViewModel document, Point screenPoint, out FaceElementModel selectedElement)
    {
        selectedElement = new FaceLampWindowElement();
        var viewport = new PanelViewportTransform(document.FaceZoom, document.FacePanX, document.FacePanY);
        var documentPoint = viewport.ScreenToDocument(screenPoint);
        foreach (var item in document.SelectionState.Items.Where(item => item.Domain == EditorSelectionDomain.FaceElement).Reverse())
        {
            if (document.TryGetFaceElementByObjectId(item.ObjectId, out var element)
                && documentPoint.X >= element.X && documentPoint.X <= element.X + element.Width
                && documentPoint.Y >= element.Y && documentPoint.Y <= element.Y + element.Height)
            {
                selectedElement = element;
                return true;
            }
        }
        return false;
    }

    private void CancelActiveMovePreview()
    {
        var document = Document;
        if (document is not null && _moveSnapshots.Count > 0)
        {
            var originals = _moveSnapshots.ToDictionary(snapshot => snapshot.ObjectId, snapshot => snapshot.OriginalElement);
            if (FaceElementPreviewMutationService.TryApplyPreviews(document, originals)) RequestRender();
        }
        _isLeftMouseDown = false;
        _isDragSelecting = false;
        _isMovingSelection = false;
        _isCommittingMoveSelection = false;
        _moveSourceElement = null;
        _moveSnapshots = [];
        FaceSkiaSurface.Cursor = Cursors.Arrow;
    }

    private void EndPan(bool releaseMouseCapture = true)
    {
        _isPanning = false;
        if (releaseMouseCapture && FaceSkiaSurface.IsMouseCaptured)
        {
            FaceSkiaSurface.ReleaseMouseCapture();
        }

        FaceSkiaSurface.Cursor = Cursors.Arrow;
    }
}
