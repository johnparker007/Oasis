using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor.Views;

public partial class EditorViewportStatusBar : UserControl
{
    public static readonly DependencyProperty ContentDimensionsProperty = DependencyProperty.Register(nameof(ContentDimensions), typeof(string), typeof(EditorViewportStatusBar), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty PointerCoordinatesProperty = DependencyProperty.Register(nameof(PointerCoordinates), typeof(string), typeof(EditorViewportStatusBar), new PropertyMetadata("X: —  Y: —"));
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(EditorViewportStatusBar), new PropertyMetadata(1d, OnZoomChanged));

    public event EventHandler? FitRequested;
    public event EventHandler<double>? ZoomRequested;

    public string ContentDimensions { get => (string)GetValue(ContentDimensionsProperty); set => SetValue(ContentDimensionsProperty, value); }
    public string PointerCoordinates { get => (string)GetValue(PointerCoordinatesProperty); set => SetValue(PointerCoordinatesProperty, value); }
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    public EditorViewportStatusBar() { InitializeComponent(); UpdateZoomText(); }

    private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EditorViewportStatusBar)d).UpdateZoomText();
    private void UpdateZoomText() => ZoomBox.Text = $"{Zoom * 100d:0.##}%";
    private void OnFit(object sender, RoutedEventArgs e) => FitRequested?.Invoke(this, EventArgs.Empty);
    private void OnZoomKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { ApplyText(); e.Handled = true; } }
    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        if (ZoomBox.SelectedItem is ComboBoxItem { Tag: string tag } && double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var zoom))
            ZoomRequested?.Invoke(this, zoom);
        else ApplyText();
    }
    private void ApplyText()
    {
        if (EditorViewportTransform.TryParseZoomPercentage(ZoomBox.Text, out var zoom)) ZoomRequested?.Invoke(this, zoom);
        else UpdateZoomText();
        ZoomBox.SelectedItem = null;
    }
}
