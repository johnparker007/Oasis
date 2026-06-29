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
    private PanelFaceSourceShapeModel? _shapeDragSource;
    private int _activeShapeCornerIndex = -1;
    private ResizeHandleKind _activeResizeHandle;
    private bool _isRenderQueued;
    private bool _isRenderDirty;
    private readonly Stopwatch _renderStopwatch = Stopwatch.StartNew();
    private readonly DispatcherTimer _renderThrottleTimer;
    private const double TargetFrameMillis = 16.0;
    private readonly IPanel2DRenderer _renderer = new Panel2DRenderer([new BackgroundElementRenderer(), new LampElementRenderer(), new ReelElementRenderer(), new SevenSegmentElementRenderer(), new AlphaElementRenderer(), new VfdDotMatrixElementRenderer()], "EditView");

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
        DrawFaceSourceShapes(canvas, document, viewport);
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
                && TryGetFaceSourceShapeCornerAtPoint(document, pointer, out var shape, out var cornerIndex))
            {
                _isLeftMouseDown = true;
                _isDragSelecting = false;
                _isMovingSelection = false;
                _isResizingSelection = true;
                _shapeDragSource = shape;
                _activeShapeCornerIndex = cornerIndex;
                _leftMouseDownStart = pointer;
                _dragSelectionCurrent = pointer;
                EditSkiaSurface.CaptureMouse();
                eventArgs.Handled = true;
                return;
            }

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
        EditSkiaSurface.Cursor = Cursors.SizeAll;
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
                UpdateMoveSelectionPreview(_dragSelectionCurrent);
                return;
            }

            if (_isResizingSelection)
            {
                RequestRender();
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

        EditSkiaSurface.Cursor = Cursors.SizeAll;
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
                    if (HasDraggedSelection(document, _leftMouseDownStart, _dragSelectionCurrent))
                    {
                        HandleMoveSelection(document, _leftMouseDownStart, _dragSelectionCurrent);
                    }
                    else
                    {
                        HandleSelectionClick(_leftMouseDownStart);
                    }
                }
                else if (_isResizingSelection)
                {
                    if (_shapeDragSource is not null)
                    {
                        HandleShapeCornerDrag(document, _dragSelectionCurrent);
                    }
                    else
                    {
                        HandleResizeSelection(document, _leftMouseDownStart, _dragSelectionCurrent);
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
            _isResizingSelection = false;
            _moveSourceElement = null;
            _resizeSourceElement = null;
            _shapeDragSource = null;
            _activeShapeCornerIndex = -1;
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

        EndPan();
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

    private void OnEditSkiaSurfaceLostMouseCapture(object sender, MouseEventArgs eventArgs)
    {
        if (_isPanning)
        {
            EndPan(releaseMouseCapture: false);
        }
    }

    private void EndPan(bool releaseMouseCapture = true)
    {
        _isPanning = false;
        if (releaseMouseCapture && EditSkiaSurface.IsMouseCaptured)
        {
            EditSkiaSurface.ReleaseMouseCapture();
        }

        EditSkiaSurface.Cursor = Cursors.Arrow;
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

        var shapeAtPoint = document.GetPanelFaceSourceShapes().LastOrDefault(shape => IsInsideShapeBounds(shape, panelPoint));
        if (shapeAtPoint is not null)
        {
            document.HierarchySelectedPanelSelection = PanelFaceSourceShapeCommands.ToSelection(shapeAtPoint);
        }

        if ((document.HierarchySelectedPanelSelection is { Kind: PanelFaceSourceShapeCommands.SelectionKind } || shapeAtPoint is not null)
            && Window.GetWindow(this)?.DataContext is MainWindowViewModel mainWindow)
        {
            var createFaceItem = new MenuItem { Header = "Create Face from Face Source Shape", Command = mainWindow.GenerateFaceFromSourceShapeCommand };
            contextMenu.Items.Add(createFaceItem);
            contextMenu.Items.Add(new Separator());
        }

        var sourceShapeItem = new MenuItem { Header = "Add Face Source Shape" };
        sourceShapeItem.Click += (_, _) => AddFaceSourceShapeAt(panelPoint);
        contextMenu.Items.Add(sourceShapeItem);
        contextMenu.Items.Add(new Separator());

        AddAddElementMenuItem(contextMenu, "Add Lamp", AddablePanelElementKind.Lamp, panelPoint);
        AddAddElementMenuItem(contextMenu, "Add Reel", AddablePanelElementKind.Reel, panelPoint);
        AddAddElementMenuItem(contextMenu, "Add 7 Segment Display", AddablePanelElementKind.SevenSegmentDisplay, panelPoint);
        AddAddElementMenuItem(contextMenu, "Add Segment Alpha", AddablePanelElementKind.SegmentAlpha, panelPoint);
        AddAddElementMenuItem(contextMenu, "Add VFD Dot Matrix", AddablePanelElementKind.VfdDotMatrix, panelPoint);

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

    private void AddFaceSourceShapeAt(Point panelPoint)
    {
        var document = Document;
        if (document is null || document.Document.DocumentType != EditorDocumentType.Panel2D) return;
        var shape = new PanelFaceSourceShapeModel
        {
            Id = $"faceSourceShape-{Guid.NewGuid():N}",
            Name = "Face Source Shape",
            TopLeft = new FacePointModel { X = panelPoint.X, Y = panelPoint.Y },
            TopRight = new FacePointModel { X = panelPoint.X + 300, Y = panelPoint.Y },
            BottomRight = new FacePointModel { X = panelPoint.X + 300, Y = panelPoint.Y + 200 },
            BottomLeft = new FacePointModel { X = panelPoint.X, Y = panelPoint.Y + 200 }
        };
        document.CommandService.Execute(PanelFaceSourceShapeCommands.CreateAddCommand(document.DocumentId, document, shape));
        RequestRender();
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
        var shapeSelection = document.GetPanelFaceSourceShapes().LastOrDefault(shape => IsInsideShapeBounds(shape, documentPoint));
        var selection = shapeSelection is not null
            ? PanelFaceSourceShapeCommands.ToSelection(shapeSelection)
            : Panel2DSelectionService.SelectFromPoint(
                document.GetPanelElements(),
                documentPoint,
                document.HierarchySelectedPanelSelection);
        NotifySelection(document, selection);
    }

    private static bool HasDraggedSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var start = viewport.ScreenToDocument(startScreenPoint);
        var end = viewport.ScreenToDocument(endScreenPoint);
        return Panel2DViewportInteractionService.HasDocumentDelta(start, end);
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
            _moveSourceElement,
            "Move element");
        document.CommandService.Execute(moveCommand);
    }

    private void UpdateMoveSelectionPreview(Point currentScreenPoint)
    {
        var document = Document;
        if (document is null || _moveSourceElement is null || string.IsNullOrWhiteSpace(_moveSourceElement.ObjectId))
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var start = viewport.ScreenToDocument(_leftMouseDownStart);
        var current = viewport.ScreenToDocument(currentScreenPoint);
        var updated = Panel2DMoveComputationService.ComputeMovedElement(_moveSourceElement, start, current);
        if (PanelElementPreviewMutationService.TryApplyPreview(document, _moveSourceElement.ObjectId, updated))
        {
            RequestRender();
        }
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

    private void DrawFaceSourceShapes(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        using var linePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0xFF, 0xC1, 0x07), StrokeWidth = (float)(2d / viewport.NormalizedZoom), IsAntialias = true };
        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xC1, 0x07, 0x28), IsAntialias = true };
        using var handlePaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xA0, 0x00), IsAntialias = true };
        foreach (var sourceShape in document.GetPanelFaceSourceShapes())
        {
            var shape = GetPreviewShape(sourceShape, viewport);
            using var path = new SKPath();
            path.MoveTo((float)shape.TopLeft.X, (float)shape.TopLeft.Y);
            path.LineTo((float)shape.TopRight.X, (float)shape.TopRight.Y);
            path.LineTo((float)shape.BottomRight.X, (float)shape.BottomRight.Y);
            path.LineTo((float)shape.BottomLeft.X, (float)shape.BottomLeft.Y);
            path.Close();
            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, linePaint);
            var radius = (float)(5d / viewport.NormalizedZoom);
            foreach (var point in GetShapePoints(shape)) canvas.DrawCircle((float)point.X, (float)point.Y, radius, handlePaint);
        }
    }


    private PanelFaceSourceShapeModel GetPreviewShape(PanelFaceSourceShapeModel sourceShape, PanelViewportTransform viewport)
    {
        if (!_isResizingSelection
            || _shapeDragSource is null
            || _activeShapeCornerIndex < 0
            || !string.Equals(sourceShape.Id, _shapeDragSource.Id, StringComparison.Ordinal))
        {
            return sourceShape;
        }

        var point = viewport.ScreenToDocument(_dragSelectionCurrent);
        return CreateShapeWithCorner(sourceShape, _activeShapeCornerIndex, new FacePointModel { X = point.X, Y = point.Y });
    }

    private static PanelFaceSourceShapeModel CreateShapeWithCorner(PanelFaceSourceShapeModel source, int cornerIndex, FacePointModel point)
    {
        return new PanelFaceSourceShapeModel
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            TopLeft = cornerIndex == 0 ? point : source.TopLeft,
            TopRight = cornerIndex == 1 ? point : source.TopRight,
            BottomRight = cornerIndex == 2 ? point : source.BottomRight,
            BottomLeft = cornerIndex == 3 ? point : source.BottomLeft
        };
    }

    private static bool IsInsideShapeBounds(PanelFaceSourceShapeModel shape, Point point)
    {
        return point.X >= shape.X && point.X <= shape.X + shape.Width && point.Y >= shape.Y && point.Y <= shape.Y + shape.Height;
    }

    private static FacePointModel[] GetShapePoints(PanelFaceSourceShapeModel shape) => [shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft];

    private bool TryGetFaceSourceShapeCornerAtPoint(DocumentTabViewModel document, Point screenPoint, out PanelFaceSourceShapeModel shape, out int cornerIndex)
    {
        shape = new PanelFaceSourceShapeModel();
        cornerIndex = -1;
        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var docPoint = viewport.ScreenToDocument(screenPoint);
        var hitRadius = 8d / viewport.NormalizedZoom;
        foreach (var candidate in document.GetPanelFaceSourceShapes().Reverse())
        {
            var points = GetShapePoints(candidate);
            for (var i = 0; i < points.Length; i++)
            {
                var dx = points[i].X - docPoint.X;
                var dy = points[i].Y - docPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) <= hitRadius)
                {
                    shape = candidate;
                    cornerIndex = i;
                    document.HierarchySelectedPanelSelection = PanelFaceSourceShapeCommands.ToSelection(candidate);
                    return true;
                }
            }
        }
        return false;
    }

    private void HandleShapeCornerDrag(DocumentTabViewModel document, Point screenPoint)
    {
        if (_shapeDragSource is null || _activeShapeCornerIndex < 0) return;
        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var point = viewport.ScreenToDocument(screenPoint);
        FacePointModel fp = new() { X = point.X, Y = point.Y };
        var updated = CreateShapeWithCorner(_shapeDragSource, _activeShapeCornerIndex, fp);
        document.CommandService.Execute(PanelFaceSourceShapeCommands.CreateUpdateCommand(document.DocumentId, document, updated, "Move Face Source Shape corner"));
    }

}
