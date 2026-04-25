using System.Windows.Controls;
using System.Windows.Input;
using OasisEditor;

namespace OasisEditor.Views;

public partial class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        InitializeComponent();
    }

    private void OnAssetListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AssetBrowserViewModel viewModel)
        {
            return;
        }

        var command = viewModel.OpenAssetCommand;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
