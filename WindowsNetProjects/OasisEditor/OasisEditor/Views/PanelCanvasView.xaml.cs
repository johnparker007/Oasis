using System.Windows;
using System.Windows.Controls;

namespace OasisEditor.Views;

public partial class PanelCanvasView : UserControl
{
    public static readonly DependencyProperty IsRectangleToolActiveProperty =
        DependencyProperty.Register(
            nameof(IsRectangleToolActive),
            typeof(bool),
            typeof(PanelCanvasView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsImageToolActiveProperty =
        DependencyProperty.Register(
            nameof(IsImageToolActive),
            typeof(bool),
            typeof(PanelCanvasView),
            new PropertyMetadata(false));

    public PanelCanvasView()
    {
        InitializeComponent();
    }

    public bool IsRectangleToolActive
    {
        get => (bool)GetValue(IsRectangleToolActiveProperty);
        set => SetValue(IsRectangleToolActiveProperty, value);
    }

    public bool IsImageToolActive
    {
        get => (bool)GetValue(IsImageToolActiveProperty);
        set => SetValue(IsImageToolActiveProperty, value);
    }
}
