using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor.Views;

public partial class FaceWorkspaceView : UserControl
{
    public FaceWorkspaceView() => InitializeComponent();

    private void OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape || DataContext is not DocumentTabViewModel document) return;
        if(document.FaceWorkspace?.IsComponentPlacementActive==true) document.FaceWorkspace.CancelComponentPlacement();
        else if(document.FaceWorkspace?.IsLampPlacementActive==true) document.FaceWorkspace.CancelLampPlacement();
        else if(document.CalibrationPlacement is not null) document.CancelCalibrationPlacement();
        else return;
        eventArgs.Handled = true;
    }
}
