using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Media = global::Avalonia.Media;
using Layout = global::Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    partial void AddFaceWorkflowMenus(MenuItem fileMenu, MenuItem createMenu)
    {
        MenuAction(fileMenu, "Import Face artwork…", async () => await ImportFaceArtwork());
        MenuAction(fileMenu, "Import artwork override…", async () => await ImportFaceArtworkOverride());
        MenuAction(fileMenu, "Artwork registration and processing…", async () => await EditFaceArtworkRecipe());
        MenuAction(fileMenu, "Rebuild Face artwork", () => Try(() => { RequireFace(); Faces.RebuildArtwork(); Refresh(); }));
        MenuAction(createMenu, "Generate Face from selected source…", async () => await GenerateFaceFromSelectedShape());
    }

    private async Task ImportFaceArtwork()
    {
        if (!_faceActive || Faces.FacePath is null) { SetStatus("Open a Face asset before importing its artwork."); return; }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Import Face artwork", FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
            Try(() => { WithSelectedImportFile(path, source => _ = Faces.ImportArtwork(source)); Refresh(); });
    }

    private async Task ImportFaceArtworkOverride()
    {
        if (!_faceActive || Faces.FacePath is null) { SetStatus("Open a Face asset before importing an artwork override."); return; }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Import artwork override", FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
            Try(() => { WithSelectedImportFile(path, source => _ = Faces.ImportArtworkOverride(source)); Refresh(); });
    }

    private async Task GenerateFaceFromSelectedShape()
    {
        if (!_faceActive && _session.PanelPath is null) { SetStatus("Open a Panel2D asset with a Face source region first."); return; }
        var shape = _session.SourceShapes.FirstOrDefault(value => value.Id == _selectedSource);
        if (shape is null) { SetStatus("Select a Face source region in the Hierarchy first."); return; }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var name = new TextBox { Text = shape.Name, PlaceholderText = "Face name" };
        var message = new TextBlock { Text = "The source Panel2D must be saved before generation.", TextWrapping = Media.TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 10 };
        panel.Children.Add(name); panel.Children.Add(message);
        var dialog = new Window { Title = "Generate Face", Width = 430, Height = 190, Content = panel, CanResize = false };
        panel.Children.Add(ActionButton("Generate Face", () =>
        {
            try
            {
                var path = Faces.CreateFromPanelFaceSourceShape(shape.Id, name.Text ?? shape.Name);
                _faceActive = true; _selectedSource = null; Refresh(); SetStatus("Generated Face package: " + path); dialog.Close();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
            { message.Text = ex.Message; }
        }));
        await dialog.ShowDialog(owner);
    }

    private async Task EditFaceArtworkRecipe()
    {
        if (!_faceActive || Faces.CurrentArtwork is null) { SetStatus("Import Face artwork before editing registration or processing."); return; }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var saved = await FaceArtworkRecipeDialog.ShowAsync(owner, Faces);
        if (saved) Refresh();
    }
}

internal static class FaceArtworkRecipeDialog
{
    public static async Task<bool> ShowAsync(Window owner, FaceAuthoringSession faces)
    {
        var artwork = faces.CurrentArtwork ?? throw new InvalidOperationException("Import artwork before editing its recipe.");
        var original = artwork.Geometry.PerspectiveRegistration;
        var points = new Dictionary<string, TextBox>(StringComparer.Ordinal);
        var calibration = artwork.ProcessingPipeline.Operations.OfType<ArtworkCalibrationOperationModel>().FirstOrDefault();
        var strength = new TextBox { Text = (calibration?.Strength ?? ArtworkCalibrationOperationModel.DefaultStrength).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        var black = new TextBox { Text = calibration?.BlackReference.ManualColor ?? "#FF000000" };
        var white = new TextBox { Text = calibration?.WhiteReference.ManualColor ?? "#FFFFFFFF" };
        var enabled = new CheckBox { Content = "Include calibration operation", IsChecked = calibration is not null };
        var operationEnabled = new CheckBox { Content = "Enable calibration", IsChecked = calibration?.Enabled ?? true, IsEnabled = calibration is not null };
        var blackManual = new CheckBox { Content = "Use manual black colour", IsChecked = calibration?.BlackReference.ManualEnabled ?? true };
        var whiteManual = new CheckBox { Content = "Use manual white colour", IsChecked = calibration?.WhiteReference.ManualEnabled ?? true };
        var blackSamples = new TextBox { Text = FormatSamples(calibration?.BlackReference.Samples ?? [], "0.05,0.05") };
        var whiteSamples = new TextBox { Text = FormatSamples(calibration?.WhiteReference.Samples ?? [], "0.95,0.95") };
        var brightness = new CheckBox { Content = "Correct spatial brightness", IsChecked = calibration?.CorrectSpatialBrightness ?? true };
        var color = new CheckBox { Content = "Correct spatial colour", IsChecked = calibration?.CorrectSpatialColor ?? true };
        var tonal = new CheckBox { Content = "Normalize black and white", IsChecked = calibration?.NormalizeBlackWhite ?? true };
        var neutral = new CheckBox { Content = "Neutralize white", IsChecked = calibration?.NeutralizeWhite ?? true };
        var overrideX = new TextBox { Text = artwork.Override?.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) ?? "0" };
        var overrideY = new TextBox { Text = artwork.Override?.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) ?? "0" };
        var overrideWidth = new TextBox { Text = artwork.Override?.Width.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) ?? "1" };
        var overrideHeight = new TextBox { Text = artwork.Override?.Height.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) ?? "1" };
        var overridePoints = new Dictionary<string, TextBox>(StringComparer.Ordinal);
        var overrideRegistration = artwork.Override?.PerspectiveRegistration ?? FacePerspectiveRegistrationModel.FullImage;
        var overrideAlpha = new ComboBox { ItemsSource = Enum.GetValues<FaceArtworkOverrideAlphaSource>(), SelectedItem = artwork.Override?.AlphaSource ?? FaceArtworkOverrideAlphaSource.OriginalFaceArt };
        var overrideEnabled = new CheckBox { Content = "Enable artwork override", IsChecked = artwork.Override?.Enabled ?? false, IsEnabled = artwork.Override is not null };
        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 9 };
        enabled.IsCheckedChanged += (_, _) => operationEnabled.IsEnabled = enabled.IsChecked == true;
        panel.Children.Add(new TextBlock { Text = "Perspective registration (normalized source coordinates)", FontWeight = Media.FontWeight.SemiBold });
        AddPoint("Top left", original.TopLeft); AddPoint("Top right", original.TopRight);
        AddPoint("Bottom right", original.BottomRight); AddPoint("Bottom left", original.BottomLeft);
        panel.Children.Add(new TextBlock { Text = "Calibration", FontWeight = Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(enabled); panel.Children.Add(operationEnabled);
        AddField("Strength (0–100)", strength); panel.Children.Add(blackManual); AddField("Manual black colour", black);
        AddField("Black sample points (x,y; …)", blackSamples); panel.Children.Add(whiteManual); AddField("Manual white colour", white);
        AddField("White sample points (x,y; …)", whiteSamples);
        panel.Children.Add(brightness); panel.Children.Add(color); panel.Children.Add(tonal); panel.Children.Add(neutral);
        if (artwork.Override is not null)
        {
            panel.Children.Add(new TextBlock { Text = "Artwork override alignment", FontWeight = Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(overrideEnabled); AddField("Override X", overrideX); AddField("Override Y", overrideY);
            AddField("Override width", overrideWidth); AddField("Override height", overrideHeight);
            AddField("Override alpha source", overrideAlpha);
            panel.Children.Add(new TextBlock { Text = "Override source registration", FontWeight = Media.FontWeight.SemiBold });
            AddOverridePoint("Top left", overrideRegistration.TopLeft); AddOverridePoint("Top right", overrideRegistration.TopRight);
            AddOverridePoint("Bottom right", overrideRegistration.BottomRight); AddOverridePoint("Bottom left", overrideRegistration.BottomLeft);
        }
        var error = new TextBlock { TextWrapping = Media.TextWrapping.Wrap, Foreground = Media.Brushes.OrangeRed };
        panel.Children.Add(error);
        var dialog = new Window { Title = "Face Artwork Registration and Processing", Width = 640, Height = 900, Content = new ScrollViewer { Content = panel }, CanResize = true };
        var saved = false;
        panel.Children.Add(new Button { Content = "Apply and rebuild", HorizontalAlignment = Layout.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) });
        var apply = (Button)panel.Children[^1]!;
        apply.Click += (_, _) =>
        {
            try
            {
                double Read(string key) => double.TryParse(points[key].Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                    ? value : throw new ArgumentException($"{key} must be a finite number.");
                var registration = new FacePerspectiveRegistrationModel
                {
                    TopLeft = new() { X = Read("Top left X"), Y = Read("Top left Y") },
                    TopRight = new() { X = Read("Top right X"), Y = Read("Top right Y") },
                    BottomRight = new() { X = Read("Bottom right X"), Y = Read("Bottom right Y") },
                    BottomLeft = new() { X = Read("Bottom left X"), Y = Read("Bottom left Y") }
                };
                faces.SetArtworkRegistration(registration);
                if (enabled.IsChecked == true)
                {
                    if (!double.TryParse(strength.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amount))
                        throw new ArgumentException("Calibration strength must be a number from zero to one hundred.");
                    faces.SetArtworkCalibration(new ArtworkCalibrationOperationModel
                    {
                        Id = calibration?.Id ?? Guid.NewGuid().ToString("N"), Enabled = operationEnabled.IsChecked == true, Strength = amount,
                        BlackReference = BuildReference(calibration?.BlackReference, blackManual.IsChecked == true, black.Text, blackSamples.Text),
                        WhiteReference = BuildReference(calibration?.WhiteReference, whiteManual.IsChecked == true, white.Text, whiteSamples.Text),
                        SameColorGroups = calibration?.SameColorGroups ?? [],
                        CorrectSpatialBrightness = brightness.IsChecked == true, CorrectSpatialColor = color.IsChecked == true,
                        NormalizeBlackWhite = tonal.IsChecked == true, NeutralizeWhite = neutral.IsChecked == true
                    });
                }
                else faces.SetArtworkCalibration(null);
                if (artwork.Override is not null)
                {
                    double ReadOverride(TextBox box, string label) => double.TryParse(box.Text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                        ? value : throw new ArgumentException($"{label} must be a finite number.");
                    if (overrideAlpha.SelectedItem is not FaceArtworkOverrideAlphaSource alpha)
                        throw new ArgumentException("Choose an artwork override alpha source.");
                    var overrideQuad = new FacePerspectiveRegistrationModel
                    {
                        TopLeft = ReadOverridePoint("Top left"), TopRight = ReadOverridePoint("Top right"),
                        BottomRight = ReadOverridePoint("Bottom right"), BottomLeft = ReadOverridePoint("Bottom left")
                    };
                    faces.SetArtworkOverrideAlignment(ReadOverride(overrideX, "Override X"), ReadOverride(overrideY, "Override Y"),
                        ReadOverride(overrideWidth, "Override width"), ReadOverride(overrideHeight, "Override height"), alpha,
                        overrideQuad, overrideEnabled.IsChecked == true);
                }
                faces.RebuildArtwork(); saved = true; dialog.Close();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            { error.Text = ex.Message; }
        };
        await dialog.ShowDialog(owner);
        return saved;

        void AddPoint(string label, NormalizedFacePointModel value)
        { AddField(label + " X", NewBox(value.X)); AddField(label + " Y", NewBox(value.Y)); }
        void AddOverridePoint(string label, NormalizedFacePointModel value)
        { AddField(label + " X", NewBox(value.X), overridePoints); AddField(label + " Y", NewBox(value.Y), overridePoints); }
        TextBox NewBox(double value) => new() { Text = value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture), Width = 115 };
        void AddField(string label, Control field, Dictionary<string, TextBox>? destination = null)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,130"), ColumnSpacing = 10 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = Layout.VerticalAlignment.Center });
            Grid.SetColumn(field, 1); row.Children.Add(field); panel.Children.Add(row);
            if (field is TextBox box && (label.EndsWith(" X", StringComparison.Ordinal) || label.EndsWith(" Y", StringComparison.Ordinal))) points[label] = box;
            if (field is TextBox textBox && destination is not null) destination[label] = textBox;
        }

        NormalizedFacePointModel ReadOverridePoint(string label)
        {
            double Read(string axis)
            {
                var key = $"{label} {axis}";
                return double.TryParse(overridePoints[key].Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                    ? value : throw new ArgumentException($"{key} must be a finite number.");
            }
            return new() { X = Read("X"), Y = Read("Y") };
        }

        CalibrationReferenceModel BuildReference(CalibrationReferenceModel? prior, bool manual, string? colorValue, string? samplesValue)
        {
            var text = (samplesValue ?? string.Empty).Trim();
            var parsed = text.Length == 0 ? [] : text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split(',', StringSplitOptions.TrimEntries))
                .Select(pair => pair.Length == 2
                    && double.TryParse(pair[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                    && double.TryParse(pair[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
                    && double.IsFinite(x) && double.IsFinite(y) && x is >= 0 and <= 1 && y is >= 0 and <= 1
                        ? (X: x, Y: y) : throw new ArgumentException("Sample points must be normalized x,y pairs separated by semicolons."))
                .ToArray();
            var priorSamples = prior?.Samples ?? [];
            var samples = parsed.Select((point, index) =>
            {
                var old = index < priorSamples.Count ? priorSamples[index] : null;
                return new CalibrationSampleModel
                {
                    Id = old?.Id ?? Guid.NewGuid().ToString("N"), X = point.X, Y = point.Y,
                    SamplingMode = old?.SamplingMode ?? CalibrationSamplingMode.Pixel,
                    RadiusNormalized = old?.RadiusNormalized ?? .01d
                };
            }).ToArray();
            return new CalibrationReferenceModel { ManualEnabled = manual, ManualColor = colorValue ?? "#FF000000", Samples = samples };
        }

        static string FormatSamples(IReadOnlyList<CalibrationSampleModel> samples, string fallback)
            => samples.Count == 0 ? fallback : string.Join("; ", samples.Select(sample =>
                $"{sample.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)},{sample.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}"));
    }
}
