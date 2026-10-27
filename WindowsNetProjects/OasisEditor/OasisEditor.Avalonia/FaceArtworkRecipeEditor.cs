using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OasisEditor.FacePipeline;

namespace OasisEditor.Avalonia;

internal enum ArtworkEditorCorner { TopLeft, TopRight, BottomRight, BottomLeft }

internal readonly record struct ArtworkEditorImageMap(Rect ImageRect)
{
    public bool Contains(Point point) => ImageRect.Contains(point);
    public Point ToNormalized(Point point) => new(
        Math.Clamp((point.X - ImageRect.X) / ImageRect.Width, 0, 1),
        Math.Clamp((point.Y - ImageRect.Y) / ImageRect.Height, 0, 1));

    public static Rect Fit(Rect bounds, int pixelWidth, int pixelHeight)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || pixelWidth <= 0 || pixelHeight <= 0) return default;
        var scale = Math.Min(bounds.Width / pixelWidth, bounds.Height / pixelHeight);
        var width = pixelWidth * scale;
        var height = pixelHeight * scale;
        return new(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }
}

internal sealed class ArtworkRegistrationEditInteraction
{
    public ArtworkEditorCorner? DraggingCorner { get; private set; }

    public bool Begin(FacePerspectiveRegistrationModel registration, ArtworkEditorImageMap map, Point point)
    {
        DraggingCorner = FindCorner(registration, map, point);
        return DraggingCorner is not null;
    }

    public FacePerspectiveRegistrationModel Move(FacePerspectiveRegistrationModel registration, ArtworkEditorImageMap map, Point point)
    {
        if (DraggingCorner is not { } corner) return registration;
        var normalized = map.ToNormalized(point);
        return FaceArtworkRecipeCanvas.SetCorner(registration, corner,
            new NormalizedFacePointModel { X = normalized.X, Y = normalized.Y });
    }

    public void End() => DraggingCorner = null;

    public static ArtworkEditorCorner? FindCorner(FacePerspectiveRegistrationModel registration, ArtworkEditorImageMap map, Point point)
    {
        var corners = new[] { registration.TopLeft, registration.TopRight, registration.BottomRight, registration.BottomLeft }
            .Select((value, index) => (Corner: (ArtworkEditorCorner)index,
                Point: new Point(map.ImageRect.X + value.X * map.ImageRect.Width, map.ImageRect.Y + value.Y * map.ImageRect.Height)));
        return corners.Select(value => (value.Corner, Distance: Math.Sqrt(Math.Pow(value.Point.X - point.X, 2) + Math.Pow(value.Point.Y - point.Y, 2))))
            .Where(value => value.Distance <= 14).OrderBy(value => value.Distance)
            .Select(value => (ArtworkEditorCorner?)value.Corner).FirstOrDefault();
    }
}

internal sealed class FaceArtworkRecipeCanvas : Control
{
    private Bitmap? _bitmap;
    private FacePerspectiveRegistrationModel _registration = FacePerspectiveRegistrationModel.FullImage;
    private ArtworkCalibrationOperationModel? _calibration;
    private readonly ArtworkRegistrationEditInteraction _interaction = new();
    private ArtworkEditorImageMap _map;

    public event Action<FacePerspectiveRegistrationModel>? RegistrationPreviewChanged;
    public event Action<double, double>? SamplePlaced;
    public FacePerspectiveRegistrationModel Registration { get => _registration; set { _registration = value; InvalidateVisual(); } }
    public ArtworkCalibrationOperationModel? Calibration { get => _calibration; set { _calibration = value; InvalidateVisual(); } }
    public bool ShowRegistration { get; set; } = true;

    public void SetPreview(string path)
    {
        var next = new Bitmap(path);
        _bitmap?.Dispose();
        _bitmap = next;
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void ClearPreview()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        InvalidateVisual();
    }

    private bool UpdateImageMap()
    {
        if (_bitmap is null || Bounds.Width <= 0 || Bounds.Height <= 0) return false;
        var imageRect = ArtworkEditorImageMap.Fit(new Rect(Bounds.Size).Deflate(10), _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
        if (imageRect.Width <= 0 || imageRect.Height <= 0) return false;
        _map = new ArtworkEditorImageMap(imageRect);
        return true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Brushes.DimGray, null, new Rect(Bounds.Size));
        var bitmap = _bitmap;
        if (bitmap is null || !UpdateImageMap()) return;
        var source = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        context.DrawImage(bitmap, source, _map.ImageRect);

        if (_calibration is not null)
        {
            DrawSamples(context, _calibration.BlackReference.Samples, Brushes.DodgerBlue);
            DrawSamples(context, _calibration.WhiteReference.Samples, Brushes.Gold);
            foreach (var group in _calibration.SameColorGroups) DrawSamples(context, group.Samples, Brushes.Magenta);
        }
        if (ShowRegistration)
        {
            var points = Corners(_registration).Select(ToImagePoint).ToArray();
            var outline = new Pen(Brushes.LimeGreen, 2);
            for (var i = 0; i < points.Length; i++) context.DrawLine(outline, points[i], points[(i + 1) % points.Length]);
            foreach (var point in points) context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 2), point, 7, 7);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!UpdateImageMap() || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        if (ShowRegistration && _interaction.Begin(_registration, _map, point))
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (_calibration is not null && _map.Contains(point))
        {
            var normalized = _map.ToNormalized(point);
            SamplePlaced?.Invoke(normalized.X, normalized.Y);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_interaction.DraggingCorner is null || !UpdateImageMap()) return;
        _registration = _interaction.Move(_registration, _map, e.GetPosition(this));
        RegistrationPreviewChanged?.Invoke(_registration);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_interaction.DraggingCorner is null) return;
        _interaction.End();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private Point ToImagePoint(NormalizedFacePointModel point)
        => new(_map.ImageRect.X + point.X * _map.ImageRect.Width, _map.ImageRect.Y + point.Y * _map.ImageRect.Height);

    private void DrawSamples(DrawingContext context, IReadOnlyList<CalibrationSampleModel> samples, IBrush brush)
    {
        foreach (var sample in samples)
        {
            var point = ToImagePoint(new NormalizedFacePointModel { X = sample.X, Y = sample.Y });
            if (sample.SamplingMode == CalibrationSamplingMode.Area)
            {
                var radius = sample.RadiusNormalized * Math.Min(_map.ImageRect.Width, _map.ImageRect.Height);
                context.DrawEllipse(null, new Pen(brush, 1.5), point, radius, radius);
            }
            context.DrawEllipse(brush, new Pen(Brushes.Black, 1), point, 5, 5);
        }
    }

    private static NormalizedFacePointModel[] Corners(FacePerspectiveRegistrationModel value)
        => [value.TopLeft, value.TopRight, value.BottomRight, value.BottomLeft];

    internal static FacePerspectiveRegistrationModel SetCorner(FacePerspectiveRegistrationModel value, ArtworkEditorCorner corner, NormalizedFacePointModel point)
        => corner switch
        {
            ArtworkEditorCorner.TopLeft => new() { TopLeft = point, TopRight = value.TopRight, BottomRight = value.BottomRight, BottomLeft = value.BottomLeft },
            ArtworkEditorCorner.TopRight => new() { TopLeft = value.TopLeft, TopRight = point, BottomRight = value.BottomRight, BottomLeft = value.BottomLeft },
            ArtworkEditorCorner.BottomRight => new() { TopLeft = value.TopLeft, TopRight = value.TopRight, BottomRight = point, BottomLeft = value.BottomLeft },
            _ => new() { TopLeft = value.TopLeft, TopRight = value.TopRight, BottomRight = value.BottomRight, BottomLeft = point }
        };
}

public sealed class FaceArtworkRecipePreview : UserControl, IDisposable
{
    private readonly FaceArtworkRecipeCanvas _canvas = new();

    public FaceArtworkRecipePreview(string imagePath, FacePerspectiveRegistrationModel registration,
        ArtworkCalibrationOperationModel? calibration = null, bool showRegistration = true)
    {
        _canvas.Registration = registration;
        _canvas.Calibration = calibration;
        _canvas.ShowRegistration = showRegistration;
        _canvas.SetPreview(imagePath);
        Content = _canvas;
    }

    public void Dispose() => _canvas.ClearPreview();
}

internal sealed record CalibrationTargetOption(string Key, string Label)
{
    public override string ToString() => Label;
}

internal static class FaceArtworkRecipeEditor
{
    public static async Task<bool> ShowAsync(Window owner, AuthoringSession project, FaceAuthoringSession faces, Action<Window>? onShown = null, bool calibrationStage = false)
    {
        var initial = faces.CurrentArtwork ?? throw new InvalidOperationException("Import Face artwork before editing registration or processing.");
        if (initial.Source.Kind != FaceArtworkSourceKind.Image || string.IsNullOrWhiteSpace(initial.Source.AssetPath))
            throw new InvalidOperationException("Interactive registration requires image-based Face artwork.");

        var canvas = new FaceArtworkRecipeCanvas { ClipToBounds = true, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
        var current = initial;
        var calibration = current.ProcessingPipeline.Operations.OfType<ArtworkCalibrationOperationModel>().FirstOrDefault()
            ?? new ArtworkCalibrationOperationModel { Id = Guid.NewGuid().ToString("N") };
        var isCalibrationStage = false;
        var movingSample = false;
        var dirtyRegistration = false;
        var dirtyCalibration = false;
        var status = new TextBlock { Text = "Drag the green corners to register the source image." , TextWrapping = TextWrapping.Wrap };
        void RunSampleEdit(Action action)
        {
            try { action(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            { status.Text = ex.Message; }
        }
        var tabs = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        var sourceTab = new Button { Content = "Registration source" };
        var calibrationTab = new Button { Content = "Calibration input" };
        tabs.Children.Add(sourceTab); tabs.Children.Add(calibrationTab);
        var registration = current.Geometry.PerspectiveRegistration;
        canvas.Registration = registration;
        canvas.Calibration = null;
        var sourceImagePath = project.ResolveAsset(current.Source.AssetPath);
        var calibrationPath = project.ResolveAsset(current.CorrectionInputAssetPath ?? current.Source.AssetPath);
        canvas.SetPreview(sourceImagePath);
        sourceTab.Click += (_, _) => { isCalibrationStage = false; canvas.ShowRegistration = true; canvas.SetPreview(sourceImagePath); canvas.Registration = registration; canvas.Calibration = null; status.Text = "Drag the green corners to register the source image."; };
        calibrationTab.Click += (_, _) => { isCalibrationStage = true; canvas.ShowRegistration = false; canvas.SetPreview(calibrationPath); canvas.Calibration = calibration; status.Text = "Select a calibration target, then choose Place sample and click the image."; };
        canvas.RegistrationPreviewChanged += value => { registration = value; dirtyRegistration = true; };

        var strength = new TextBox { Text = calibration.Strength.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 90 };
        var include = new CheckBox { Content = "Include calibration", IsChecked = current.ProcessingPipeline.Operations.Any(o => o is ArtworkCalibrationOperationModel) };
        var enabled = new CheckBox { Content = "Enable calibration", IsChecked = calibration.Enabled };
        var blackManual = new CheckBox { Content = "Use manual black", IsChecked = calibration.BlackReference.ManualEnabled };
        var blackColor = new TextBox { Text = calibration.BlackReference.ManualColor, Width = 100 };
        var whiteManual = new CheckBox { Content = "Use manual white", IsChecked = calibration.WhiteReference.ManualEnabled };
        var whiteColor = new TextBox { Text = calibration.WhiteReference.ManualColor, Width = 100 };
        var correctBrightness = new CheckBox { Content = "Correct spatial brightness", IsChecked = calibration.CorrectSpatialBrightness };
        var correctColor = new CheckBox { Content = "Correct spatial colour", IsChecked = calibration.CorrectSpatialColor };
        var normalize = new CheckBox { Content = "Normalize black and white", IsChecked = calibration.NormalizeBlackWhite };
        var neutralize = new CheckBox { Content = "Neutralize white", IsChecked = calibration.NeutralizeWhite };
        var target = new ComboBox { Width = 210, ItemsSource = new[] { new CalibrationTargetOption("black", "Black reference"), new CalibrationTargetOption("white", "White reference") } };
        target.SelectedIndex = 0;
        var mode = new ComboBox { Width = 100, ItemsSource = Enum.GetValues<CalibrationSamplingMode>() };
        mode.SelectedItem = CalibrationSamplingMode.Pixel;
        var radius = new TextBox { Text = "0.01", Width = 72 };
        var samples = new ListBox { MinHeight = 80, MaxHeight = 130 };
        var sampleMode = new ComboBox { Width = 100, ItemsSource = Enum.GetValues<CalibrationSamplingMode>() };
        var sampleRadius = new TextBox { Text = "0.01", Width = 72 };
        var groupName = new TextBox { Watermark = "Group name", Width = 130 };
        var placing = new CheckBox { Content = "Place sample by clicking image", IsChecked = false };
        include.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        enabled.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        blackManual.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        whiteManual.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        correctBrightness.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        correctColor.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        normalize.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        neutralize.IsCheckedChanged += (_, _) => dirtyCalibration = true;
        strength.TextChanged += (_, _) => dirtyCalibration = true;
        blackColor.TextChanged += (_, _) => dirtyCalibration = true;
        whiteColor.TextChanged += (_, _) => dirtyCalibration = true;

        void SyncTargets()
        {
            var options = new List<CalibrationTargetOption> { new("black", "Black reference"), new("white", "White reference") };
            options.AddRange(calibration.SameColorGroups.Select(group => new CalibrationTargetOption("group:" + group.Id, group.Name)));
            target.ItemsSource = options;
            target.SelectedItem = options.FirstOrDefault(value => value.Key == (target.SelectedItem as CalibrationTargetOption)?.Key) ?? options[0];
        }
        void RefreshSamples()
        {
            var key = (target.SelectedItem as CalibrationTargetOption)?.Key ?? "black";
            samples.ItemsSource = GetSamples(calibration, key).Select((sample, index) => new CalibrationSampleOption(sample, index)).ToArray();
            canvas.Calibration = isCalibrationStage ? calibration : null;
            dirtyCalibration = true;
        }
        var selectedSample = () => samples.SelectedItem is CalibrationSampleOption option ? option.Sample : null;
        target.SelectionChanged += (_, _) => RefreshSamples();
        samples.SelectionChanged += (_, _) =>
        {
            if (selectedSample() is not { } sample) return;
            sampleMode.SelectedItem = sample.SamplingMode;
            sampleRadius.Text = sample.RadiusNormalized.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        };
        include.IsCheckedChanged += (_, _) => enabled.IsEnabled = include.IsChecked == true;
        enabled.IsEnabled = include.IsChecked == true;
        var groupButtons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
        groupButtons.Children.Add(new Button { Content = "Add colour group" });
        groupButtons.Children.Add(new Button { Content = "Rename group" });
        groupButtons.Children.Add(new Button { Content = "Remove group" });
        ((Button)groupButtons.Children[0]!).Click += (_, _) =>
        {
            var name = string.IsNullOrWhiteSpace(groupName.Text) ? $"Colour Group {calibration.SameColorGroups.Count + 1}" : groupName.Text.Trim();
            calibration = Copy(calibration, groups: [.. calibration.SameColorGroups, new SameColorCalibrationGroupModel { Name = name }]); SyncTargets();
            target.SelectedItem = (target.ItemsSource as IEnumerable<CalibrationTargetOption>)?.LastOrDefault(); RefreshSamples();
        };
        ((Button)groupButtons.Children[1]!).Click += (_, _) =>
        {
            if ((target.SelectedItem as CalibrationTargetOption)?.Key is not { } key || !key.StartsWith("group:", StringComparison.Ordinal)) return;
            var id = key[6..];
            calibration = Copy(calibration, groups: calibration.SameColorGroups.Select(group => group.Id == id ? new SameColorCalibrationGroupModel { Id = group.Id, Name = string.IsNullOrWhiteSpace(groupName.Text) ? group.Name : groupName.Text.Trim(), Samples = group.Samples } : group).ToArray());
            SyncTargets(); RefreshSamples();
        };
        ((Button)groupButtons.Children[2]!).Click += (_, _) =>
        {
            if ((target.SelectedItem as CalibrationTargetOption)?.Key is not { } key || !key.StartsWith("group:", StringComparison.Ordinal)) return;
            var id = key[6..]; calibration = Copy(calibration, groups: calibration.SameColorGroups.Where(group => group.Id != id).ToArray()); SyncTargets(); RefreshSamples();
        };

        canvas.SamplePlaced += (x, y) =>
        {
            RunSampleEdit(() =>
            {
                var selected = target.SelectedItem as CalibrationTargetOption;
                if (placing.IsChecked != true || selected is null) return;
                var prior = selectedSample();
                if (movingSample)
                {
                    var existing = prior ?? throw new InvalidOperationException("Select the sample again before placing it in this calibration target.");
                    calibration = ReplaceSample(calibration, selected.Key, new CalibrationSampleModel { Id = existing.Id, X = x, Y = y,
                        SamplingMode = existing.SamplingMode, RadiusNormalized = existing.RadiusNormalized });
                }
                else
                    calibration = AddSample(calibration, selected.Key, new CalibrationSampleModel { X = x, Y = y,
                        SamplingMode = (CalibrationSamplingMode?)mode.SelectedItem ?? CalibrationSamplingMode.Pixel, RadiusNormalized = ParseRadius(radius.Text) });
                RefreshSamples();
                samples.SelectedItem = movingSample
                    ? (samples.ItemsSource as CalibrationSampleOption[])?.FirstOrDefault(value => value.Sample.Id == prior?.Id)
                    : (samples.ItemsSource as CalibrationSampleOption[])?.LastOrDefault();
                placing.IsChecked = false;
                movingSample = false;
                dirtyCalibration = true;
                status.Text = "Calibration sample updated.";
            });
        };

        var sampleActions = new WrapPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal };
        var addSample = new Button { Content = "Place sample" };
        var moveSample = new Button { Content = "Move selected sample" };
        var applySample = new Button { Content = "Apply sample type / radius" };
        var removeSample = new Button { Content = "Remove sample" };
        addSample.Click += (_, _) => RunSampleEdit(() =>
        { isCalibrationStage = true; canvas.ShowRegistration = false; canvas.SetPreview(calibrationPath); canvas.Calibration = calibration; placing.IsChecked = true; status.Text = "Click inside the image to add the selected sample."; });
        moveSample.Click += (_, _) => RunSampleEdit(() =>
        {
            if (selectedSample() is null) { status.Text = "Select a sample to move first."; return; }
            isCalibrationStage = true; movingSample = true; placing.IsChecked = true; canvas.ShowRegistration = false; canvas.SetPreview(calibrationPath);
            canvas.Calibration = calibration;
            status.Text = "Click inside the image to move the selected sample.";
        });
        applySample.Click += (_, _) =>
        {
            RunSampleEdit(() =>
            {
                if (selectedSample() is not { } prior || target.SelectedItem is not CalibrationTargetOption selected) return;
                var updated = new CalibrationSampleModel { Id = prior.Id, X = prior.X, Y = prior.Y,
                    SamplingMode = (CalibrationSamplingMode?)sampleMode.SelectedItem ?? CalibrationSamplingMode.Pixel,
                    RadiusNormalized = ParseRadius(sampleRadius.Text) };
                calibration = ReplaceSample(calibration, selected.Key, updated);
                RefreshSamples(); dirtyCalibration = true;
                status.Text = "Calibration sample settings updated.";
            });
        };
        removeSample.Click += (_, _) =>
        {
            if (selectedSample() is not { } sample || target.SelectedItem is not CalibrationTargetOption selected) return;
            calibration = RemoveSample(calibration, selected.Key, sample.Id); RefreshSamples();
        };
        foreach (var button in new[] { addSample, moveSample, applySample, removeSample }) sampleActions.Children.Add(button);
        var sampleConfig = new WrapPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal };
        sampleConfig.Children.Add(new TextBlock { Text = "New sample:", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }); sampleConfig.Children.Add(mode);
        sampleConfig.Children.Add(new TextBlock { Text = "Radius fraction:", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }); sampleConfig.Children.Add(radius);
        var sampleEdit = new WrapPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal };
        sampleEdit.Children.Add(new TextBlock { Text = "Selected:", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }); sampleEdit.Children.Add(sampleMode);
        sampleEdit.Children.Add(new TextBlock { Text = "Radius fraction:", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }); sampleEdit.Children.Add(sampleRadius);

        var options = new StackPanel { Spacing = 5, Margin = new Thickness(8) };
        options.Children.Add(new TextBlock { Text = "CALIBRATION", FontWeight = FontWeight.SemiBold });
        options.Children.Add(include); options.Children.Add(enabled);
        options.Children.Add(new WrapPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Children = { new TextBlock { Text = "Strength (0–100):", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }, strength, blackManual, blackColor, whiteManual, whiteColor } });
        options.Children.Add(correctBrightness); options.Children.Add(correctColor); options.Children.Add(normalize); options.Children.Add(neutralize);
        options.Children.Add(new WrapPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Children = { target, groupName } });
        options.Children.Add(groupButtons); options.Children.Add(sampleConfig); options.Children.Add(placing); options.Children.Add(samples); options.Children.Add(sampleEdit); options.Children.Add(sampleActions);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,500"), Margin = new Thickness(10) };
        Grid.SetColumnSpan(tabs, 2); layout.Children.Add(tabs);
        Grid.SetRow(canvas, 1); layout.Children.Add(canvas);
        var optionsScroll = new ScrollViewer { Content = options, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(optionsScroll, 1); Grid.SetColumn(optionsScroll, 1); layout.Children.Add(optionsScroll);
        var footer = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8) };
        footer.Children.Add(status);
        var rebuild = new Button { Content = "Apply and rebuild" };
        var close = new Button { Content = "Done" };
        footer.Children.Add(rebuild); footer.Children.Add(close); Grid.SetRow(footer, 2); Grid.SetColumnSpan(footer, 2); layout.Children.Add(footer);
        var dialog = new Window { Title = "Face Artwork Registration and Calibration", Width = 1100, Height = 800, MinWidth = 1000, MinHeight = 640, Content = layout, CanResize = true };
        var saved = false;
        rebuild.Click += (_, _) =>
        {
            try
            {
                if (dirtyRegistration) faces.SetArtworkRegistration(registration);
                if (dirtyCalibration || dirtyRegistration)
                {
                    if (include.IsChecked == true)
                    {
                        var amount = double.TryParse(strength.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed) && parsed is >= 0 and <= 100
                            ? parsed : throw new ArgumentException("Calibration strength must be between 0 and 100.");
                        if (!IsColor(blackColor.Text) || !IsColor(whiteColor.Text))
                            throw new ArgumentException("Black and white reference colours must use #RRGGBB or #AARRGGBB notation.");
                        var updated = new ArtworkCalibrationOperationModel { Id = calibration.Id, Enabled = enabled.IsChecked == true, Strength = amount,
                            BlackReference = new CalibrationReferenceModel { ManualEnabled = blackManual.IsChecked == true, ManualColor = blackColor.Text ?? "#FF000000", Samples = calibration.BlackReference.Samples },
                            WhiteReference = new CalibrationReferenceModel { ManualEnabled = whiteManual.IsChecked == true, ManualColor = whiteColor.Text ?? "#FFFFFFFF", Samples = calibration.WhiteReference.Samples },
                            SameColorGroups = calibration.SameColorGroups, CorrectSpatialBrightness = correctBrightness.IsChecked == true,
                            CorrectSpatialColor = correctColor.IsChecked == true, NormalizeBlackWhite = normalize.IsChecked == true, NeutralizeWhite = neutralize.IsChecked == true };
                        faces.SetArtworkCalibration(updated);
                    }
                    else faces.SetArtworkCalibration(null);
                }
                faces.RebuildArtwork(); saved = true; dirtyRegistration = false; dirtyCalibration = false;
                current = faces.CurrentArtwork!; registration = current.Geometry.PerspectiveRegistration;
                calibration = current.ProcessingPipeline.Operations.OfType<ArtworkCalibrationOperationModel>().FirstOrDefault() ?? calibration;
                canvas.Registration = registration;
                canvas.SetPreview(isCalibrationStage ? calibrationPath : sourceImagePath);
                canvas.Calibration = isCalibrationStage ? calibration : null;
                status.Text = "Artwork stages rebuilt.";
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            { status.Text = ex.Message; }
        };
        close.Click += (_, _) => dialog.Close();
        SyncTargets(); RefreshSamples(); dirtyCalibration = false;
        if (calibrationStage) calibrationTab.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var showing = dialog.ShowDialog(owner);
        onShown?.Invoke(dialog);
        await showing;
        canvas.ClearPreview();
        return saved;
    }

    private sealed record CalibrationSampleOption(CalibrationSampleModel Sample, int Index)
    {
        public override string ToString() => $"Sample {Index + 1}: ({Sample.X:0.####}, {Sample.Y:0.####}) · {Sample.SamplingMode} · radius {Sample.RadiusNormalized:0.####}";
    }

    private static IReadOnlyList<CalibrationSampleModel> GetSamples(ArtworkCalibrationOperationModel calibration, string key)
        => key switch { "black" => calibration.BlackReference.Samples, "white" => calibration.WhiteReference.Samples,
            _ when key.StartsWith("group:", StringComparison.Ordinal) => calibration.SameColorGroups.FirstOrDefault(group => group.Id == key[6..])?.Samples ?? [], _ => [] };

    private static ArtworkCalibrationOperationModel AddSample(ArtworkCalibrationOperationModel calibration, string key, CalibrationSampleModel sample)
        => ChangeSamples(calibration, key, values => [.. values, sample]);
    private static ArtworkCalibrationOperationModel ReplaceSample(ArtworkCalibrationOperationModel calibration, string key, CalibrationSampleModel sample)
        => ChangeSamples(calibration, key, values => values.Select(value => value.Id == sample.Id ? sample : value).ToArray());
    private static ArtworkCalibrationOperationModel RemoveSample(ArtworkCalibrationOperationModel calibration, string key, string id)
        => ChangeSamples(calibration, key, values => values.Where(value => value.Id != id).ToArray());
    private static ArtworkCalibrationOperationModel ChangeSamples(ArtworkCalibrationOperationModel calibration, string key, Func<IReadOnlyList<CalibrationSampleModel>, IReadOnlyList<CalibrationSampleModel>> update)
        => key switch
        {
            "black" => Copy(calibration, black: new CalibrationReferenceModel { ManualEnabled = calibration.BlackReference.ManualEnabled, ManualColor = calibration.BlackReference.ManualColor, Samples = update(calibration.BlackReference.Samples) }),
            "white" => Copy(calibration, white: new CalibrationReferenceModel { ManualEnabled = calibration.WhiteReference.ManualEnabled, ManualColor = calibration.WhiteReference.ManualColor, Samples = update(calibration.WhiteReference.Samples) }),
            _ when key.StartsWith("group:", StringComparison.Ordinal) => Copy(calibration, groups: calibration.SameColorGroups.Select(group => group.Id == key[6..] ? new SameColorCalibrationGroupModel { Id = group.Id, Name = group.Name, Samples = update(group.Samples) } : group).ToArray()),
            _ => calibration
        };
    private static ArtworkCalibrationOperationModel Copy(ArtworkCalibrationOperationModel source, CalibrationReferenceModel? black = null, CalibrationReferenceModel? white = null, IReadOnlyList<SameColorCalibrationGroupModel>? groups = null)
        => new() { Id = source.Id, Enabled = source.Enabled, Strength = source.Strength, BlackReference = black ?? source.BlackReference,
            WhiteReference = white ?? source.WhiteReference, SameColorGroups = groups ?? source.SameColorGroups,
            CorrectSpatialBrightness = source.CorrectSpatialBrightness, CorrectSpatialColor = source.CorrectSpatialColor,
            NormalizeBlackWhite = source.NormalizeBlackWhite, NeutralizeWhite = source.NeutralizeWhite };
    private static double ParseRadius(string? value)
        => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radius) && double.IsFinite(radius) && radius is >= 0 and <= .5
            ? radius : throw new ArgumentException("Sample radius must be between 0 and 0.5 of the shorter image dimension.");
    private static bool IsColor(string? value)
        => value is { Length: 7 or 9 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}
