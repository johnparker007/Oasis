using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class InspectorTextPropertyViewModel : InspectorPropertyRowViewModel
{
    private string _value;

    public InspectorTextPropertyViewModel(string displayName, string groupName, string value, bool isReadOnly = false)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value;
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

public sealed class InspectorDoublePropertyViewModel : InspectorPropertyRowViewModel
{
    private double _value;

    public InspectorDoublePropertyViewModel(string displayName, string groupName, double value, bool isReadOnly = false)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value;
    }

    public double Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

public sealed class InspectorIntPropertyViewModel : InspectorPropertyRowViewModel
{
    private int? _value;

    public InspectorIntPropertyViewModel(string displayName, string groupName, int? value, bool isReadOnly = false)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value;
    }

    public int? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

public sealed class InspectorBoolPropertyViewModel : InspectorPropertyRowViewModel
{
    private bool _value;

    public InspectorBoolPropertyViewModel(string displayName, string groupName, bool value, bool isReadOnly = false)
        : base(displayName, groupName, isReadOnly)
    {
        _value = value;
    }

    public bool Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
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
