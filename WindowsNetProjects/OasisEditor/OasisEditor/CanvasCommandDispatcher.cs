using System.Windows;
using System.Windows.Controls;
using OasisEditor.Commands;

namespace OasisEditor;

internal static class CanvasCommandDispatcher
{
    public static bool ExecuteMutation(FrameworkElement canvas, DocumentTabViewModel tab, ICommand command)
    {
        if (TryGetShellViewModel(canvas, out var shellViewModel))
        {
            return shellViewModel.ExecuteDocumentCanvasCommand(tab.DocumentId, command);
        }

        tab.CommandService.Execute(command);
        return true;
    }

    public static void NotifyDocumentSelection(FrameworkElement canvas, DocumentTabViewModel tab, PanelSelectionInfo? selection)
    {
        if (!TryGetShellViewModel(canvas, out var shellViewModel))
        {
            return;
        }

        shellViewModel.UpdateDocumentPanelSelection(tab.DocumentId, selection);
    }

    private static bool TryGetShellViewModel(FrameworkElement canvas, out MainWindowViewModel shellViewModel)
    {
        shellViewModel = null!;
        if (Window.GetWindow(canvas)?.DataContext is not MainWindowViewModel viewModel)
        {
            return false;
        }

        shellViewModel = viewModel;
        return true;
    }
}
