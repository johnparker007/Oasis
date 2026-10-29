using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;

namespace OasisEditor.Avalonia;

/// <summary>
/// Dock exposes a template for tab headers, but its tool title bar exposes only
/// a string Title. Add a decorative sibling in PART_Grip, retaining the original
/// label, theme, buttons and all drag/context-menu hit targets.
/// </summary>
public sealed class ToolChromeIcons : AvaloniaObject
{
    public static readonly AttachedProperty<bool> ShowIconProperty =
        AvaloniaProperty.RegisterAttached<ToolChromeIcons, ToolChromeControl, bool>("ShowIcon");
    public static bool GetShowIcon(ToolChromeControl control) => control.GetValue(ShowIconProperty);
    public static void SetShowIcon(ToolChromeControl control, bool value) => control.SetValue(ShowIconProperty, value);
    private static readonly ConditionalWeakTable<ToolChromeControl, Header> Headers = new();

    static ToolChromeIcons() => ShowIconProperty.Changed.AddClassHandler<ToolChromeControl>((chrome, change) =>
    {
        if (change.NewValue is true) Headers.GetValue(chrome, c => new Header(c)).Enable();
        else if (Headers.TryGetValue(chrome, out var header)) header.Disable();
    });

    private sealed class Header(ToolChromeControl chrome)
    {
        private bool _enabled;
        private TextBlock? _title;
        private Panel? _panel;
        private EditorIcon? _icon;
        private Thickness _originalMargin;
        public void Enable()
        {
            if (_enabled) return;
            _enabled = true; chrome.TemplateApplied += Applied;
        }
        public void Disable()
        {
            _enabled = false; chrome.TemplateApplied -= Applied; Clear();
        }
        private void Clear()
        {
            if (_title is not null) { _title.PropertyChanged -= Changed; _title.Margin = _originalMargin; }
            if (_icon is not null) _panel?.Children.Remove(_icon);
            _title = null; _panel = null; _icon = null;
        }
        private void Applied(object? sender, TemplateAppliedEventArgs args)
        {
            Clear();
            var grip = args.NameScope.Find<Control>("PART_Grip");
            // The Fluent title label is the direct child of a Panel in the grip.
            _title = grip?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Parent is Panel);
            if (_title?.Parent is not Panel panel) return;
            _panel = panel; _originalMargin = _title.Margin;
            _title.PropertyChanged += Changed;
            _icon = new EditorIcon { HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
            _icon.Bind(TemplatedControl.ForegroundProperty, new Binding(nameof(TextBlock.Foreground)) { Source = _title });
            panel.Children.Add(_icon); Refresh();
        }
        private void Changed(object? sender, AvaloniaPropertyChangedEventArgs args)
        { if (args.Property == TextBlock.TextProperty) Refresh(); }
        private void Refresh()
        {
            if (_icon is null || _title is null) return;
            _icon.Kind = EditorIcons.ForPane(_title.Text); _icon.IsVisible = _icon.Kind != EditorIconKind.None;
            _title.Margin = _originalMargin + new Thickness(_icon.IsVisible ? 22 : 0, 0, 0, 0);
        }
    }
}
