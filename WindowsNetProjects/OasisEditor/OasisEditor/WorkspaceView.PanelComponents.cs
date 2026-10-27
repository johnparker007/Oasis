using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private void InitializePanelComponents(MenuItem createMenu)
    {
        foreach (var entry in new[]
        {
            ("Lamp", "lamp"), ("Reel", "reel"), ("7 Segment Display", "sevenSegment"),
            ("Segment Alpha", "alpha"), ("VFD Dot Matrix", "vfdDotMatrix")
        })
            MenuAction(createMenu, "Panel " + entry.Item1, () => Try(() =>
            {
                RequirePanel();
                _session.AddSpecialistElement(entry.Item2, 24, 24);
            }));
    }

    private void AppendPanelComponentInspector(string id)
    {
        if (_faceActive || _machineActive) return;
        var element = ActiveElements.FirstOrDefault(value => value.Id == id);
        if (element is null || element.Kind is not ("lamp" or "reel" or "sevenSegment" or "alpha" or "vfdDotMatrix")) return;
        var settings = _session.GetSpecialistSettings(id);
        if (settings is null) return;

        _inspector.Children.Add(new TextBlock { Text = "NATIVE COMPONENT", FontSize = 10, Opacity = .65, Margin = new global::Avalonia.Thickness(0, 5, 0, 0) });
        var fields = new Dictionary<string, TextBox>(StringComparer.Ordinal);
        TextBox Add(string label, string? value)
        {
            var box = new TextBox { Text = value ?? string.Empty, MinWidth = 85 };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            Grid.SetColumn(box, 1); row.Children.Add(box); _inspector.Children.Add(row); fields[label] = box; return box;
        }
        CheckBox AddCheck(string label, bool? value)
        {
            var check = new CheckBox { Content = label, IsChecked = value == true }; _inspector.Children.Add(check); return check;
        }
        string? NumberText(int? value) => value?.ToString(CultureInfo.InvariantCulture);
        if (element.Kind is "lamp" or "reel" or "sevenSegment") Add("Display Number", NumberText(settings.DisplayNumber));
        if (element.Kind is "lamp" or "sevenSegment" or "alpha" or "vfdDotMatrix") Add("On Color", settings.OnColorHex);
        if (element.Kind == "lamp")
        {
            Add("Off Color", settings.OffColorHex); Add("Text Color", settings.TextColorHex); Add("Display Text", settings.DisplayText);
        }
        if (element.Kind == "alpha")
        {
            Add("Text Color", settings.TextColorHex); Add("Display Text", settings.DisplayText);
            var combo = new ComboBox { ItemsSource = new[] { "led14seg", "led16seg" }, SelectedItem = settings.SegmentDisplayType ?? "led16seg" };
            AddControl("Segment Type", combo);
            var decimalPoint = AddCheck("Decimal Point", settings.ShowDecimalPoint);
            var comma = AddCheck("Comma", settings.ShowCommaTail);
            _inspector.Children.Add(ActionButton("Apply component settings", () => Apply(settings with
            {
                OnColorHex = Read("On Color"), TextColorHex = Read("Text Color"), DisplayText = Read("Display Text"),
                SegmentDisplayType = combo.SelectedItem?.ToString(), ShowDecimalPoint = decimalPoint.IsChecked == true,
                ShowCommaTail = comma.IsChecked == true
            })));
            return;
        }
        if (element.Kind == "lamp")
        {
            var border = AddCheck("Border", settings.HasBorder);
            _inspector.Children.Add(ActionButton("Apply component settings", () => Apply(settings with
            { DisplayNumber = ReadInt("Display Number"), OnColorHex = Read("On Color"), OffColorHex = Read("Off Color"), TextColorHex = Read("Text Color"), DisplayText = Read("Display Text"), HasBorder = border.IsChecked == true })));
            return;
        }
        if (element.Kind is "sevenSegment" or "vfdDotMatrix")
        {
            _inspector.Children.Add(ActionButton("Apply component settings", () => Apply(settings with
            { DisplayNumber = element.Kind == "sevenSegment" ? ReadInt("Display Number") : settings.DisplayNumber, OnColorHex = Read("On Color") })));
            return;
        }

        Add("Stops", NumberText(settings.Stops)); Add("Visible Scale", settings.VisibleScale?.ToString("G17", CultureInfo.InvariantCulture));
        Add("Band Offset", (settings.BandOffset ?? 0).ToString("G17", CultureInfo.InvariantCulture));
        var reversed = AddCheck("Reversed", settings.IsReversed);
        var lamps = AddCheck("Lamps Enabled", settings.ReelLampsEnabled);
        Add("Top Lamp Number", NumberText(settings.TopLampNumber)); Add("Middle Lamp Number", NumberText(settings.MiddleLampNumber)); Add("Bottom Lamp Number", NumberText(settings.BottomLampNumber));
        _inspector.Children.Add(ActionButton("Apply component settings", () => Apply(settings with
        {
            DisplayNumber = ReadInt("Display Number"), Stops = ReadInt("Stops"), VisibleScale = ReadDouble("Visible Scale"),
            BandOffset = ReadDouble("Band Offset"), IsReversed = reversed.IsChecked == true, ReelLampsEnabled = lamps.IsChecked == true,
            TopLampNumber = ReadInt("Top Lamp Number"), MiddleLampNumber = ReadInt("Middle Lamp Number"), BottomLampNumber = ReadInt("Bottom Lamp Number")
        })));

        string? Read(string label) => fields[label].Text;
        int? ReadInt(string label) => string.IsNullOrWhiteSpace(fields[label].Text) ? null
            : int.TryParse(fields[label].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw new ArgumentException($"{label} must be a whole number.");
        double? ReadDouble(string label) => double.TryParse(fields[label].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value : throw new ArgumentException($"{label} must be a finite number.");
        void Apply(PanelSpecialistSettings updated) => Try(() => _session.UpdateSpecialistSettings(id, updated));
        void AddControl(string label, Control control)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(control, 1); row.Children.Add(control); _inspector.Children.Add(row);
        }
    }
}
