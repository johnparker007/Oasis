using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor.Views;

public partial class PlayView : UserControl
{
    private DocumentTabViewModel? _subscribedDocument;
    private Window? _ownerWindow;

    public PlayView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshCanvasFromSelection();
        AttachOwnerWindowKeyHandlers();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachOwnerWindowKeyHandlers();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is MainWindowViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshCanvasFromSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SelectedDocument))
        {
            Dispatcher.Invoke(RefreshCanvasFromSelection);
        }
    }

    private void RefreshCanvasFromSelection()
    {
        var selected = ViewModel?.SelectedDocument;
        UpdateSelectedDocumentSubscription(selected);

        if (selected is null || selected.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            PlayCanvas.Children.Clear();
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        PanelLayoutMapper.ApplyPersistedLayout(PlayCanvas, selected.PanelLayoutJson, selected.RuntimeState);
        ApplyClickableCursorHints(selected);
    }

    private async void OnPlayCanvasPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyDownAsync(eventArgs);
    }

    private async void OnPlayCanvasPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyUpAsync(eventArgs);
    }

    private async void OnRootPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyDownAsync(eventArgs);
    }

    private async void OnRootPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyUpAsync(eventArgs);
    }

    private void OnPlayCanvasPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayCanvasFocused();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayCanvasFocused();
    }

    private async void OnPlayCanvasPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null || !TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        await ViewModel.TryHandlePlayViewPointerDownAsync(visualElementId, isFocused: true, CancellationToken.None);
    }

    private async void OnPlayCanvasPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null || !TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        await ViewModel.TryHandlePlayViewPointerUpAsync(visualElementId, isFocused: true, CancellationToken.None);
    }

    private async void OnPlayCanvasLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View focus loss", CancellationToken.None);
    }

    private async void OnPlayCanvasUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View close", CancellationToken.None);
    }

    private bool TryResolveVisualElementId(DependencyObject? source, out Guid visualElementId)
    {
        var selectable = CanvasSelectionBehavior.FindSelectableElement(source, PlayCanvas);
        if (selectable is not null && Guid.TryParse(selectable.Uid?.Trim(), out visualElementId))
        {
            return true;
        }

        visualElementId = Guid.Empty;
        return false;
    }

    private void UpdateSelectedDocumentSubscription(DocumentTabViewModel? selected)
    {
        if (ReferenceEquals(_subscribedDocument, selected))
        {
            return;
        }

        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelVisualStateChanged -= OnSelectedDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged -= OnSelectedDocumentPropertyChanged;
        }

        _subscribedDocument = selected;
        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelVisualStateChanged += OnSelectedDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged += OnSelectedDocumentPropertyChanged;
        }
    }

    private void OnSelectedDocumentPanelVisualStateChanged(PanelVisualStateChangedEvent visualStateChanged)
    {
        if (_subscribedDocument is null || visualStateChanged.DocumentId != _subscribedDocument.DocumentId)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            PanelLayoutMapper.ApplyVisualState(PlayCanvas, _subscribedDocument, visualStateChanged, _subscribedDocument.RuntimeState);
        });
    }

    private void OnSelectedDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.PanelLayoutJson))
        {
            Dispatcher.Invoke(RefreshCanvasFromSelection);
        }
    }

    private void ApplyClickableCursorHints(DocumentTabViewModel selectedDocument)
    {
        var clickableObjectIds = new HashSet<Guid>(
            (ViewModel?.InputDefinitions ?? [])
                .Where(input => input.LinkedVisualElementId.HasValue)
                .Select(input => input.LinkedVisualElementId!.Value));

        foreach (var visual in PlayCanvas.Children.OfType<FrameworkElement>())
        {
            var uid = visual.Uid?.Trim();
            var isClickable = !string.IsNullOrWhiteSpace(uid)
                && Guid.TryParse(uid, out var visualId)
                && clickableObjectIds.Contains(visualId);
            visual.Cursor = isClickable
                ? Cursors.Hand
                : Cursors.Arrow;
        }
    }

    private void EnsurePlayCanvasFocused()
    {
        if (!PlayCanvas.IsKeyboardFocusWithin)
        {
            Keyboard.Focus(PlayCanvas);
        }
    }

    private async Task TryRouteKeyDownAsync(KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled || ViewModel is null || !IsKeyboardFocusWithin)
        {
            return;
        }

        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        var handled = await ViewModel.TryHandlePlayViewKeyDownAsync(key.ToString(), isFocused: true, eventArgs.IsRepeat, CancellationToken.None);
        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private async Task TryRouteKeyUpAsync(KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled || ViewModel is null || !IsKeyboardFocusWithin)
        {
            return;
        }

        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        var handled = await ViewModel.TryHandlePlayViewKeyUpAsync(key.ToString(), isFocused: true, CancellationToken.None);
        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private void AttachOwnerWindowKeyHandlers()
    {
        if (_ownerWindow is not null)
        {
            return;
        }

        _ownerWindow = Window.GetWindow(this);
        if (_ownerWindow is null)
        {
            return;
        }

        _ownerWindow.PreviewKeyDown += OnOwnerWindowPreviewKeyDown;
        _ownerWindow.PreviewKeyUp += OnOwnerWindowPreviewKeyUp;
    }

    private void DetachOwnerWindowKeyHandlers()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        _ownerWindow.PreviewKeyDown -= OnOwnerWindowPreviewKeyDown;
        _ownerWindow.PreviewKeyUp -= OnOwnerWindowPreviewKeyUp;
        _ownerWindow = null;
    }

    private async void OnOwnerWindowPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyDownAsync(eventArgs);
    }

    private async void OnOwnerWindowPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyUpAsync(eventArgs);
    }
}
