using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OasisEditor;

/// <summary>A pending declaration bound to the Machine that opened the Add Input flow.</summary>
public sealed class InputCreationViewModel : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _target;
    private readonly Func<DocumentTabViewModel?> _activeMachine;
    private readonly Action _close;
    private bool _closed;
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string _error = string.Empty;

    internal InputCreationViewModel(DocumentTabViewModel target, Func<DocumentTabViewModel?> activeMachine, Action close)
    {
        if (target.Document.DocumentType != EditorDocumentType.Machine)
            throw new ArgumentException("Input declarations require a Machine document.", nameof(target));
        _target = target;
        _activeMachine = activeMachine;
        _close = close;
        CreateCommand = new RelayCommand(() => TryCreate());
        CancelCommand = new RelayCommand(Cancel);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string MachineName => _target.GetMachineDocument().DisplayName;
    public string Id { get => _id; set { _id = value; Notify(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; Notify(); } }
    public string Error { get => _error; private set { _error = value; Notify(); } }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    internal bool TryCreate()
    {
        if (_closed) return false;
        if (!ReferenceEquals(_activeMachine(), _target))
        {
            Error = "The active Machine changed. Cancel and open Add Input for the intended Machine.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Id))
        {
            Error = "Enter a logical input ID.";
            return false;
        }
        // The same ASCII identity contract as Player RuntimeIdentity and Machine anchors/objects.
        if (!MachineCompositionId.IsValid(Id))
        {
            Error = "Use only ASCII letters, digits, underscore or hyphen, without the input: prefix.";
            return false;
        }
        var machine = _target.GetMachineDocument();
        if (machine.InputDefinitions.Any(existing => string.Equals(existing.Id, Id, StringComparison.Ordinal)))
        {
            Error = $"Input ID '{Id}' already exists in this Machine (IDs are case-sensitive).";
            return false;
        }
        var input = new InputDefinitionModel
        {
            Id = Id,
            Name = string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName.Trim(),
            Kind = InputDefinitionKind.Button
        };
        _target.ExecuteMachineMutation(current => current with
        {
            InputDefinitions = [.. current.InputDefinitions, input]
        }, "Add Machine input definition");
        Cancel();
        return true;
    }

    internal void Cancel()
    {
        if (_closed) return;
        _closed = true;
        _close();
    }

    private void Notify([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
