using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OasisEditor;

public abstract class InspectorPropertyRowViewModel : INotifyPropertyChanged
{
    private string _errorText = string.Empty;

    protected InspectorPropertyRowViewModel(string displayName, string groupName, bool isReadOnly)
    {
        DisplayName = displayName;
        GroupName = groupName;
        IsReadOnly = isReadOnly;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; }

    public string GroupName { get; }

    public bool IsReadOnly { get; }

    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    protected void RaisePropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public abstract class InspectorEditablePropertyRowViewModel : InspectorPropertyRowViewModel
{
    protected InspectorEditablePropertyRowViewModel(string displayName, string groupName, bool isReadOnly)
        : base(displayName, groupName, isReadOnly)
    {
    }

    public abstract void Commit();
}

public sealed class InspectorTextPropertyViewModel : InspectorEditablePropertyRowViewModel
{
    private string _value;
    private string _committedValue;
    private readonly Func<string, string?>? _commit;

    public InspectorTextPropertyViewModel(string displayName, string groupName, string value, bool isReadOnly = false, Func<string, string?>? commit = null)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value ?? string.Empty;
        _committedValue = _value;
        _commit = commit;
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public override void Commit()
    {
        if (IsReadOnly || string.Equals(_value, _committedValue, StringComparison.Ordinal))
        {
            return;
        }

        var error = _commit?.Invoke(_value);
        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorText = error;
            _value = _committedValue;
            RaisePropertyChanged(nameof(Value));
            return;
        }

        ErrorText = string.Empty;
        _committedValue = _value;
    }

    public void SetCommittedValue(string? value)
    {
        ErrorText = string.Empty;
        _value = value ?? string.Empty;
        _committedValue = _value;
        RaisePropertyChanged(nameof(Value));
    }

}

public sealed class InspectorDoublePropertyViewModel : InspectorEditablePropertyRowViewModel
{
    private string _value;
    private string _committedValue;
    private readonly Func<double, string?>? _commit;
    private readonly string _format;

    public InspectorDoublePropertyViewModel(string displayName, string groupName, double value, bool isReadOnly = false, Func<double, string?>? commit = null, string format = "0.###")
        : base(displayName, groupName, isReadOnly)
    {
        _format = format;
        _value = value.ToString(_format, CultureInfo.InvariantCulture);
        _committedValue = _value;
        _commit = commit;
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public override void Commit()
    {
        if (IsReadOnly || string.Equals(_value, _committedValue, StringComparison.Ordinal))
        {
            return;
        }

        if (!double.TryParse(_value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            || !PanelElementValidation.IsFinite(parsed))
        {
            ErrorText = "Enter a valid number.";
            _value = _committedValue;
            RaisePropertyChanged(nameof(Value));
            return;
        }

        var error = _commit?.Invoke(parsed);
        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorText = error;
            _value = _committedValue;
            RaisePropertyChanged(nameof(Value));
            return;
        }

        ErrorText = string.Empty;
        _committedValue = parsed.ToString(_format, CultureInfo.InvariantCulture);
        _value = _committedValue;
        RaisePropertyChanged(nameof(Value));
    }

    public void SetCommittedValue(double value)
    {
        ErrorText = string.Empty;
        _committedValue = value.ToString(_format, CultureInfo.InvariantCulture);
        _value = _committedValue;
        RaisePropertyChanged(nameof(Value));
    }
}

public sealed class InspectorIntPropertyViewModel : InspectorEditablePropertyRowViewModel
{
    private string _value;
    private string _committedValue;
    private readonly bool _allowEmpty;
    private readonly Func<int?, string?>? _commit;

    public InspectorIntPropertyViewModel(string displayName, string groupName, int? value, bool isReadOnly = false, bool allowEmpty = true, Func<int?, string?>? commit = null)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _committedValue = _value;
        _allowEmpty = allowEmpty;
        _commit = commit;
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public override void Commit()
    {
        if (IsReadOnly || string.Equals(_value, _committedValue, StringComparison.Ordinal))
        {
            return;
        }

        int? parsedValue;
        if (string.IsNullOrWhiteSpace(_value))
        {
            if (!_allowEmpty)
            {
                ErrorText = "A value is required.";
                _value = _committedValue;
                RaisePropertyChanged(nameof(Value));
                return;
            }

            parsedValue = null;
        }
        else if (!int.TryParse(_value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            ErrorText = "Enter a valid integer.";
            _value = _committedValue;
            RaisePropertyChanged(nameof(Value));
            return;
        }
        else
        {
            parsedValue = parsedInt;
        }

        var error = _commit?.Invoke(parsedValue);
        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorText = error;
            _value = _committedValue;
            RaisePropertyChanged(nameof(Value));
            return;
        }

        ErrorText = string.Empty;
        _committedValue = parsedValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _value = _committedValue;
        RaisePropertyChanged(nameof(Value));
    }

    public void SetCommittedValue(int? value)
    {
        ErrorText = string.Empty;
        _committedValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _value = _committedValue;
        RaisePropertyChanged(nameof(Value));
    }
}


public sealed class InspectorColorPropertyViewModel : InspectorEditablePropertyRowViewModel
{
    private Color _selectedColor;
    private Color _committedColor;
    private string _hexValue;
    private readonly Func<string?, string?>? _commit;
    private readonly bool _allowEmpty;

    public InspectorColorPropertyViewModel(string displayName, string groupName, string? value, bool isReadOnly = false, bool allowEmpty = true, Func<string?, string?>? commit = null)
        : base(displayName, groupName, isReadOnly)
    {
        _allowEmpty = allowEmpty;
        _commit = commit;

        if (InspectorColorHex.TryParse(value, out var parsedColor))
        {
            _selectedColor = parsedColor;
            _committedColor = parsedColor;
            _hexValue = InspectorColorHex.Format(parsedColor);
            _committedHexValue = _hexValue;
        }
        else
        {
            _selectedColor = Colors.White;
            _committedColor = _selectedColor;
            _hexValue = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            _committedHexValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (IsReadOnly || !SetProperty(ref _selectedColor, value))
            {
                return;
            }

            _hexValue = InspectorColorHex.Format(value);
            RaisePropertyChanged(nameof(HexValue));
            Commit();
        }
    }

    public string HexValue
    {
        get => _hexValue;
        set => SetProperty(ref _hexValue, value);
    }

    public override void Commit()
    {
        if (IsReadOnly)
        {
            return;
        }

        var normalizedHex = NormalizeHex(_hexValue, out var parsedColor, out var parseError);
        if (!string.IsNullOrWhiteSpace(parseError))
        {
            ErrorText = parseError;
            _hexValue = _committedHexValue ?? string.Empty;
            _selectedColor = _committedColor;
            RaisePropertyChanged(nameof(HexValue));
            RaisePropertyChanged(nameof(SelectedColor));
            return;
        }

        if (string.Equals(normalizedHex, _committedHexValue, StringComparison.Ordinal))
        {
            ErrorText = string.Empty;
            return;
        }

        var error = _commit?.Invoke(normalizedHex);
        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorText = error;
            _hexValue = _committedHexValue ?? string.Empty;
            _selectedColor = _committedColor;
            RaisePropertyChanged(nameof(HexValue));
            RaisePropertyChanged(nameof(SelectedColor));
            return;
        }

        ErrorText = string.Empty;
        _hexValue = normalizedHex ?? string.Empty;
        RaisePropertyChanged(nameof(HexValue));

        if (parsedColor.HasValue)
        {
            _selectedColor = parsedColor.Value;
            RaisePropertyChanged(nameof(SelectedColor));
            _committedColor = parsedColor.Value;
        }

        _committedHexValue = normalizedHex;
    }

    private string? _committedHexValue;

    private string? NormalizeHex(string? value, out Color? parsedColor, out string? error)
    {
        parsedColor = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (_allowEmpty)
            {
                return null;
            }

            error = "A color value is required.";
            return null;
        }

        if (!InspectorColorHex.TryParse(value, out var color))
        {
            error = "Enter a valid hex color (RRGGBB or AARRGGBB).";
            return null;
        }

        parsedColor = color;
        return InspectorColorHex.Format(color);
    }

    public void SetCommittedValue(string? value)
    {
        ErrorText = string.Empty;
        if (InspectorColorHex.TryParse(value, out var parsedColor))
        {
            _selectedColor = parsedColor;
            _committedColor = parsedColor;
            _hexValue = InspectorColorHex.Format(parsedColor);
            _committedHexValue = _hexValue;
        }
        else
        {
            _hexValue = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            _committedHexValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        RaisePropertyChanged(nameof(HexValue));
        RaisePropertyChanged(nameof(SelectedColor));
    }
}

public sealed class InspectorBoolPropertyViewModel : InspectorPropertyRowViewModel
{
    private bool _value;
    private readonly Func<bool, string?>? _commit;
    private bool _isApplyingChange;

    public InspectorBoolPropertyViewModel(string displayName, string groupName, bool value, bool isReadOnly = false, Func<bool, string?>? commit = null)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value;
        _commit = commit;
    }

    public bool Value
    {
        get => _value;
        set
        {
            if (IsReadOnly || _isApplyingChange || !SetProperty(ref _value, value))
            {
                return;
            }

            var error = _commit?.Invoke(value);
            if (string.IsNullOrWhiteSpace(error))
            {
                ErrorText = string.Empty;
                return;
            }

            ErrorText = error;
            _isApplyingChange = true;
            Value = !value;
            _isApplyingChange = false;
        }
    }

    public void SetCommittedValue(bool value)
    {
        ErrorText = string.Empty;
        _value = value;
        RaisePropertyChanged(nameof(Value));
    }
}

public sealed class InspectorInfoPropertyViewModel : InspectorPropertyRowViewModel
{
    private string _value;

    public InspectorInfoPropertyViewModel(string displayName, string groupName, string value)
        : base(displayName, groupName, true)
    {
        _value = value;
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public void SetCommittedValue(string value)
    {
        Value = value;
    }
}

public sealed class InspectorChoicePropertyViewModel : InspectorPropertyRowViewModel
{
    private string _value;
    private readonly Func<string, string?>? _commit;

    public InspectorChoicePropertyViewModel(string displayName, string groupName, IReadOnlyList<string> choices, string value, bool isReadOnly = false, Func<string, string?>? commit = null)
        : base(displayName, groupName, isReadOnly)
    {
        Choices = choices;
        _value = value;
        _commit = commit;
    }

    public IReadOnlyList<string> Choices { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (IsReadOnly || !SetProperty(ref _value, value))
            {
                return;
            }

            var error = _commit?.Invoke(value);
            if (string.IsNullOrWhiteSpace(error))
            {
                ErrorText = string.Empty;
                return;
            }

            ErrorText = error;
        }
    }

    public void SetCommittedValue(string value)
    {
        ErrorText = string.Empty;
        _value = value;
        RaisePropertyChanged(nameof(Value));
    }
}


public sealed class InspectorActionPropertyViewModel : InspectorPropertyRowViewModel
{
    public InspectorActionPropertyViewModel(string displayName, string groupName, System.Windows.Input.ICommand command)
        : base(displayName, groupName, false)
    {
        Command = command;
    }

    public System.Windows.Input.ICommand Command { get; }
}

public sealed class InspectorImagePreviewPropertyViewModel : InspectorPropertyRowViewModel
{
    public InspectorImagePreviewPropertyViewModel(string displayName, string groupName, BitmapSource image, string? caption = null)
        : base(displayName, groupName, true)
    {
        Image = image;
        Caption = caption ?? $"{image.PixelWidth:0} x {image.PixelHeight:0}";
    }

    public BitmapSource Image { get; }

    public string Caption { get; }
}
