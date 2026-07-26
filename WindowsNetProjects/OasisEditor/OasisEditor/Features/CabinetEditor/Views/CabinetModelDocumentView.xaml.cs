using System.Windows.Controls;
using OasisEditor;

namespace OasisEditor.Features.CabinetEditor.Views;

public partial class CabinetModelDocumentView : UserControl
{
    public CabinetModelDocumentView()
    {
        InitializeComponent();
    }

    private void OnCabinetViewportSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        (DataContext as DocumentTabViewModel)?.ExistingCabinetViewer?.UpdateViewportSize(e.NewSize.Width, e.NewSize.Height);
    }
}
