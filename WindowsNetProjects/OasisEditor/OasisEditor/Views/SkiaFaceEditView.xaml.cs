using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
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
    private CalibrationSampleModel? _markerPreviewPoint;
    private Vector _panOrigin;
    private readonly Stopwatch _renderStopwatch = Stopwatch.StartNew();
    private readonly DispatcherTimer _renderThrottleTimer;

    internal static void InvalidateArtworkImage(string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || !ProjectAssetPathResolver.TryResolveAssetPath(assetPath, out var resolvedPath)) return;
        if (CachedArtworkImages.TryRemove(resolvedPath, out var image)) image?.Dispose();
    }

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

    private (Rect Viewport, Rect Bounds, DpiScale Dpi) NavigationGeometry(DocumentTabViewModel document) =>
        (new Rect(0, 0, FaceSkiaSurface.ActualWidth, FaceSkiaSurface.ActualHeight),
         EditorLogicalViewportBounds.Face(document), VisualTreeHelper.GetDpi(FaceSkiaSurface));

    private EditorViewportTransform CoreViewport(DocumentTabViewModel document) => new(document.FaceZoom, document.FacePanX, document.FacePanY);

    private static EditorViewportContentScale ContentScale(DocumentTabViewModel document)
    {
        var artwork = document.GetFaceElements().OfType<FaceArtworkElement>().FirstOrDefault(e => e.Width > 0d && e.Height > 0d);
        return artwork is not null && TryGetArtworkImage(artwork.AssetPath, out var image)
            ? FaceArtworkRasterMapping.ContentScale(artwork, image.Width, image.Height)
            : EditorViewportContentScale.Identity;
    }

    private PanelViewportTransform CreateViewport(DocumentTabViewModel document)
    {
        var (viewport, bounds, dpi) = NavigationGeometry(document);
        return PanelViewportTransform.FromEditorNavigation(CoreViewport(document), viewport, bounds, dpi.DpiScaleX,
            dpi.DpiScaleY, ContentScale(document));
    }

    private PanelViewportTransform CreateRenderViewport(DocumentTabViewModel document)
    {
        var (viewport, bounds, dpi) = NavigationGeometry(document);
        return PanelViewportTransform.FromEditor(CoreViewport(document), viewport, bounds, dpi.DpiScaleX, dpi.DpiScaleY,
            ContentScale(document));
    }

    private void SetViewport(EditorViewportTransform transform)
    {
        if (Document is not { } document) return;
        document.FaceZoom = transform.ClampedZoom; document.FacePanX = transform.PanX; document.FacePanY = transform.PanY;
        ViewportStatus.Zoom = transform.ClampedZoom;
    }

    private void OnFitRequested(object? sender, EventArgs e)
    {
        if (Document is not { } document) return;
        var (viewport, bounds, dpi) = NavigationGeometry(document);
        SetViewport(EditorViewportTransform.Fit(viewport, bounds, dpi.DpiScaleX, dpi.DpiScaleY, ContentScale(document)));
    }

    private void OnZoomRequested(object? sender, double zoom)
    {
        if (Document is not { } document) return;
        var (viewport, bounds, dpi) = NavigationGeometry(document);
        SetViewport(CoreViewport(document).WithZoomAt(new Point(viewport.Width / 2, viewport.Height / 2), zoom,
            viewport, bounds, dpi.DpiScaleX, dpi.DpiScaleY, ContentScale(document)));
    }

    private void UpdateStatus(Point? pointer = null)
    {
        if (Document is not { } document) return;
        var (viewport, bounds, dpi) = NavigationGeometry(document);
        ViewportStatus.ContentDimensions = $"{bounds.Width:0} × {bounds.Height:0}"; ViewportStatus.Zoom = document.FaceZoom;
        if (pointer is not { } screen) { ViewportStatus.PointerCoordinates = "X: —  Y: —"; return; }
        var point = CoreViewport(document).ScreenToContent(screen, viewport, bounds, dpi.DpiScaleX, dpi.DpiScaleY,
            ContentScale(document));
        ViewportStatus.PointerCoordinates = bounds.Contains(point) ? $"X: {Math.Floor(point.X):0}  Y: {Math.Floor(point.Y):0}" : "X: —  Y: —";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDocumentSubscription(Document);
        UpdateStatus();
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
            or nameof(DocumentTabViewModel.CalibrationPlacement)
            or nameof(DocumentTabViewModel.HierarchySelectedPanelSelection))
        {
            RequestRender();
            UpdateStatus();
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

        var viewport = CreateRenderViewport(document);
        canvas.Save();
        canvas.Translate((float)viewport.PanX, (float)viewport.PanY);
        canvas.Scale((float)viewport.NormalizedZoom, (float)viewport.NormalizedScaleY);
        DrawBlankNativeCanvas(canvas, document, viewport);
        DrawFaceElements(canvas, document, viewport);
        DrawArtworkSamples(canvas, document, viewport);
        DrawSelectionOutline(canvas, document, viewport);
        DrawDragSelectionRect(canvas, viewport);
        canvas.Restore();
    }

    private static void DrawBlankNativeCanvas(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        var face = document.GetFaceDocument();
        if (face.Artwork is not null || face.SourceRegion is not { Width: > 0, Height: > 0 } bounds) return;
        using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0x2A, 0x2A, 0x2A) };
        using var grid = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x48, 0x48, 0x48), StrokeWidth = (float)(1d / viewport.NormalizedZoom) };
        canvas.DrawRect(SKRect.Create(0, 0, (float)bounds.Width, (float)bounds.Height), fill);
        const int spacing = 64;
        for (var x = spacing; x < bounds.Width; x += spacing) canvas.DrawLine(x, 0, x, (float)bounds.Height, grid);
        for (var y = spacing; y < bounds.Height; y += spacing) canvas.DrawLine(0, y, (float)bounds.Width, y, grid);
        canvas.DrawRect(SKRect.Create(0, 0, (float)bounds.Width, (float)bounds.Height), grid);
    }

    private void DrawArtworkSamples(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        var artwork=document.GetFaceElements().OfType<FaceArtworkElement>().FirstOrDefault(); if(artwork is null)return;
        foreach(var operation in document.GetFaceDocument().Artwork?.ProcessingPipeline.Operations.OfType<ArtworkCalibrationOperationModel>()??[])
        {
            Draw(operation.BlackReference.Samples,SKColors.Black,SKColors.White); Draw(operation.WhiteReference.Samples,SKColors.White,SKColors.Black);
            var palette=new[]{SKColors.Orange,SKColors.DeepSkyBlue,SKColors.LimeGreen,SKColors.Magenta,SKColors.Gold};var i=0;foreach(var g in operation.SameColorGroups)Draw(g.Samples,palette[i++%palette.Length],SKColors.Black);
        }
        if(_markerPreviewPoint is not null && document.CalibrationPlacement is not null)Draw([_markerPreviewPoint],new SKColor(255,193,7,180),SKColors.Black);
        void Draw(IReadOnlyList<CalibrationSampleModel> samples,SKColor fill,SKColor stroke){using var fp=new SKPaint{Style=SKPaintStyle.Fill,Color=fill,IsAntialias=true};using var sp=new SKPaint{Style=SKPaintStyle.Stroke,Color=stroke,StrokeWidth=(float)(2/viewport.NormalizedZoom),IsAntialias=true};foreach(var sample in samples){var x=artwork.X+sample.X*artwork.Width;var y=artwork.Y+sample.Y*artwork.Height;var half=(float)(5/viewport.NormalizedZoom);canvas.DrawRect(SKRect.Create((float)x-half,(float)y-half,half*2,half*2),fp);canvas.DrawRect(SKRect.Create((float)x-half,(float)y-half,half*2,half*2),sp);if(sample.SamplingMode==CalibrationSamplingMode.Area){var radius=(float)(sample.RadiusNormalized*Math.Min(artwork.Width,artwork.Height));canvas.DrawCircle((float)x,(float)y,radius,sp);}}}
    }

    private static void DrawFaceElements(SKCanvas canvas, DocumentTabViewModel document, PanelViewportTransform viewport)
    {
        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xC1, 0x07, 0x66), IsAntialias = true };
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0xFF, 0xD5, 0x4F), StrokeWidth = (float)(1.5d / viewport.NormalizedZoom), IsAntialias = true };
        using var hiddenPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x80, 0x80, 0x80), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };

        foreach (var element in document.GetFaceElements().OfType<FaceArtworkElement>())
        {
            DrawArtworkElement(canvas, element, element.AssetPath, viewport, hiddenPaint);
        }

        foreach (var element in FaceArtworkEditingPresentation.GetViewportElements(document).Where(element => element is not FaceArtworkElement))
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
            if (FaceArtworkEditingPresentation.IsSuppressed(document, selectedElement)) continue;
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
            element.ShowCommaTail);
    }

    private static void DrawArtworkElement(SKCanvas canvas, FaceArtworkElement element, string? assetPath, PanelViewportTransform viewport, SKPaint hiddenPaint)
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

        if (TryGetArtworkImage(assetPath, out var image))
        {
            var source = FaceArtworkRasterMapping.ResolveSourceRect(element, image.Width, image.Height);
            using var imagePaint = new SKPaint { FilterQuality = viewport.NormalizedRasterMagnification >= 1d ? SKFilterQuality.None : SKFilterQuality.Medium };
            canvas.DrawImage(image, source, destination, imagePaint);
            return;
        }

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0x33, 0x33, 0x33), IsAntialias = true };
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x66, 0x66, 0x66), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };
        canvas.DrawRect(destination, fillPaint);
        canvas.DrawRect(destination, strokePaint);
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
        return ProjectAssetPathResolver.TryResolveAssetPath(assetPath, out resolvedPath);
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
            if(document.FaceWorkspace?.IsComponentPlacementActive==true || document.FaceWorkspace?.IsLampPlacementActive==true)
            {
                _isLeftMouseDown=true; _leftMouseDownStart=pointer; _dragSelectionCurrent=pointer;
                FaceSkiaSurface.CaptureMouse(); eventArgs.Handled=true; return;
            }
            if (TryAddArtworkSample(document, pointer))
            {
                eventArgs.Handled = true;
                return;
            }
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

    private bool TryAddArtworkSample(DocumentTabViewModel document, Point pointer)
    {
        var placement=document.CalibrationPlacement;if(placement is null)return false;var artwork=document.GetFaceElements().OfType<FaceArtworkElement>().FirstOrDefault();var operation=document.GetFaceDocument().Artwork?.ProcessingPipeline.Operations.OfType<ArtworkCalibrationOperationModel>().FirstOrDefault(o=>o.Id==placement.OperationId);if(artwork is null||operation is null)return false;
        var facePoint=CreateViewport(document).ScreenToDocument(pointer);var x=facePoint.X;var y=facePoint.Y;if(x<artwork.X||x>artwork.X+artwork.Width||y<artwork.Y||y>artwork.Y+artwork.Height)return false;var sample=SnapArtworkPoint(document,artwork,x,y);
        sample=new CalibrationSampleModel{Id=sample.Id,X=sample.X,Y=sample.Y,SamplingMode=placement.SamplingMode,RadiusNormalized=placement.RadiusNormalized};
        CalibrationReferenceModel Add(CalibrationReferenceModel r)=>new(){ManualEnabled=r.ManualEnabled,ManualColor=r.ManualColor,Samples=r.Samples.Append(sample).ToArray()};
        var updated=new ArtworkCalibrationOperationModel{Id=operation.Id,Enabled=operation.Enabled,Strength=operation.Strength,BlackReference=placement.TargetKind==CalibrationPlacementTargetKind.BlackReference?Add(operation.BlackReference):operation.BlackReference,WhiteReference=placement.TargetKind==CalibrationPlacementTargetKind.WhiteReference?Add(operation.WhiteReference):operation.WhiteReference,SameColorGroups=operation.SameColorGroups.Select(g=>placement.TargetKind==CalibrationPlacementTargetKind.SameColorGroup&&g.Id==placement.TargetId?new SameColorCalibrationGroupModel{Id=g.Id,Name=g.Name,Samples=g.Samples.Append(sample).ToArray()}:g).ToArray(),CorrectSpatialBrightness=operation.CorrectSpatialBrightness,CorrectSpatialColor=operation.CorrectSpatialColor,NormalizeBlackWhite=operation.NormalizeBlackWhite,NeutralizeWhite=operation.NeutralizeWhite};
        document.CommandService.Execute(FaceMutationCommands.CreateUpdateProcessingOperationCommand(document.DocumentId,document,updated,"Add calibration sample"));document.CalibrationPlacement=null;return true;
    }
    private static CalibrationSampleModel SnapArtworkPoint(DocumentTabViewModel document,FaceArtworkElement artwork,double faceX,double faceY){var authored=document.GetFaceDocument().Artwork;var width=Math.Max(1,authored?.OutputWidth??(int)Math.Round(artwork.Width));var height=Math.Max(1,authored?.OutputHeight??(int)Math.Round(artwork.Height));var nx=Math.Clamp((faceX-artwork.X)/artwork.Width,0,1);var ny=Math.Clamp((faceY-artwork.Y)/artwork.Height,0,1);var px=Math.Clamp((int)Math.Round(nx*(width-1)),0,width-1);var py=Math.Clamp((int)Math.Round(ny*(height-1)),0,height-1);return new CalibrationSampleModel{X=width==1?0:(double)px/(width-1),Y=height==1?0:(double)py/(height-1)};}

    private void OnFaceSkiaSurfaceMouseMove(object sender, MouseEventArgs eventArgs)
    {
        UpdateStatus(eventArgs.GetPosition(FaceSkiaSurface));
        UpdateMarkerPreview(eventArgs.GetPosition(FaceSkiaSurface));
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

    private void UpdateMarkerPreview(Point pointer)
    {
        var document = Document;
        var artwork = document?.GetFaceElements().OfType<FaceArtworkElement>().FirstOrDefault();
        if (document is null || artwork is null || document.CalibrationPlacement is null)
        {
            if (_markerPreviewPoint is not null) { _markerPreviewPoint = null; RequestRender(); }
            return;
        }
        var facePoint = CreateViewport(document).ScreenToDocument(pointer);
        var x = facePoint.X;
        var y = facePoint.Y;
        var next = x >= artwork.X && x <= artwork.X + artwork.Width && y >= artwork.Y && y <= artwork.Y + artwork.Height
            ? SnapArtworkPoint(document, artwork, x, y)
            : null;
        if (_markerPreviewPoint?.X == next?.X && _markerPreviewPoint?.Y == next?.Y) return;
        _markerPreviewPoint = next;
        RequestRender();
    }

    private void OnFaceSkiaSurfaceMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            var document = Document;
            if (document is not null)
            {
                if(document.FaceWorkspace?.IsComponentPlacementActive==true || document.FaceWorkspace?.IsLampPlacementActive==true)
                {
                    var viewport = CreateViewport(document); var start=viewport.ScreenToDocument(_leftMouseDownStart);
                    var end=viewport.ScreenToDocument(_dragSelectionCurrent);
                    var width=Math.Abs(end.X-start.X); var height=Math.Abs(end.Y-start.Y);
                    if(document.FaceWorkspace.IsLampPlacementActive) document.FaceWorkspace.CompleteLampPlacement(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),width>=4?width:0,height>=4?height:0);
                    else document.FaceWorkspace.CompleteComponentPlacement(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),width>=4?width:0,height>=4?height:0);
                }
                else if (_isDragSelecting)
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

        var (viewport, bounds, dpi) = NavigationGeometry(document);
        var zoom = CoreViewport(document).ClampedZoom * (eventArgs.Delta > 0 ? EditorViewportTransform.ZoomStep : 1 / EditorViewportTransform.ZoomStep);
        SetViewport(CoreViewport(document).WithZoomAt(eventArgs.GetPosition(FaceSkiaSurface), zoom, viewport, bounds,
            dpi.DpiScaleX, dpi.DpiScaleY, ContentScale(document)));
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

        var viewport = CreateViewport(document);
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

        var viewport = CreateViewport(document);
        var selection = FaceSelectionService.SelectFromPoint(FaceArtworkEditingPresentation.GetViewportElements(document).ToArray(), viewport.ScreenToDocument(screenPoint), document.HierarchySelectedPanelSelection);
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

    private bool HasDraggedSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        var viewport = CreateViewport(document);
        return Panel2DViewportInteractionService.HasDocumentDelta(viewport.ScreenToDocument(startScreenPoint), viewport.ScreenToDocument(endScreenPoint));
    }

    private void HandleDragSelection(DocumentTabViewModel document, Point startScreenPoint, Point endScreenPoint)
    {
        var viewport = CreateViewport(document);
        var rect = Panel2DSelectionBoundsService.CreateNormalizedDocumentRect(viewport.ScreenToDocument(startScreenPoint), viewport.ScreenToDocument(endScreenPoint));
        var items = FaceSelectionInteractionService.SelectItemsFromRect(FaceArtworkEditingPresentation.GetViewportElements(document).ToArray(), rect);
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
        var viewport = CreateViewport(document);
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
        var viewport = CreateViewport(document);
        var updated = FaceElementBulkMoveService.ComputeMovedElements(_moveSnapshots, viewport.ScreenToDocument(_leftMouseDownStart), viewport.ScreenToDocument(currentScreenPoint));
        if (FaceElementPreviewMutationService.TryApplyPreviews(document, updated)) RequestRender();
    }

    private bool TryGetSelectedElementAtPoint(DocumentTabViewModel document, Point screenPoint, out FaceElementModel selectedElement)
    {
        selectedElement = new FaceLampWindowElement();
        var viewport = CreateViewport(document);
        var documentPoint = viewport.ScreenToDocument(screenPoint);
        foreach (var item in document.SelectionState.Items.Where(item => item.Domain == EditorSelectionDomain.FaceElement).Reverse())
        {
            if (document.TryGetFaceElementByObjectId(item.ObjectId, out var element)
                && !FaceArtworkEditingPresentation.IsSuppressed(document, element)
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

    private void OnFaceSkiaSurfaceMouseLeave(object sender, MouseEventArgs e) => UpdateStatus();
}
