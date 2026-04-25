using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OasisEditor;

public sealed class HierarchyViewModel : INotifyPropertyChanged
{
    private readonly Func<DocumentTabViewModel?> _getSelectedDocument;
    private readonly IReadOnlyList<IDocumentHierarchyProvider> _providers;
    private string _emptyStateMessage = "No active document.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public HierarchyViewModel(
        Func<DocumentTabViewModel?> getSelectedDocument,
        IReadOnlyList<IDocumentHierarchyProvider> providers)
    {
        _getSelectedDocument = getSelectedDocument;
        _providers = providers;
    }

    public ObservableCollection<HierarchyItemViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        private set
        {
            if (string.Equals(_emptyStateMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _emptyStateMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyStateMessage)));
        }
    }

    public void Refresh()
    {
        var selectedDocument = _getSelectedDocument();
        var provider = _providers.FirstOrDefault(p => p.CanBuild(selectedDocument));

        Items.Clear();

        if (selectedDocument is null)
        {
            EmptyStateMessage = "No active document.";
            NotifyCollectionStateChanged();
            return;
        }

        if (provider is null)
        {
            EmptyStateMessage = $"Hierarchy is not available for {selectedDocument.TypeLabel}.";
            NotifyCollectionStateChanged();
            return;
        }

        foreach (var item in provider.Build(selectedDocument))
        {
            Items.Add(item);
        }

        EmptyStateMessage = Items.Count > 0 ? string.Empty : "This document has no hierarchy items yet.";
        NotifyCollectionStateChanged();
    }

    private void NotifyCollectionStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasItems)));
    }
}
