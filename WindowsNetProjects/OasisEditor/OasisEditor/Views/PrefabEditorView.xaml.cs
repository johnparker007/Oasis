using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace OasisEditor.Views;

public partial class PrefabEditorView : UserControl
{
    public PrefabEditorView() => InitializeComponent();
    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Viewport.DataContext is not PrefabPreviewViewModel preview) return;
        // Input adapter only; authored selection and ownership live in the ViewModel.
        VisualTreeHelper.HitTest(Viewport.Viewport, null, result =>
        {
            if (result is RayMeshGeometry3DHitTestResult { ModelHit: GeometryModel3D geometry })
            { return preview.SelectGeometry(geometry) ? HitTestResultBehavior.Stop : HitTestResultBehavior.Continue; }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(e.GetPosition(Viewport.Viewport)));
    }
}
