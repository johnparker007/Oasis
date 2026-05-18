using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using OasisEditor.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace OasisEditor.Views;

public partial class PanelCanvasView : UserControl
{
    private static readonly DependencyProperty EditSkiaSurfaceSubscriptionProperty =
        DependencyProperty.RegisterAttached(
            "EditSkiaSurfaceSubscription",
            typeof(EditSkiaSurfaceSubscription),
            typeof(PanelCanvasView),
            new PropertyMetadata(null));

    private readonly IPanel2DRenderer _editSkiaRenderer = Panel2DRendererFactory.CreateDefault();

    public PanelCanvasView()
    {
        InitializeComponent();
    }

    private void OnEditSkiaSurfacePaintSurface(object? sender, SKPaintSurfaceEventArgs eventArgs)
    {
        var canvas = eventArgs.Surface.Canvas;
        canvas.Clear(new SKColor(0x1E, 0x1E, 0x1E));

        if (sender is not SKElement skiaSurface
            || skiaSurface.DataContext is not DocumentTabViewModel document
            || document.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        var viewport = new PanelViewportTransform(document.PanelZoom, document.PanelPanX, document.PanelPanY);
        canvas.Save();
        canvas.Translate((float)viewport.PanX, (float)viewport.PanY);
        canvas.Scale((float)viewport.NormalizedZoom, (float)viewport.NormalizedZoom);
        _editSkiaRenderer.Render(canvas, document.GetPanelElements(), document.RuntimeState, viewport);
        canvas.Restore();
    }

    private void OnEditSkiaSurfaceLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not SKElement skiaSurface)
        {
            return;
        }

        skiaSurface.DataContextChanged += OnEditSkiaSurfaceDataContextChanged;
        SubscribeEditSkiaSurface(skiaSurface, skiaSurface.DataContext as DocumentTabViewModel);
        skiaSurface.InvalidateVisual();
    }

    private void OnEditSkiaSurfaceUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not SKElement skiaSurface)
        {
            return;
        }

        skiaSurface.DataContextChanged -= OnEditSkiaSurfaceDataContextChanged;
        SubscribeEditSkiaSurface(skiaSurface, null);
    }

    private void OnEditSkiaSurfaceDataContextChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (sender is not SKElement skiaSurface)
        {
            return;
        }

        SubscribeEditSkiaSurface(skiaSurface, eventArgs.NewValue as DocumentTabViewModel);
        skiaSurface.InvalidateVisual();
    }

    private static void SubscribeEditSkiaSurface(SKElement skiaSurface, DocumentTabViewModel? document)
    {
        if (skiaSurface.GetValue(EditSkiaSurfaceSubscriptionProperty) is EditSkiaSurfaceSubscription previousSubscription)
        {
            previousSubscription.Dispose();
            skiaSurface.ClearValue(EditSkiaSurfaceSubscriptionProperty);
        }

        if (document is null)
        {
            return;
        }

        skiaSurface.SetValue(EditSkiaSurfaceSubscriptionProperty, new EditSkiaSurfaceSubscription(skiaSurface, document));
    }

    private sealed class EditSkiaSurfaceSubscription : IDisposable
    {
        private readonly SKElement _skiaSurface;
        private readonly DocumentTabViewModel _document;
        private bool _isDisposed;

        public EditSkiaSurfaceSubscription(SKElement skiaSurface, DocumentTabViewModel document)
        {
            _skiaSurface = skiaSurface;
            _document = document;
            _document.PanelVisualStateChanged += OnPanelVisualStateChanged;
            _document.PropertyChanged += OnDocumentPropertyChanged;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _document.PanelVisualStateChanged -= OnPanelVisualStateChanged;
            _document.PropertyChanged -= OnDocumentPropertyChanged;
            _isDisposed = true;
        }

        private void OnPanelVisualStateChanged(PanelVisualStateChangedEvent visualStateChanged)
        {
            if (visualStateChanged.DocumentId != _document.DocumentId)
            {
                return;
            }

            InvalidateSurface();
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

            InvalidateSurface();
        }

        private void InvalidateSurface()
        {
            _skiaSurface.Dispatcher.Invoke(_skiaSurface.InvalidateVisual);
        }
    }
}
