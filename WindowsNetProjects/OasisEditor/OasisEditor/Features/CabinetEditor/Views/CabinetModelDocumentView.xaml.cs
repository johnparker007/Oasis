using System.Windows.Controls;
using OasisEditor;

namespace OasisEditor.Features.CabinetEditor.Views;

public partial class CabinetModelDocumentView : UserControl
{
    public CabinetModelDocumentView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as DocumentTabViewModel)?.CabinetViewer?.ReflectionEditor.Attach();
        Unloaded += (_, _) => (DataContext as DocumentTabViewModel)?.ExistingCabinetViewer?.ReflectionEditor.Detach();
    }
}
