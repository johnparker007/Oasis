using System.Globalization;
using System.Windows;

namespace OasisEditor;

public partial class GenerateFaceFromRegionDialog : Window
{
    public GenerateFaceFromRegionDialog()
    {
        InitializeComponent();
    }

    public Rect SourceRegion { get; private set; }

    private void OnGenerateClicked(object sender, RoutedEventArgs e)
    {
        if (!TryReadDouble(XTextBox.Text, out var x)
            || !TryReadDouble(YTextBox.Text, out var y)
            || !TryReadDouble(WidthTextBox.Text, out var width)
            || !TryReadDouble(HeightTextBox.Text, out var height)
            || width <= 0
            || height <= 0)
        {
            ErrorTextBlock.Text = "Enter finite numeric X, Y, Width, and Height values. Width and Height must be greater than zero.";
            return;
        }

        SourceRegion = new Rect(x, y, width, height);
        DialogResult = true;
    }

    private static bool TryReadDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            && !double.IsNaN(result)
            && !double.IsInfinity(result);
    }
}
