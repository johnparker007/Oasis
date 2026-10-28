using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Globalization;
using Media = global::Avalonia.Media;
using Layout = global::Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private void InitializeFaceReels(MenuItem create)
    {
        var item = new MenuItem { Header = "Reel mount" };
        item.Click += (_, _) => Try(() =>
        {
            RequireFace();
            _faces.AddReelMount("Reel mount", 24, 24, 120, 80, reelIndex: 0);
            Refresh();
        });
        create.Items.Add(item);
    }

    private void AppendFaceReelInspector(string id)
    {
        var reel = _faces.Document?.Elements.OfType<FaceReelMount>()
            .FirstOrDefault(element => string.Equals(element.ObjectId, id, StringComparison.Ordinal));
        if (reel is null) return;

        Heading("REEL MOUNT SETTINGS");
        var reference = new TextBox { Text = LogicalReelIndex(reel)?.ToString(CultureInfo.InvariantCulture) ?? "" };
        var symbolBand = new TextBox { Text = reel.AssetPath ?? "" };
        var stops = new TextBox { Text = reel.Stops?.ToString(CultureInfo.InvariantCulture) ?? "" };
        var visibleScale = new TextBox { Text = Format(reel.VisibleScale) };
        var bandOffset = new TextBox { Text = Format(reel.BandOffset) };
        var topLamp = new TextBox { Text = LampNumber(reel, "top") };
        var middleLamp = new TextBox { Text = LampNumber(reel, "middle") };
        var bottomLamp = new TextBox { Text = LampNumber(reel, "bottom") };
        var transmissionMask = new TextBox { Text = reel.ReelLampTransmissionMaskAssetPath ?? "" };
        var reversed = new CheckBox { Content = "Reverse symbol band", IsChecked = reel.IsReversed };
        var lampsEnabled = new CheckBox { Content = "Enable reel lamps", IsChecked = reel.ReelLampsEnabled };
        var opaque = new CheckBox { Content = "Opaque reel", IsChecked = reel.IsOpaqueReel };

        AddField("Logical reel ID", reference);
        AddField("Symbol-band image", symbolBand);
        _inspector.Children.Add(ActionButton("Import symbol-band image…", () => ImportSymbolBand(id)));
        _inspector.Children.Add(ActionButton("Generate transmission mask", () => Try(() =>
        {
            _faces.GenerateTransmissionMask(id);
            _inspected = null;
            Refresh();
        })));
        AddField("Stops", stops);
        AddField("Visible scale", visibleScale);
        AddField("Band offset", bandOffset);
        AddField("Top lamp number", topLamp);
        AddField("Middle lamp number", middleLamp);
        AddField("Bottom lamp number", bottomLamp);
        _inspector.Children.Add(lampsEnabled);
        _inspector.Children.Add(reversed);
        _inspector.Children.Add(opaque);
        AddField("Transmission mask", transmissionMask);
        _inspector.Children.Add(new TextBlock
        {
            Text = "Leave optional numeric fields blank to clear them. A blank image or reel reference also clears that assignment.",
            TextWrapping = Media.TextWrapping.Wrap,
            Opacity = .7
        });
        _inspector.Children.Add(ActionButton("Apply reel settings", () => Try(() =>
        {
            int? ReadInt(TextBox box, string label) => string.IsNullOrWhiteSpace(box.Text)
                ? null
                : int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? value : throw new ArgumentException($"{label} must be a whole number.");
            double? ReadDouble(TextBox box, string label) => string.IsNullOrWhiteSpace(box.Text)
                ? null
                : double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                    ? value : throw new ArgumentException($"{label} must be a finite number.");

            var reelIndex = ReadInt(reference, "Logical reel ID");
            var stopCount = ReadInt(stops, "Stops");
            var scale = ReadDouble(visibleScale, "Visible scale");
            var offset = ReadDouble(bandOffset, "Band offset");
            var top = ReadInt(topLamp, "Top lamp number");
            var middle = ReadInt(middleLamp, "Middle lamp number");
            var bottom = ReadInt(bottomLamp, "Bottom lamp number");
            _faces.UpdateReelMount(id, new FaceReelMountUpdateOptions
            {
                LogicalReelIndex = reelIndex,
                ClearLogicalReelReference = reelIndex is null,
                SymbolBandAssetPath = string.IsNullOrWhiteSpace(symbolBand.Text) ? null : symbolBand.Text.Trim(),
                ClearSymbolBandAssetPath = string.IsNullOrWhiteSpace(symbolBand.Text),
                Stops = stopCount,
                ClearStops = stopCount is null,
                VisibleScale = scale,
                ClearVisibleScale = scale is null,
                BandOffset = offset,
                ClearBandOffset = offset is null,
                IsReversed = reversed.IsChecked == true,
                ReelLampsEnabled = lampsEnabled.IsChecked == true,
                TopLampNumber = top,
                ClearTopLampNumber = top is null,
                MiddleLampNumber = middle,
                ClearMiddleLampNumber = middle is null,
                BottomLampNumber = bottom,
                ClearBottomLampNumber = bottom is null,
                IsOpaqueReel = opaque.IsChecked == true,
                TransmissionMaskAssetPath = string.IsNullOrWhiteSpace(transmissionMask.Text) ? null : transmissionMask.Text.Trim(),
                ClearTransmissionMaskAssetPath = string.IsNullOrWhiteSpace(transmissionMask.Text)
            });
            _inspected = null;
            Refresh();
        })));

        void AddField(string label, Control control)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,130"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = Layout.VerticalAlignment.Center, TextWrapping = Media.TextWrapping.Wrap });
            Grid.SetColumn(control, 1);
            row.Children.Add(control);
            _inspector.Children.Add(row);
        }
    }

    private async void ImportSymbolBand(string elementId)
    {
        if (!_faceActive || _faces.FacePath is null) { SetStatus("Open a Face asset before importing a reel symbol band."); return; }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import reel symbol-band image",
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }]
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
            Try(() => WithSelectedImportFile(path, source => _faces.ImportSymbolBand(elementId, source)));
        _inspected = null;
        Refresh();
    }

    private static int? LogicalReelIndex(FaceReelMount reel)
        => reel.LinkedMachineObjectReference is { Kind: MachineObjectKind.Reel } reference
            && int.TryParse(reference.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static string LampNumber(FaceReelMount reel, string position)
        => reel.ReelLamps.FirstOrDefault(lamp => string.Equals(lamp.Position.ToString(), position, StringComparison.OrdinalIgnoreCase))?.LampNumber?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static string Format(double? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "";
}
