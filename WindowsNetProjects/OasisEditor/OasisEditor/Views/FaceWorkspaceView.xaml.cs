using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor.Views;

public partial class FaceWorkspaceView : UserControl
{
    public FaceWorkspaceView() => InitializeComponent();

    private void OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape || DataContext is not DocumentTabViewModel document || document.CalibrationPlacement is null) return;
        document.CancelCalibrationPlacement();
        eventArgs.Handled = true;
    }
}
