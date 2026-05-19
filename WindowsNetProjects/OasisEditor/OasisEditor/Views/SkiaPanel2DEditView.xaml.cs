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
    private DocumentTabViewModel? _subscribedDocument;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;
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

    private void OnEditSkiaSurfaceMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            HandleSelectionClick(eventArgs.GetPosition(EditSkiaSurface));
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
}
