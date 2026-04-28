using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;

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

}

public sealed class InspectorDoublePropertyViewModel : InspectorEditablePropertyRowViewModel
{
    private string _value;
    private string _committedValue;
    private readonly Func<double, string?>? _commit;

    public InspectorDoublePropertyViewModel(string displayName, string groupName, double value, bool isReadOnly = false, Func<double, string?>? commit = null)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value.ToString("0.###", CultureInfo.InvariantCulture);
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
        _committedValue = parsed.ToString("0.###", CultureInfo.InvariantCulture);
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
}
