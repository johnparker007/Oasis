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
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var command = viewModel.OpenAssetCommand;
        var selectedAsset = viewModel.SelectedAsset;
        if (command.CanExecute(selectedAsset))
        {
            command.Execute(selectedAsset);
        }
    }
}
