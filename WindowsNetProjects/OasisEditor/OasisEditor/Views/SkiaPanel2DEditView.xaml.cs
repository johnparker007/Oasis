using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using OasisEditor.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace OasisEditor.Views;

public partial class SkiaPanel2DEditView : UserControl
{
    private const double DragSelectionStartThreshold = 4d;
    private const double ResizeHandleScreenSize = 10d;
    private DocumentTabViewModel? _subscribedDocument;
    private bool _isPanning;
    private bool _isLeftMouseDown;
    private bool _isDragSelecting;
    private bool _isMovingSelection;
    private bool _isResizingSelection;
    private Point _panStart;
    private Vector _panOrigin;
    private Point _leftMouseDownStart;
    private Point _dragSelectionCurrent;
    private PanelElementModel? _moveSourceElement;
    private PanelElementModel? _resizeSourceElement;
    private ResizeHandleKind _activeResizeHandle;
    private bool _isRenderQueued;
    private bool _isRenderDirty;
    private readonly Stopwatch _renderStopwatch = Stopwatch.StartNew();
    private readonly DispatcherTimer _renderThrottleTimer;
    private const double TargetFrameMillis = 16.0;
    private readonly IPanel2DRenderer _renderer = new Panel2DRenderer([new BackgroundElementRenderer(), new LampElementRenderer(), new ReelElementRenderer(), new SevenSegmentElementRenderer(), new AlphaElementRenderer()], "EditView");

    public SkiaPanel2DEditView()
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
        if (!ReferenceEquals(_subscribedDocument, null))
        {
            _subscribedDocument!.PanelChanged -= OnDocumentPanelChanged;
            _subscribedDocument.PanelVisualStateChanged -= OnDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _subscribedDocument = next;
        if (_subscribedDocument is null)
        {
            return;
        }

        _subscribedDocument.PanelChanged += OnDocumentPanelChanged;
        _subscribedDocument.PanelVisualStateChanged += OnDocumentPanelVisualStateChanged;
        _subscribedDocument.PropertyChanged += OnDocumentPropertyChanged;
    }

    private void OnDocumentPanelChanged(PanelChangeEvent _)
    {
        RequestRender();
    }

    private void OnDocumentPanelVisualStateChanged(PanelVisualStateChangedEvent _)
    {
        RequestRender();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(DocumentTabViewModel.PanelZoom)
            or nameof(DocumentTabViewModel.PanelPanX)
            or nameof(DocumentTabViewModel.PanelPanY))
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
            EditSkiaSurface.InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    private void OnEditSkiaSurfacePaintSurface(object? sender, SKPaintSurfaceEventArgs eventArgs)
    {
        _isRenderDirty = false;
        var canvas = eventArgs.Surface.Canvas;
        canvas.Clear(new SKColor(0x1E, 0x1E, 0x1E));

        var document = Document;
        if (document is null || document.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        canvas.Save();
        canvas.Translate((float)viewport.PanX, (float)viewport.PanY);
        canvas.Scale((float)viewport.NormalizedZoom, (float)viewport.NormalizedZoom);
        _renderer.Render(canvas, document.GetPanelElements(), document.RuntimeState, viewport);
        DrawSelectionOutline(canvas, document, viewport);
        DrawResizeHandles(canvas, document, viewport);
        DrawDragSelectionRect(canvas, viewport);
        canvas.Restore();
    }

    private static void DrawSelectionOutline(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        var selection = document.HierarchySelectedPanelSelection;
        if (selection is null)
        {
            return;
        }

        if (!document.TryGetPanelElement(selection.Value, out var selectedElement))
        {
            return;
        }

        using var selectionPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x4F, 0xC3, 0xF7),
            StrokeWidth = (float)(2d / viewport.NormalizedZoom),
            IsAntialias = true
        };

        canvas.DrawRect(
            (float)selectedElement.X,
            (float)selectedElement.Y,
            (float)Math.Max(0d, selectedElement.Width),
            (float)Math.Max(0d, selectedElement.Height),
            selectionPaint);
    }

    private static void DrawResizeHandles(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        var selection = document.HierarchySelectedPanelSelection;
        if (selection is null)
        {
            return;
        }

        if (!document.TryGetPanelElement(selection.Value, out var selectedElement))
        {
            return;
        }

        var handleSizeDoc = ResizeHandleScreenSize / viewport.NormalizedZoom;
        var half = handleSizeDoc / 2d;
        var handles = Panel2DResizeHandleService.GetHandles(selectedElement);

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x1E, 0x88, 0xE5),
            IsAntialias = true
        };
        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.White,
            StrokeWidth = (float)(1d / viewport.NormalizedZoom),
            IsAntialias = true
        };

        foreach (var handle in handles)
        {
            var rect = SKRect.Create(
                (float)(handle.X - half),
                (float)(handle.Y - half),
                (float)handleSizeDoc,
                (float)handleSizeDoc);
            canvas.DrawRect(rect, fillPaint);
            canvas.DrawRect(rect, strokePaint);
        }
    }

    private void OnEditSkiaSurfaceMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton == MouseButton.Right)
        {
            ShowAddElementContextMenu(eventArgs.GetPosition(EditSkiaSurface));
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            var document = Document;
            var pointer = eventArgs.GetPosition(EditSkiaSurface);
            if (document is not null
                && TryGetResizeHandleAtPoint(document, pointer, out var resizeHandle, out var resizeElement))
            {
                _isLeftMouseDown = true;
                _isDragSelecting = false;
                _isMovingSelection = false;
                _isResizingSelection = true;
                _resizeSourceElement = resizeElement;
                _activeResizeHandle = resizeHandle;
                _leftMouseDownStart = pointer;
                _dragSelectionCurrent = pointer;
                EditSkiaSurface.CaptureMouse();
                eventArgs.Handled = true;
                return;
            }

            if (document is not null
                && TryGetSelectedElement(document, out var selectedElement)
                && IsPointInsideElement(pointer, selectedElement, new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY)))
            {
                _isLeftMouseDown = true;
                _isDragSelecting = false;
                _isMovingSelection = true;
                _isResizingSelection = false;
                _moveSourceElement = selectedElement;
                _leftMouseDownStart = pointer;
                _dragSelectionCurrent = pointer;
                EditSkiaSurface.CaptureMouse();
                eventArgs.Handled = true;
                return;
            }

            _isLeftMouseDown = true;
            _isDragSelecting = false;
            _isMovingSelection = false;
            _isResizingSelection = false;
            _leftMouseDownStart = eventArgs.GetPosition(EditSkiaSurface);
            _dragSelectionCurrent = _leftMouseDownStart;
            EditSkiaSurface.CaptureMouse();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Middle || Document is null)
        {
            return;
        }

        _isPanning = true;
        _panStart = eventArgs.GetPosition(EditSkiaSurface);
        _panOrigin = new Vector(Document.PanelPanX, Document.PanelPanY);
        EditSkiaSurface.CaptureMouse();
        eventArgs.Handled = true;
    }

    private void OnEditSkiaSurfaceMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_isLeftMouseDown)
        {
            _dragSelectionCurrent = eventArgs.GetPosition(EditSkiaSurface);
            if (_isMovingSelection)
            {
                return;
            }

            if (!_isDragSelecting && Panel2DViewportInteractionService.ShouldStartDragSelection(_leftMouseDownStart, _dragSelectionCurrent, DragSelectionStartThreshold))
            {
                _isDragSelecting = true;
            }

            if (_isDragSelecting)
            {
                RequestRender();
            }

            return;
        }

        if (!_isPanning || Document is null)
        {
            return;
        }

        var delta = eventArgs.GetPosition(EditSkiaSurface) - _panStart;
        Document.PanelPanX = _panOrigin.X + delta.X;
        Document.PanelPanY = _panOrigin.Y + delta.Y;
        RequestRender();
    }

    private void OnEditSkiaSurfaceMouseUp(object sender, MouseButtonEventArgs eventArgs)
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
                    HandleMoveSelection(document, _leftMouseDownStart, _dragSelectionCurrent);
                }
                else if (_isResizingSelection)
                {
                    HandleResizeSelection(document, _leftMouseDownStart, _dragSelectionCurrent);
                }
                else
                {
                    HandleSelectionClick(_leftMouseDownStart);
                }
            }

            _isLeftMouseDown = false;
            _isDragSelecting = false;
            _isMovingSelection = false;
            _isResizingSelection = false;
            _moveSourceElement = null;
            _resizeSourceElement = null;
            _activeResizeHandle = ResizeHandleKind.None;
            EditSkiaSurface.ReleaseMouseCapture();
            RequestRender();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = false;
        EditSkiaSurface.ReleaseMouseCapture();
        eventArgs.Handled = true;
    }

    private void OnEditSkiaSurfaceMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        if (Document is null)
        {
            return;
        }

        var transform = new PanelViewportTransform(Document.PanelZoom, Document.PanelPanX, Document.PanelPanY)
            .WithZoomAt(eventArgs.GetPosition(EditSkiaSurface), eventArgs.Delta);

        Document.PanelZoom = transform.Zoom;
        Document.PanelPanX = transform.PanX;
        Document.PanelPanY = transform.PanY;
        RequestRender();
        eventArgs.Handled = true;
    }


    private void ShowAddElementContextMenu(Point screenPoint)
    {
        var document = Document;
        if (document is null || document.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var panelPoint = viewport.ScreenToDocument(screenPoint);
        var contextMenu = new ContextMenu
        {
            PlacementTarget = EditSkiaSurface,
            Placement = PlacementMode.MousePoint
        };

        AddAddElementMenuItem(contextMenu, "Add Lamp", AddablePanelElementKind.Lamp, panelPoint);
        AddAddElementMenuItem(contextMenu, "Add Reel", AddablePanelElementKind.Reel, panelPoint);
        AddAddElementMenuItem(contextMenu, "Add 7 Segment Display", AddablePanelElementKind.SevenSegmentDisplay, panelPoint);
        AddAddElementMenuItem(contextMenu, "Add Segment Alpha", AddablePanelElementKind.SegmentAlpha, panelPoint);

        contextMenu.IsOpen = true;
    }

    private void AddAddElementMenuItem(ContextMenu contextMenu, string header, AddablePanelElementKind kind, Point panelPoint)
    {
        var menuItem = new MenuItem
        {
            Header = header
        };
        menuItem.Click += (_, _) => AddElementAt(kind, panelPoint);
        contextMenu.Items.Add(menuItem);
    }

    private void AddElementAt(AddablePanelElementKind kind, Point panelPoint)
    {
        var document = Document;
        if (document is null || document.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        var element = PanelElementFactory.CreateAddableElement(kind, panelPoint);
        var command = CanvasMutationCommands.CreateAddPanelElementCommand(document.DocumentId, document, element);
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

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var documentPoint = viewport.ScreenToDocument(screenPoint);
        var selection = Panel2DSelectionService.SelectFromPoint(document.GetPanelElements(), documentPoint);
        NotifySelection(document, selection);
    }

    private void NotifySelection(DocumentTabViewModel document, PanelSelectionInfo? selection)
    {
        Panel2DSelectionNotificationService.NotifySelection(this, document, selection);
    }

    private void HandleDragSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var a = viewport.ScreenToDocument(startScreenPoint);
        var b = viewport.ScreenToDocument(endScreenPoint);
        var rect = Panel2DSelectionBoundsService.CreateNormalizedDocumentRect(a, b);
        var selection = Panel2DSelectionService.SelectFromRect(
            document.GetPanelElements(),
            rect.Left,
            rect.Top,
            rect.Right,
            rect.Bottom);
        NotifySelection(document, selection);
    }

    private void DrawDragSelectionRect(SKCanvas canvas, PanelViewportTransform viewport)
    {
        if (!_isLeftMouseDown || !_isDragSelecting)
        {
            return;
        }

        var start = viewport.ScreenToDocument(_leftMouseDownStart);
        var end = viewport.ScreenToDocument(_dragSelectionCurrent);
        var rect = Panel2DSelectionBoundsService.CreateNormalizedDocumentRect(start, end);
        var x = (float)rect.Left;
        var y = (float)rect.Top;
        var width = (float)rect.Width;
        var height = (float)rect.Height;

        using var fill = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x4F, 0xC3, 0xF7, 0x40)
        };
        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x4F, 0xC3, 0xF7, 0xD0),
            StrokeWidth = (float)(1.5d / viewport.NormalizedZoom),
            IsAntialias = true
        };

        canvas.DrawRect(x, y, width, height, fill);
        canvas.DrawRect(x, y, width, height, stroke);
    }

    private void HandleMoveSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        if (_moveSourceElement is null || string.IsNullOrWhiteSpace(_moveSourceElement.ObjectId))
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var start = viewport.ScreenToDocument(startScreenPoint);
        var end = viewport.ScreenToDocument(endScreenPoint);
        if (!Panel2DViewportInteractionService.HasDocumentDelta(start, end))
        {
            return;
        }

        var updated = Panel2DMoveComputationService.ComputeMovedElement(_moveSourceElement, start, end);
        var moveCommand = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            _moveSourceElement.ObjectId,
            updated,
            "Move element");
        document.CommandService.Execute(moveCommand);
    }

    private static bool TryGetSelectedElement(DocumentTabViewModel document, out PanelElementModel selectedElement)
    {
        selectedElement = new PanelElementModel();
        var selection = document.HierarchySelectedPanelSelection;
        if (selection is null)
        {
            return false;
        }

        return document.TryGetPanelElement(selection.Value, out selectedElement);
    }

    private static bool IsPointInsideElement(Point screenPoint, PanelElementModel element, PanelViewportTransform viewport)
    {
        var docPoint = viewport.ScreenToDocument(screenPoint);
        return docPoint.X >= element.X
               && docPoint.X <= element.X + element.Width
               && docPoint.Y >= element.Y
               && docPoint.Y <= element.Y + element.Height;
    }

    private void HandleResizeSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        if (_resizeSourceElement is null || _activeResizeHandle == ResizeHandleKind.None || string.IsNullOrWhiteSpace(_resizeSourceElement.ObjectId))
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var start = viewport.ScreenToDocument(startScreenPoint);
        var end = viewport.ScreenToDocument(endScreenPoint);
        var updated = Panel2DResizeComputationService.ComputeResizedElement(_resizeSourceElement, _activeResizeHandle, start, end);
        var command = CanvasMutationCommands.CreateUpdateElementCommand(document.DocumentId, document, _resizeSourceElement.ObjectId, updated, "Resize element");
        document.CommandService.Execute(command);
    }

    private bool TryGetResizeHandleAtPoint(DocumentTabViewModel document, Point screenPoint, out ResizeHandleKind handleKind, out PanelElementModel selectedElement)
    {
        handleKind = ResizeHandleKind.None;
        selectedElement = new PanelElementModel();
        if (!TryGetSelectedElement(document, out selectedElement))
        {
            return false;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var docPoint = viewport.ScreenToDocument(screenPoint);
        var handleSizeDoc = ResizeHandleScreenSize / viewport.NormalizedZoom;
        return Panel2DResizeHandleHitTestService.TryHitHandle(selectedElement, docPoint, handleSizeDoc, out handleKind);
    }
}
