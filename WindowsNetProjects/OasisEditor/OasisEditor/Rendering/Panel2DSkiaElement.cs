using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace OasisEditor.Rendering;

public sealed class Panel2DSkiaElement : SKElement
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(DocumentTabViewModel),
            typeof(Panel2DSkiaElement),
            new PropertyMetadata(null, OnDocumentChanged));

    private readonly IPanel2DRenderer _renderer;
    private DocumentTabViewModel? _subscribedDocument;
    private bool _isDocumentSubscribed;
    private bool _isInvalidateQueued;

    public Panel2DSkiaElement()
        : this(Panel2DRendererFactory.CreateDefault())
    {
    }

    internal Panel2DSkiaElement(IPanel2DRenderer renderer)
    {
        _renderer = renderer;
        PaintSurface += OnPaintSurface;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    public DocumentTabViewModel? Document
    {
        get => (DocumentTabViewModel?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not Panel2DSkiaElement element)
        {
            return;
        }

        element.UpdateDocumentSubscription(element.GetEffectiveDocument());
        element.RequestInvalidate();
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        UpdateDocumentSubscription(GetEffectiveDocument());
        RequestInvalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        UpdateDocumentSubscription(null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (Document is not null)
        {
            return;
        }

        UpdateDocumentSubscription(GetEffectiveDocument());
        RequestInvalidate();
    }

    private DocumentTabViewModel? GetEffectiveDocument()
    {
        return Document ?? DataContext as DocumentTabViewModel;
    }

    private void UpdateDocumentSubscription(DocumentTabViewModel? document)
    {
        if (_subscribedDocument is not null && _isDocumentSubscribed)
        {
            _subscribedDocument.PanelVisualStateChanged -= OnDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged -= OnDocumentPropertyChanged;
            _isDocumentSubscribed = false;
        }

        _subscribedDocument = document;

        if (_subscribedDocument is not null && IsLoaded)
        {
            _subscribedDocument.PanelVisualStateChanged += OnDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged += OnDocumentPropertyChanged;
            _isDocumentSubscribed = true;
        }
    }

    private void OnDocumentPanelVisualStateChanged(PanelVisualStateChangedEvent visualStateChanged)
    {
        if (_subscribedDocument is null || visualStateChanged.DocumentId != _subscribedDocument.DocumentId)
        {
            return;
        }

        RequestInvalidate();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not (
            nameof(DocumentTabViewModel.PanelLayoutJson)
            or nameof(DocumentTabViewModel.PanelZoom)
            or nameof(DocumentTabViewModel.PanelPanX)
            or nameof(DocumentTabViewModel.PanelPanY)))
        {
            return;
        }

        RequestInvalidate();
    }

    private void RequestInvalidate()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(RequestInvalidate));
            return;
        }

        if (_isInvalidateQueued)
        {
            return;
        }

        _isInvalidateQueued = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                _isInvalidateQueued = false;
                InvalidateVisual();
            }));
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs eventArgs)
    {
        var canvas = eventArgs.Surface.Canvas;
        canvas.Clear(new SKColor(0x1E, 0x1E, 0x1E));

        var document = GetEffectiveDocument();
        if (document is null || document.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        canvas.Save();
        canvas.Translate((float)viewport.PanX, (float)viewport.PanY);
        canvas.Scale((float)viewport.NormalizedZoom, (float)viewport.NormalizedZoom);
        _renderer.Render(canvas, document.GetPanelElements(), document.RuntimeState, viewport);
        canvas.Restore();
    }
}
