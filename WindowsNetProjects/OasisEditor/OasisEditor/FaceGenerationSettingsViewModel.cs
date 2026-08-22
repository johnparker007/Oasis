using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace OasisEditor;

public sealed class FaceGenerationSettingsViewModel : INotifyPropertyChanged
{
    private string _maskExtractionThresholdText;
    private string _trayBoundsInflationPercentText;
    private string _trayBoundsPaddingPixelsText;
    private bool _clampTrayBoundsToLampWindow;
    private bool _postWarpSharpeningEnabled;
    private string _postWarpSharpeningAmountText;
    private string _postWarpSharpeningRadiusPixelsText;
    private string _postWarpSharpeningThresholdText;
    private string _errorMessage = string.Empty;

    public FaceGenerationSettingsViewModel(FaceGenerationSettingsModel settings)
    {
        var normalized = (settings ?? FaceGenerationSettingsModel.Default).Normalize();
        _maskExtractionThresholdText = normalized.MaskExtractionThreshold.ToString(CultureInfo.InvariantCulture);
        _trayBoundsInflationPercentText = normalized.TrayBoundsInflationPercent.ToString("0.##", CultureInfo.InvariantCulture);
        _trayBoundsPaddingPixelsText = normalized.TrayBoundsPaddingPixels.ToString("0.##", CultureInfo.InvariantCulture);
        _clampTrayBoundsToLampWindow = normalized.ClampTrayBoundsToLampWindow;
        _postWarpSharpeningEnabled = normalized.PostWarpSharpeningEnabled;
        _postWarpSharpeningAmountText = normalized.PostWarpSharpeningAmount.ToString("0.##", CultureInfo.InvariantCulture);
        _postWarpSharpeningRadiusPixelsText = normalized.PostWarpSharpeningRadiusPixels.ToString("0.##", CultureInfo.InvariantCulture);
        _postWarpSharpeningThresholdText = normalized.PostWarpSharpeningThreshold.ToString(CultureInfo.InvariantCulture);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string MaskExtractionThresholdText
    {
        get => _maskExtractionThresholdText;
        set => SetProperty(ref _maskExtractionThresholdText, value);
    }

    public string TrayBoundsInflationPercentText
    {
        get => _trayBoundsInflationPercentText;
        set => SetProperty(ref _trayBoundsInflationPercentText, value);
    }

    public string TrayBoundsPaddingPixelsText
    {
        get => _trayBoundsPaddingPixelsText;
        set => SetProperty(ref _trayBoundsPaddingPixelsText, value);
    }

    public bool ClampTrayBoundsToLampWindow
    {
        get => _clampTrayBoundsToLampWindow;
        set => SetProperty(ref _clampTrayBoundsToLampWindow, value);
    }

    public bool PostWarpSharpeningEnabled
    {
        get => _postWarpSharpeningEnabled;
        set => SetProperty(ref _postWarpSharpeningEnabled, value);
    }

    public string PostWarpSharpeningAmountText { get => _postWarpSharpeningAmountText; set => SetProperty(ref _postWarpSharpeningAmountText, value); }
    public string PostWarpSharpeningRadiusPixelsText { get => _postWarpSharpeningRadiusPixelsText; set => SetProperty(ref _postWarpSharpeningRadiusPixelsText, value); }
    public string PostWarpSharpeningThresholdText { get => _postWarpSharpeningThresholdText; set => SetProperty(ref _postWarpSharpeningThresholdText, value); }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public FaceGenerationSettingsModel Settings => TryCreateSettings(out var settings) ? settings : FaceGenerationSettingsModel.Default;

    public bool TryCreateSettings(out FaceGenerationSettingsModel settings)
    {
        settings = FaceGenerationSettingsModel.Default;
        ErrorMessage = string.Empty;

        if (!TryReadBoundedDouble(PostWarpSharpeningAmountText, 0d, 2d, out var sharpenAmount))
        {
            ErrorMessage = "Sharpen amount must be a finite number from 0 to 2.";
            return false;
        }
        if (!TryReadBoundedDouble(PostWarpSharpeningRadiusPixelsText, 0.1d, 3d, out var sharpenRadius))
        {
            ErrorMessage = "Sharpen radius must be a finite number from 0.1 to 3 pixels.";
            return false;
        }
        if (!int.TryParse(PostWarpSharpeningThresholdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharpenThreshold)
            || sharpenThreshold is < 0 or > 255)
        {
            ErrorMessage = "Sharpen threshold must be a whole number from 0 to 255.";
            return false;
        }

        if (!byte.TryParse(MaskExtractionThresholdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold))
        {
            ErrorMessage = "Mask extraction threshold must be a whole number from 0 to 255.";
            return false;
        }

        if (!TryReadNonNegativeDouble(TrayBoundsInflationPercentText, out var inflationPercent))
        {
            ErrorMessage = "Tray inflation percent must be a finite non-negative number.";
            return false;
        }

        if (!TryReadNonNegativeDouble(TrayBoundsPaddingPixelsText, out var paddingPixels))
        {
            ErrorMessage = "Tray padding pixels must be a finite non-negative number.";
            return false;
        }

        settings = new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = threshold,
            TrayBoundsInflationPercent = inflationPercent,
            TrayBoundsPaddingPixels = paddingPixels,
            ClampTrayBoundsToLampWindow = ClampTrayBoundsToLampWindow,
            PostWarpSharpeningEnabled = PostWarpSharpeningEnabled,
            PostWarpSharpeningAmount = sharpenAmount,
            PostWarpSharpeningRadiusPixels = sharpenRadius,
            PostWarpSharpeningThreshold = sharpenThreshold
        }.Normalize();
        return true;
    }

    private static bool TryReadNonNegativeDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            && !double.IsNaN(result)
            && !double.IsInfinity(result)
            && result >= 0d;
    }

    private static bool TryReadBoundedDouble(string? value, double minimum, double maximum, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
        && double.IsFinite(result) && result >= minimum && result <= maximum;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
