using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly IPanel2DRenderer _renderer = new Panel2DRenderer([new BackgroundElementRenderer(), new LampElementRenderer(), new ReelElementRenderer(), new SevenSegmentElementRenderer(), new AlphaElementRenderer()]);

    public SkiaPanel2DEditView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DocumentTabViewModel? Document => DataContext as DocumentTabViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDocumentSubscription(Document);
        EditSkiaSurface.InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateDocumentSubscription(null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateDocumentSubscription(e.NewValue as DocumentTabViewModel);
        EditSkiaSurface.InvalidateVisual();
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
        Dispatcher.Invoke(EditSkiaSurface.InvalidateVisual);
    }

    private void OnDocumentPanelVisualStateChanged(PanelVisualStateChangedEvent _)
    {
        Dispatcher.Invoke(EditSkiaSurface.InvalidateVisual);
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(DocumentTabViewModel.PanelZoom)
            or nameof(DocumentTabViewModel.PanelPanX)
            or nameof(DocumentTabViewModel.PanelPanY))
        {
            EditSkiaSurface.InvalidateVisual();
        }
    }

    private void OnEditSkiaSurfacePaintSurface(object? sender, SKPaintSurfaceEventArgs eventArgs)
    {
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
        var left = selectedElement.X;
        var right = selectedElement.X + selectedElement.Width;
        var top = selectedElement.Y;
        var bottom = selectedElement.Y + selectedElement.Height;

        Span<(double X, double Y)> points =
        [
            (left, top),
            ((left + right) / 2d, top),
            (right, top),
            (left, (top + bottom) / 2d),
            (right, (top + bottom) / 2d),
            (left, bottom),
            ((left + right) / 2d, bottom),
            (right, bottom)
        ];

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

        foreach (var point in points)
        {
            var rect = SKRect.Create(
                (float)(point.X - half),
                (float)(point.Y - half),
                (float)handleSizeDoc,
                (float)handleSizeDoc);
            canvas.DrawRect(rect, fillPaint);
            canvas.DrawRect(rect, strokePaint);
        }
    }

    private void OnEditSkiaSurfaceMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
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

            if (!_isDragSelecting && (_dragSelectionCurrent - _leftMouseDownStart).Length >= DragSelectionStartThreshold)
            {
                _isDragSelecting = true;
            }

            if (_isDragSelecting)
            {
                EditSkiaSurface.InvalidateVisual();
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
        EditSkiaSurface.InvalidateVisual();
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
            EditSkiaSurface.InvalidateVisual();
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
        EditSkiaSurface.InvalidateVisual();
        eventArgs.Handled = true;
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
        var hitElement = document.GetPanelElements()
            .Where(element => element.IsVisible && !element.IsLocked)
            .LastOrDefault(element =>
                documentPoint.X >= element.X
                && documentPoint.X <= element.X + element.Width
                && documentPoint.Y >= element.Y
                && documentPoint.Y <= element.Y + element.Height);

        if (hitElement is null)
        {
            NotifySelection(document, null);
            return;
        }

        NotifySelection(
            document,
            new PanelSelectionInfo(
                hitElement.ObjectId,
                Panel2DDocumentStorage.SerializeElementKind(hitElement.Kind),
                hitElement.X,
                hitElement.Y,
                hitElement.Width,
                hitElement.Height));
    }

    private void NotifySelection(DocumentTabViewModel document, PanelSelectionInfo? selection)
    {
        if (Window.GetWindow(this)?.DataContext is MainWindowViewModel shellViewModel)
        {
            shellViewModel.UpdateDocumentPanelSelection(document.DocumentId, selection);
            return;
        }

        document.HierarchySelectedPanelSelection = selection;
    }

    private void HandleDragSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        var a = viewport.ScreenToDocument(startScreenPoint);
        var b = viewport.ScreenToDocument(endScreenPoint);

        var minX = Math.Min(a.X, b.X);
        var maxX = Math.Max(a.X, b.X);
        var minY = Math.Min(a.Y, b.Y);
        var maxY = Math.Max(a.Y, b.Y);

        var hit = document.GetPanelElements()
            .Where(element => element.IsVisible && !element.IsLocked)
            .LastOrDefault(element =>
                element.X <= maxX
                && (element.X + element.Width) >= minX
                && element.Y <= maxY
                && (element.Y + element.Height) >= minY);

        if (hit is null)
        {
            NotifySelection(document, null);
            return;
        }

        NotifySelection(
            document,
            new PanelSelectionInfo(
                hit.ObjectId,
                Panel2DDocumentStorage.SerializeElementKind(hit.Kind),
                hit.X,
                hit.Y,
                hit.Width,
                hit.Height));
    }

    private void DrawDragSelectionRect(SKCanvas canvas, PanelViewportTransform viewport)
    {
        if (!_isLeftMouseDown || !_isDragSelecting)
        {
            return;
        }

        var start = viewport.ScreenToDocument(_leftMouseDownStart);
        var end = viewport.ScreenToDocument(_dragSelectionCurrent);
        var x = (float)Math.Min(start.X, end.X);
        var y = (float)Math.Min(start.Y, end.Y);
        var width = (float)Math.Abs(end.X - start.X);
        var height = (float)Math.Abs(end.Y - start.Y);

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
        var delta = end - start;
        if (Math.Abs(delta.X) < 0.0001d && Math.Abs(delta.Y) < 0.0001d)
        {
            return;
        }

        var updated = PanelElementModelCloner.Clone(
            _moveSourceElement,
            x: _moveSourceElement.X + delta.X,
            y: _moveSourceElement.Y + delta.Y);
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
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        var x = _resizeSourceElement.X;
        var y = _resizeSourceElement.Y;
        var width = _resizeSourceElement.Width;
        var height = _resizeSourceElement.Height;
        const double minSize = 1d;

        if (_activeResizeHandle is ResizeHandleKind.Left or ResizeHandleKind.TopLeft or ResizeHandleKind.BottomLeft)
        {
            x += dx;
            width -= dx;
        }
        if (_activeResizeHandle is ResizeHandleKind.Right or ResizeHandleKind.TopRight or ResizeHandleKind.BottomRight)
        {
            width += dx;
        }
        if (_activeResizeHandle is ResizeHandleKind.Top or ResizeHandleKind.TopLeft or ResizeHandleKind.TopRight)
        {
            y += dy;
            height -= dy;
        }
        if (_activeResizeHandle is ResizeHandleKind.Bottom or ResizeHandleKind.BottomLeft or ResizeHandleKind.BottomRight)
        {
            height += dy;
        }

        if (width < minSize)
        {
            width = minSize;
        }
        if (height < minSize)
        {
            height = minSize;
        }

        var updated = PanelElementModelCloner.Clone(_resizeSourceElement, x: x, y: y);
        updated.Width = width;
        updated.Height = height;
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
        var half = handleSizeDoc / 2d;
        var left = selectedElement.X;
        var right = selectedElement.X + selectedElement.Width;
        var top = selectedElement.Y;
        var bottom = selectedElement.Y + selectedElement.Height;
        var midX = (left + right) / 2d;
        var midY = (top + bottom) / 2d;

        (ResizeHandleKind Kind, double X, double Y)[] handles =
        [
            (ResizeHandleKind.TopLeft, left, top),
            (ResizeHandleKind.Top, midX, top),
            (ResizeHandleKind.TopRight, right, top),
            (ResizeHandleKind.Left, left, midY),
            (ResizeHandleKind.Right, right, midY),
            (ResizeHandleKind.BottomLeft, left, bottom),
            (ResizeHandleKind.Bottom, midX, bottom),
            (ResizeHandleKind.BottomRight, right, bottom)
        ];

        foreach (var handle in handles)
        {
            if (docPoint.X >= handle.X - half
                && docPoint.X <= handle.X + half
                && docPoint.Y >= handle.Y - half
                && docPoint.Y <= handle.Y + half)
            {
                handleKind = handle.Kind;
                return true;
            }
        }

        return false;
    }

    private enum ResizeHandleKind
    {
        None,
        TopLeft,
        Top,
        TopRight,
        Left,
        Right,
        BottomLeft,
        Bottom,
        BottomRight
    }
}
