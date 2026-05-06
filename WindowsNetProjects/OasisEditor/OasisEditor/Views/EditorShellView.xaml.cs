using AvalonDock.Controls;
using AvalonDock.Layout;
using System.ComponentModel;
using System.Windows.Controls;

namespace OasisEditor.Views;

public partial class EditorShellView : UserControl
{
    public EditorShellView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        HideToolWindow(EditorToolWindowId.Preferences);
        HideToolWindow(EditorToolWindowId.ProjectSettings);
    }

    public void ShowOrFocusToolWindow(EditorToolWindowId toolWindowId)
    {
        var target = FindToolWindow(toolWindowId);
        if (target is null)
        {
            return;
        }

        EnsureToolWindowHasParent(target);

        if (target.IsHidden || !target.IsVisible)
        {
            target.Show();
        }

        if (toolWindowId is EditorToolWindowId.Preferences or EditorToolWindowId.ProjectSettings)
        {
            target.Float();
        }

        target.IsSelected = true;
        target.IsActive = true;
    }

    private void EnsureToolWindowHasParent(LayoutAnchorable target)
    {
        if (target.Parent is not null)
        {
            return;
        }

        target.AddToLayout(DockingManager, AnchorableShowStrategy.Right);
    }

    public void HideToolWindow(EditorToolWindowId toolWindowId)
    {
        var target = FindToolWindow(toolWindowId);
        target?.Hide();
    }

    private LayoutAnchorable? FindToolWindow(EditorToolWindowId toolWindowId)
    {
        var contentId = toolWindowId.ToString();
        var fromLayout = DockingManager.Layout.Descendents()
            .OfType<LayoutAnchorable>()
            .FirstOrDefault(anchorable => string.Equals(anchorable.ContentId, contentId, StringComparison.Ordinal));
        if (fromLayout is not null)
        {
            return fromLayout;
        }

        return DockingManager.Layout.Hidden
            .OfType<LayoutAnchorable>()
            .FirstOrDefault(anchorable => string.Equals(anchorable.ContentId, contentId, StringComparison.Ordinal));
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ConfigureHideOnClose(EditorToolWindowId.Preferences);
        ConfigureHideOnClose(EditorToolWindowId.ProjectSettings);

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        RebuildDocuments(viewModel);
        viewModel.OpenDocuments.CollectionChanged += (_, _) => RebuildDocuments(viewModel);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.SelectedDocument))
            {
                ActivateSelectedDocument(viewModel.SelectedDocument);
            }
        };
    }

    private void ConfigureHideOnClose(EditorToolWindowId toolWindowId)
    {
        var target = FindToolWindow(toolWindowId);
        if (target is null || target.Tag is string)
        {
            return;
        }

        target.Closing += OnToolWindowClosingHideInstead;
        target.Tag = "HideOnCloseConfigured";
    }

    private static void OnToolWindowClosingHideInstead(object? sender, CancelEventArgs e)
    {
        if (sender is not LayoutAnchorable target)
        {
            return;
        }

        e.Cancel = true;
        target.Hide();
    }

    private void RebuildDocuments(MainWindowViewModel viewModel)
    {
        DocumentPane.Children.Clear();

        foreach (var document in viewModel.OpenDocuments)
        {
            var layoutDocument = new LayoutDocument
            {
                Title = document.Title,
                ContentId = document.DocumentId.ToString(),
                CanClose = true,
                Content = new DocumentEditorView { DataContext = document }
            };

            document.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DocumentTabViewModel.Title))
                {
                    layoutDocument.Title = document.Title;
                }
            };

            layoutDocument.IsSelectedChanged += (_, _) =>
            {
                if (layoutDocument.IsSelected && viewModel.SelectedDocument != document)
                {
                    viewModel.SelectedDocument = document;
                }
            };

            layoutDocument.Closing += (_, _) =>
            {
                if (viewModel.SelectedDocument != document)
                {
                    viewModel.SelectedDocument = document;
                }

                viewModel.CloseSelectedDocumentCommand.Execute(null);
            };

            DocumentPane.Children.Add(layoutDocument);
        }

        ActivateSelectedDocument(viewModel.SelectedDocument);
    }

    private void ActivateSelectedDocument(DocumentTabViewModel? selectedDocument)
    {
        if (selectedDocument is null)
        {
            return;
        }

        var target = DocumentPane.Children
            .OfType<LayoutDocument>()
            .FirstOrDefault(doc => string.Equals(doc.ContentId, selectedDocument.DocumentId.ToString(), StringComparison.Ordinal));

        if (target is not null)
        {
            target.IsSelected = true;
            target.IsActive = true;
        }
    }
}
