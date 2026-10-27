using Avalonia;
using Avalonia.Controls;
using System.Globalization;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private void InitializeFaceComponents(MenuItem create)
    {
        Add("Face button", () => Faces.AddButton("Button", 24, 24));
        Add("7 segment display", () => Faces.AddSevenSegmentDisplay("Seven-Segment Display", 24, 24));
        Add("Alpha display", () => Faces.AddAlphaDisplay("Alpha Display", 24, 24));
        var settings = new MenuItem { Header = "Face generation settings…" };
        settings.Click += async (_, _) => await EditGenerationSettings();
        create.Items.Add(settings);

        void Add(string label, Func<string> createComponent)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => Try(() => { RequireFace(); createComponent(); Refresh(); });
            create.Items.Add(item);
        }
    }

    private async Task EditGenerationSettings()
    {
        if (!_faceActive || Faces.Document is null) { SetStatus("Open a Face before editing its generation settings."); return; }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var saved = await FaceGenerationSettingsDialog.ShowAsync(owner, Faces);
        if (saved) { _inspected = null; Refresh(); }
    }

    private void AppendFaceComponentInspector(string id)
    {
        var element = Faces.Document?.Elements.FirstOrDefault(candidate => string.Equals(candidate.ObjectId, id, StringComparison.Ordinal));
        if (element is null || element is not (FaceButtonElement or FaceSevenSegmentDisplayElement or FaceAlphaDisplayElement)) return;

        Heading("COMPONENT SETTINGS");
        var reference = new TextBox { Text = element.LinkedMachineObjectReference?.Id ?? "" };
        var input = new TextBox { Text = element is FaceButtonElement button ? button.LinkedInputReference?.Id ?? "" : "" };
        var onColor = new TextBox { Text = element switch { FaceSevenSegmentDisplayElement seven => seven.OnColorHex ?? "", FaceAlphaDisplayElement alpha => alpha.OnColorHex ?? "", _ => "" } };
        var offColor = new TextBox { Text = element switch { FaceSevenSegmentDisplayElement seven => seven.OffColorHex ?? "", FaceAlphaDisplayElement alpha => alpha.OffColorHex ?? "", _ => "" } };
        var digits = new TextBox { Text = element switch { FaceSevenSegmentDisplayElement seven => seven.DigitCount.ToString(CultureInfo.InvariantCulture), FaceAlphaDisplayElement alpha => alpha.DigitCount.ToString(CultureInfo.InvariantCulture), _ => "" } };
        var segmentType = new TextBox { Text = (element as FaceAlphaDisplayElement)?.SegmentDisplayType ?? "" };
        var decimalPoint = new CheckBox { Content = "Show decimal point", IsChecked = element switch { FaceSevenSegmentDisplayElement seven => seven.ShowDecimalPoint, FaceAlphaDisplayElement alpha => alpha.ShowDecimalPoint, _ => false } };
        var commaTail = new CheckBox { Content = "Show comma tail", IsChecked = (element as FaceAlphaDisplayElement)?.ShowCommaTail == true };
        var reversed = new CheckBox { Content = "Reverse display", IsChecked = (element as FaceAlphaDisplayElement)?.IsReversed == true };

        if (element is FaceButtonElement)
            Field("Machine input ID", input);
        else
        {
            Field("Display index", reference);
            Field("Digit count", digits);
            Field("On colour", onColor);
            Field("Off colour", offColor);
            _inspector.Children.Add(decimalPoint);
            if (element is FaceAlphaDisplayElement)
            {
                Field("Segment type", segmentType);
                _inspector.Children.Add(commaTail);
                _inspector.Children.Add(reversed);
            }
        }

        _inspector.Children.Add(ActionButton("Apply component settings", () => Try(() =>
        {
            int? displayIndex = null;
            if (element is not FaceButtonElement && !string.IsNullOrWhiteSpace(reference.Text))
                displayIndex = int.TryParse(reference.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
                    ? parsed : throw new ArgumentException("Display index must be a whole number zero or greater.");
            int? digitCount = element is FaceButtonElement ? null
                : int.TryParse(digits.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                    ? count : throw new ArgumentException("Digit count must be a whole number.");
            var logicalReference = displayIndex is int value
                ? element is FaceSevenSegmentDisplayElement ? MachineObjectReference.SevenSegmentDisplay(value) : MachineObjectReference.AlphaDisplay(value)
                : (MachineObjectReference?)null;
            Faces.UpdateFaceComponent(id, new FaceComponentUpdateOptions
            {
                LogicalReference = logicalReference,
                ClearLogicalReference = element is not FaceButtonElement && logicalReference is null,
                InputId = element is FaceButtonElement && !string.IsNullOrWhiteSpace(input.Text) ? input.Text.Trim() : null,
                ClearInputReference = element is FaceButtonElement && string.IsNullOrWhiteSpace(input.Text),
                OnColorHex = element is not FaceButtonElement && !string.IsNullOrWhiteSpace(onColor.Text) ? onColor.Text : null,
                ClearOnColorHex = element is not FaceButtonElement && string.IsNullOrWhiteSpace(onColor.Text),
                OffColorHex = element is not FaceButtonElement && !string.IsNullOrWhiteSpace(offColor.Text) ? offColor.Text : null,
                ClearOffColorHex = element is not FaceButtonElement && string.IsNullOrWhiteSpace(offColor.Text),
                DigitCount = digitCount,
                SegmentDisplayType = element is FaceAlphaDisplayElement && !string.IsNullOrWhiteSpace(segmentType.Text) ? segmentType.Text : null,
                ClearSegmentDisplayType = element is FaceAlphaDisplayElement && string.IsNullOrWhiteSpace(segmentType.Text),
                ShowDecimalPoint = element is not FaceButtonElement ? decimalPoint.IsChecked == true : null,
                ShowCommaTail = element is FaceAlphaDisplayElement ? commaTail.IsChecked == true : null,
                IsReversed = element is FaceAlphaDisplayElement ? reversed.IsChecked == true : null
            });
            _inspected = null;
            Refresh();
        })));

        void Field(string label, Control control)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,130"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            Grid.SetColumn(control, 1);
            row.Children.Add(control);
            _inspector.Children.Add(row);
        }
    }
}

internal static class FaceGenerationSettingsDialog
{
    public static async Task<bool> ShowAsync(Window owner, FaceAuthoringSession faces)
    {
        var settings = faces.Document?.GenerationSettings ?? FaceGenerationSettingsModel.Default;
        var threshold = Number(settings.MaskExtractionThreshold);
        var inflation = Number(settings.TrayBoundsInflationPercent);
        var padding = Number(settings.TrayBoundsPaddingPixels);
        var amount = Number(settings.PostWarpSharpeningAmount);
        var radius = Number(settings.PostWarpSharpeningRadiusPixels);
        var sharpenThreshold = Number(settings.PostWarpSharpeningThreshold);
        var clamp = new CheckBox { Content = "Clamp tray bounds to lamp window", IsChecked = settings.ClampTrayBoundsToLampWindow };
        var sharpening = new CheckBox { Content = "Enable post-warp sharpening", IsChecked = settings.PostWarpSharpeningEnabled };
        var error = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var body = new StackPanel { Margin = new Thickness(18), Spacing = 9 };
        body.Children.Add(new TextBlock { Text = "Tune artwork sharpening, lamp-mask extraction, and tray generation." });
        body.Children.Add(new TextBlock { Text = "PERSPECTIVE ARTWORK", FontWeight = global::Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        body.Children.Add(sharpening);
        Add("Sharpen amount (0–2)", amount); Add("Sharpen radius (0.1–3 px)", radius); Add("Sharpen threshold (0–255)", sharpenThreshold);
        body.Children.Add(new TextBlock { Text = "LAMP MASK GENERATION", FontWeight = global::Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        Add("Mask threshold (0–255)", threshold);
        body.Children.Add(new TextBlock { Text = "LAMP TRAY GENERATION", FontWeight = global::Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        Add("Tray inflation percent", inflation); Add("Tray padding pixels", padding); body.Children.Add(clamp);
        body.Children.Add(error);
        var apply = new Button { Content = "Apply", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, MinWidth = 100 };
        body.Children.Add(apply);
        var dialog = new Window { Title = "Face Generation Settings", Width = 520, Height = 620, Content = new ScrollViewer { Content = body }, CanResize = true };
        var saved = false;
        apply.Click += (_, _) =>
        {
            try
            {
                var updated = new FaceGenerationSettingsModel
                {
                    MaskExtractionThreshold = Byte(threshold, "Mask threshold"),
                    TrayBoundsInflationPercent = NonNegative(inflation, "Tray inflation percent"),
                    TrayBoundsPaddingPixels = NonNegative(padding, "Tray padding pixels"),
                    ClampTrayBoundsToLampWindow = clamp.IsChecked == true,
                    PostWarpSharpeningEnabled = sharpening.IsChecked == true,
                    PostWarpSharpeningAmount = Read(amount, "Sharpen amount", 0, 2),
                    PostWarpSharpeningRadiusPixels = Read(radius, "Sharpen radius", .1, 3),
                    PostWarpSharpeningThreshold = Integer(sharpenThreshold, "Sharpen threshold", 0, 255)
                };
                faces.UpdateGenerationSettings(updated);
                saved = true;
                dialog.Close();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
            { error.Text = exception.Message; }
        };
        await dialog.ShowDialog(owner);
        return saved;

        void Add(string label, TextBox control)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,140"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            Grid.SetColumn(control, 1); row.Children.Add(control); body.Children.Add(row);
        }
        static TextBox Number(object value) => new() { Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" };
        static double Read(TextBox box, string label, double min, double max)
            => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value >= min && value <= max
                ? value : throw new ArgumentException($"{label} must be a finite number from {min} to {max}.");
        static double NonNegative(TextBox box, string label)
            => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value >= 0
                ? value : throw new ArgumentException($"{label} must be a finite non-negative number.");
        static byte Byte(TextBox box, string label) => (byte)Integer(box, label, 0, 255);
        static int Integer(TextBox box, string label, int min, int max)
            => int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max
                ? value : throw new ArgumentException($"{label} must be a whole number from {min} to {max}.");
    }
}
