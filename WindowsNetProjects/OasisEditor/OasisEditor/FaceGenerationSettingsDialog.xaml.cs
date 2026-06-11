using System.Windows;

namespace OasisEditor;

public partial class FaceGenerationSettingsDialog : Window
{
    private readonly FaceGenerationSettingsViewModel _viewModel;

    public FaceGenerationSettingsDialog(FaceGenerationSettingsModel settings, string actionText)
    {
        InitializeComponent();
        _viewModel = new FaceGenerationSettingsViewModel(settings);
        DataContext = _viewModel;
        ActionButton.Content = string.IsNullOrWhiteSpace(actionText) ? "OK" : actionText;
    }

    public FaceGenerationSettingsModel Settings => _viewModel.Settings;

    private void OnActionClicked(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryCreateSettings(out _))
        {
            return;
        }

        DialogResult = true;
    }
}
