using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    internal void AppendPanelInspectorParity(string id)
    {
        if (_faceActive || _machineActive) return;
        var element = ActiveElements.FirstOrDefault(value => value.Id == id);
        if (element is null || element.Kind is not ("image" or "background" or "lamp" or "reel" or "label")) return;
        var settings = _session.GetPanelElementInspectorSettings(id);
        _inspector.Children.Add(new TextBlock { Text = "PANEL ASSET AND LABEL SETTINGS", FontSize = 10, Opacity = .65,
            Margin = new global::Avalonia.Thickness(0, 8, 0, 0) });
        var fields = new Dictionary<string, TextBox>(StringComparer.Ordinal);
        void Add(string name, string? value)
        {
            var box = new TextBox { Text = value ?? string.Empty, MinWidth = 85 };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            Grid.SetColumn(box, 1); row.Children.Add(box); _inspector.Children.Add(row); fields[name] = box;
        }
        if (element.Kind is "image" or "background" or "lamp" or "reel") Add("Asset Path", settings.AssetPath);
        if (element.Kind is "background" or "reel") Add("Secondary Asset", settings.SecondaryAssetPath);
        if (element.Kind == "background") Add("Color", settings.BackgroundColorHex);
        if (element.Kind == "label")
        {
            Add("Lamp Number", settings.LampNumber?.ToString(CultureInfo.InvariantCulture));
            Add("Display Text", settings.DisplayText);
            Add("Text Color", settings.TextColorHex);
        }
        BindInspectorEdits(() => {
            int? lampNumber = null;
            if (element.Kind == "label" && !string.IsNullOrWhiteSpace(fields["Lamp Number"].Text))
                lampNumber = int.TryParse(fields["Lamp Number"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed : throw new ArgumentException("Lamp Number must be a whole number zero or greater.");
            _session.UpdatePanelElementInspectorSettings(id, new PanelElementInspectorSettings(
                element.Kind is "image" or "background" or "lamp" or "reel" ? fields["Asset Path"].Text : settings.AssetPath,
                element.Kind is "background" or "reel" ? fields["Secondary Asset"].Text : settings.SecondaryAssetPath,
                element.Kind == "background" ? fields["Color"].Text : settings.BackgroundColorHex,
                element.Kind == "label" ? lampNumber : settings.LampNumber,
                element.Kind == "label" ? fields["Text Color"].Text : settings.TextColorHex,
                element.Kind == "label" ? fields["Display Text"].Text : settings.DisplayText));
        });
    }
}
