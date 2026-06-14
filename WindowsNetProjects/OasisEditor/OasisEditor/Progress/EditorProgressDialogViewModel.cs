using System.ComponentModel;
using System.Windows.Input;

namespace OasisEditor.Progress;

public sealed class EditorProgressDialogViewModel : INotifyPropertyChanged
{
    private EditorProgressState _state;
    private readonly Action _requestCancel;
    private readonly RelayCommand _cancelCommand;

    public EditorProgressDialogViewModel(EditorProgressState initialState, Action requestCancel)
    {
        _state = initialState;
        _requestCancel = requestCancel ?? throw new ArgumentNullException(nameof(requestCancel));
        _cancelCommand = new RelayCommand(RequestCancel, () => CanCancel && !IsCancelling);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => _state.Title;
    public string Message => _state.Message;
    public bool IsIndeterminate => _state.Mode == EditorProgressMode.Indeterminate;
    public bool IsDeterminate => _state.Mode == EditorProgressMode.Determinate;
    public double ProgressPercent => (_state.Value ?? 0d) * 100d;
    public bool CanCancel => _state.CanCancel;
    public bool IsCancelling => _state.IsCancelling;
    public string CancelButtonText => IsCancelling ? "Cancelling..." : "Cancel";
    public string? ErrorMessage => _state.ErrorMessage;
    public bool HasError => !string.IsNullOrWhiteSpace(_state.ErrorMessage);
    public ICommand CancelCommand => _cancelCommand;

    public void UpdateState(EditorProgressState state)
    {
        _state = state;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(IsDeterminate));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(IsCancelling));
        OnPropertyChanged(nameof(CancelButtonText));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
        _cancelCommand.RaiseCanExecuteChanged();
    }

    private void RequestCancel()
    {
        if (!CanCancel || IsCancelling)
        {
            return;
        }

        UpdateState(_state.WithCancelling().WithMessage("Cancelling..."));
        _requestCancel();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
