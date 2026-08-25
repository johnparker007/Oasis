using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace OasisEditor;

internal sealed class BatchedObservableCollection<T> : ObservableCollection<T>
{
    private int _updateDepth;
    private bool _changedDuringUpdate;

    public IDisposable BeginUpdate()
    {
        _updateDepth++;
        return new UpdateScope(this);
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_updateDepth > 0)
        {
            _changedDuringUpdate = true;
            return;
        }

        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_updateDepth > 0)
        {
            _changedDuringUpdate = true;
            return;
        }

        base.OnPropertyChanged(e);
    }

    private void EndUpdate()
    {
        if (--_updateDepth != 0 || !_changedDuringUpdate)
        {
            return;
        }

        _changedDuringUpdate = false;
        base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        base.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private sealed class UpdateScope(BatchedObservableCollection<T> owner) : IDisposable
    {
        private BatchedObservableCollection<T>? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.EndUpdate();
        }
    }
}
