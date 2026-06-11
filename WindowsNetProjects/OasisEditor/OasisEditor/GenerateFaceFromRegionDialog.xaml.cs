using System.Globalization;
using System.Windows;

namespace OasisEditor;

public partial class GenerateFaceFromRegionDialog : Window
{
    private readonly FaceGenerationSettingsViewModel _settingsViewModel;
    private bool _showGenerationSettings = true;

    public GenerateFaceFromRegionDialog()
        : this(FaceGenerationSettingsModel.Default, true)
    {
    }

    public GenerateFaceFromRegionDialog(FaceGenerationSettingsModel generationSettings, bool showGenerationSettings)
    {
        InitializeComponent();
        _settingsViewModel = new FaceGenerationSettingsViewModel(generationSettings);
        ShowGenerationSettings = showGenerationSettings;
        ApplyGenerationSettingsToFields();
    }

    public Rect SourceRegion { get; private set; }
    public FaceGenerationSettingsModel GenerationSettings => _settingsViewModel.Settings;

    public bool ShowGenerationSettings
    {
        get => _showGenerationSettings;
        set
        {
            _showGenerationSettings = value;
            var visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (SettingsHeaderTextBlock is null)
            {
                return;
            }

            SettingsHeaderTextBlock.Visibility = visibility;
            ThresholdLabel.Visibility = visibility;
            MaskThresholdTextBox.Visibility = visibility;
            InflationLabel.Visibility = visibility;
            TrayInflationTextBox.Visibility = visibility;
            PaddingLabel.Visibility = visibility;
            TrayPaddingTextBox.Visibility = visibility;
            ClampTrayCheckBox.Visibility = visibility;
        }
    }

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

        if (ShowGenerationSettings)
        {
            ReadGenerationSettingsFromFields();
            if (!_settingsViewModel.TryCreateSettings(out _))
            {
                ErrorTextBlock.Text = _settingsViewModel.ErrorMessage;
                return;
            }
        }

        SourceRegion = new Rect(x, y, width, height);
        DialogResult = true;
    }

    private void ApplyGenerationSettingsToFields()
    {
        MaskThresholdTextBox.Text = _settingsViewModel.MaskExtractionThresholdText;
        TrayInflationTextBox.Text = _settingsViewModel.TrayBoundsInflationPercentText;
        TrayPaddingTextBox.Text = _settingsViewModel.TrayBoundsPaddingPixelsText;
        ClampTrayCheckBox.IsChecked = _settingsViewModel.ClampTrayBoundsToLampWindow;
    }

    private void ReadGenerationSettingsFromFields()
    {
        _settingsViewModel.MaskExtractionThresholdText = MaskThresholdTextBox.Text;
        _settingsViewModel.TrayBoundsInflationPercentText = TrayInflationTextBox.Text;
        _settingsViewModel.TrayBoundsPaddingPixelsText = TrayPaddingTextBox.Text;
        _settingsViewModel.ClampTrayBoundsToLampWindow = ClampTrayCheckBox.IsChecked == true;
    }

    private static bool TryReadDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            && !double.IsNaN(result)
            && !double.IsInfinity(result);
    }
}
