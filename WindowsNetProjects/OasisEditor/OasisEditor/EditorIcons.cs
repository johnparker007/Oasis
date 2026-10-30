using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Dock.Model.Core;

namespace OasisEditor.Avalonia;

public enum EditorIconKind { None, Hierarchy, Inspector, Project, Console, Lamp, SevenSegment, Reel, Button, SegmentedAlpha, DotMatrixAlpha, Background }

/// <summary>
/// Approved oasis-icon-study.html designs (2026-10-29), in the original 16-unit
/// view box. Retains the corrected seven-segment polygons and curved button base.
/// </summary>
public static class EditorIcons
{
    private sealed record Shape(Geometry Geometry, bool Filled = false, double StrokeWidth = 1.25, PenLineCap Cap = PenLineCap.Round);
    private static Shape Line(string path, double width = 1.25, PenLineCap cap = PenLineCap.Round) => new(Geometry.Parse(path), StrokeWidth: width, Cap: cap);
    private static Shape Fill(string path) => new(Geometry.Parse(path), true);
    // Approved oasis-alpha-icon-study.html artwork (2026-10-29). Both designs
    // share the exact SVG rect; N1 overrides only the character stroke/caps.
    private static readonly Shape AlphaFrame = new(new RectangleGeometry(new Rect(1, 3, 14, 10), 1.25, 1.25));
    private static readonly IReadOnlyDictionary<EditorIconKind, Shape[]> Shapes = new Dictionary<EditorIconKind, Shape[]>
    {
        [EditorIconKind.Hierarchy] = [Line("M6.6 2H9.4A.6 .6 0 0 1 10 2.6V4.4A.6 .6 0 0 1 9.4 5H6.6A.6 .6 0 0 1 6 4.4V2.6A.6 .6 0 0 1 6.6 2Z M2.6 11H5.4A.6 .6 0 0 1 6 11.6V13.4A.6 .6 0 0 1 5.4 14H2.6A.6 .6 0 0 1 2 13.4V11.6A.6 .6 0 0 1 2.6 11Z M10.6 11H13.4A.6 .6 0 0 1 14 11.6V13.4A.6 .6 0 0 1 13.4 14H10.6A.6 .6 0 0 1 10 13.4V11.6A.6 .6 0 0 1 10.6 11Z M8 5v3M4 11V8h8v3")],
        // Interrupt the tracks at the handles: no background-coloured masking.
        [EditorIconKind.Inspector] = [Line("M2 4H5 M7 4H14 M2 8H10 M12 8H14 M2 12H4 M6 12H14 M5.5 2.5H6.5A.5 .5 0 0 1 7 3V5A.5 .5 0 0 1 6.5 5.5H5.5A.5 .5 0 0 1 5 5V3A.5 .5 0 0 1 5.5 2.5Z M10.5 6.5H11.5A.5 .5 0 0 1 12 7V9A.5 .5 0 0 1 11.5 9.5H10.5A.5 .5 0 0 1 10 9V7A.5 .5 0 0 1 10.5 6.5Z M4.5 10.5H5.5A.5 .5 0 0 1 6 11V13A.5 .5 0 0 1 5.5 13.5H4.5A.5 .5 0 0 1 4 13V11A.5 .5 0 0 1 4.5 10.5Z")],
        [EditorIconKind.Project] = [Line("M2 5V3.5a1 1 0 0 1 1-1h3l2 2h5a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V5Z M2 5.5h12")],
        [EditorIconKind.Console] = [Line("M3 2.5H13A1 1 0 0 1 14 3.5V12.5A1 1 0 0 1 13 13.5H3A1 1 0 0 1 2 12.5V3.5A1 1 0 0 1 3 2.5Z m1.5 3.5 2 2-2 2M9 10h2.5")],
        [EditorIconKind.Lamp] = [Line("M5.5 10.5c0-1.8-2-2.2-2-4.5a4.5 4.5 0 0 1 9 0c0 2.3-2 2.7-2 4.5Z M5.5 12.5h5M7 14h2")],
        [EditorIconKind.SevenSegment] = [Fill("M5 2 6 1h4l1 1-1 1H6Z M4 2.5l1 1v3l-1 1-1-1v-3Z M12 2.5l1 1v3l-1 1-1-1v-3Z M5 8l1-1h4l1 1-1 1H6Z M4 8.5l1 1v3l-1 1-1-1v-3Z M12 8.5l1 1v3l-1 1-1-1v-3Z M5 14l1-1h4l1 1-1 1H6Z")],
        [EditorIconKind.Reel] = [Line("M5 1.5H11A2 2 0 0 1 13 3.5V12.5A2 2 0 0 1 11 14.5H5A2 2 0 0 1 3 12.5V3.5A2 2 0 0 1 5 1.5Z M3 5h10M3 11h10"), Fill("m8 6.5 1.5 1.5L8 9.5 6.5 8Z")],
        [EditorIconKind.Button] = [Line("M2 6v4a6 3 0 0 0 12 0V6 M14 6A6 3 0 1 0 2 6A6 3 0 1 0 14 6Z M5 5.5c1-.6 3-.8 4.5-.4")],
        [EditorIconKind.SegmentedAlpha] = [AlphaFrame, Line("M3.5 5.3v2.3m0 .8v2.3M7.5 5.3v2.3m0 .8v2.3M4 5.5l1.25 2m.5 1 1.25 2M11.5 5.3v2.3m0 .8v2.3M9.5 6.5l1.25-1", 1, PenLineCap.Flat)],
        [EditorIconKind.DotMatrixAlpha] = DotMatrixAlpha(),
        // Shared background swatch for component rows and their automatic groups.
        [EditorIconKind.Background] = [Line("M2 2H14V14H2Z"), Fill("M5 5H11V11H5Z")]
    };

    private static Shape[] DotMatrixAlpha()
    {
        var shapes = new List<Shape> { AlphaFrame };
        string[] letters = ["010101111101101", "010110010010111"];
        for (var character = 0; character < letters.Length; character++)
            for (var i = 0; i < letters[character].Length; i++)
                if (letters[character][i] == '1')
                {
                    var x = Math.Round(3.8 + character * 5.6 + i % 3 * 1.4, 2);
                    var y = Math.Round(5.2 + i / 3 * 1.4, 2);
                    shapes.Add(new Shape(new EllipseGeometry(new Rect(x - .48, y - .48, .96, .96)), Filled: true));
                }
        return shapes.ToArray();
    }

    internal static void Draw(DrawingContext context, EditorIconKind kind, IBrush? brush)
    {
        if (!Shapes.TryGetValue(kind, out var shapes) || brush is null) return;
        foreach (var shape in shapes)
        {
            var pen = shape.Filled ? null : new Pen(brush, shape.StrokeWidth, lineCap: shape.Cap, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(shape.Filled ? brush : null, pen, shape.Geometry);
        }
    }

    public static EditorIconKind ForPane(string? id) => id switch
    { "Hierarchy" => EditorIconKind.Hierarchy, "Inspector" => EditorIconKind.Inspector,
      "Assets" or "Project" => EditorIconKind.Project, "Output" => EditorIconKind.Console, _ => EditorIconKind.None };

    public static EditorIconKind ForComponent(string? kind) => kind?.ToLowerInvariant() switch
    { "lamp" or "lampwindow" => EditorIconKind.Lamp, "sevensegment" or "sevensegmentdisplay" => EditorIconKind.SevenSegment,
      "alpha" or "alphadisplay" => EditorIconKind.SegmentedAlpha,
      "vfddotmatrix" => EditorIconKind.DotMatrixAlpha,
      "background" => EditorIconKind.Background,
      "reel" or "reelmount" => EditorIconKind.Reel, "button" => EditorIconKind.Button, _ => EditorIconKind.None };

    internal static Control Label(EditorIconKind kind, string? label, double indent = 0)
    {
        var text = new TextBlock { Text = label ?? "", TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        if (kind == EditorIconKind.None) { text.Margin = new Thickness(indent, 0, 0, 0); return text; }
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6, Margin = new Thickness(indent, 0, 0, 0) };
        row.Children.Add(new EditorIcon { Kind = kind });
        Grid.SetColumn(text, 1); row.Children.Add(text);
        AutomationProperties.SetName(row, label ?? "");
        return row;
    }
}

/// <summary>Decorative vector control. Strokes remain strokes at both 16 and 20 DIP.</summary>
public sealed class EditorIcon : TemplatedControl
{
    public static readonly StyledProperty<EditorIconKind> KindProperty = AvaloniaProperty.Register<EditorIcon, EditorIconKind>(nameof(Kind));
    public EditorIconKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    static EditorIcon() => AffectsRender<EditorIcon>(KindProperty, ForegroundProperty);
    public EditorIcon() { Width = Height = 16; IsHitTestVisible = false; Focusable = false; VerticalAlignment = VerticalAlignment.Center; }
    public override void Render(DrawingContext context)
    {
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 16;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation((Bounds.Width - 16 * scale) / 2, (Bounds.Height - 16 * scale) / 2)))
            EditorIcons.Draw(context, Kind, Foreground);
    }
}

/// <summary>Uses Dock's supported header template, including floating tool tabs.</summary>
public sealed class WorkspacePaneHeaderTemplate : IDataTemplate
{
    public bool Match(object? data) => data is IDockable;
    public Control Build(object? data)
    {
        if (data is not IDockable pane) return new TextBlock { Text = data?.ToString() ?? "" };
        var row = EditorIcons.Label(EditorIcons.ForPane(pane.Id), pane.Title);
        var text = row is TextBlock block ? block : ((Grid)row).Children.OfType<TextBlock>().Single();
        text.Bind(TextBlock.TextProperty, new Binding(nameof(IDockable.Title)) { Source = pane });
        return row;
    }
}
