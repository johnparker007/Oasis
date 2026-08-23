using System.ComponentModel;
using Microsoft.Win32;
using System.Windows;

namespace OasisEditor;

public sealed class NewFaceDialogViewModel : INotifyPropertyChanged
{
    private bool _isImage;
    private string? _imagePath;
    private string? _errorMessage;
    public NewFaceDialogViewModel(string defaultName) => Name = defaultName;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; set; }
    public bool IsBlank { get => !_isImage; set { if (value) IsImage = false; } }
    public bool IsImage { get => _isImage; set { if (_isImage == value) return; _isImage = value; Raise(nameof(IsImage)); Raise(nameof(IsBlank)); } }
    public string? ImagePath { get => _imagePath; set { _imagePath = value; Raise(nameof(ImagePath)); } }
    public string? ErrorMessage { get => _errorMessage; set { _errorMessage = value; Raise(nameof(ErrorMessage)); } }
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class NewFaceDialog : Window
{
    public NewFaceDialogViewModel ViewModel { get; }
    public NewFaceDialog(string defaultName)
    {
        InitializeComponent();
        DataContext = ViewModel = new NewFaceDialogViewModel(defaultName);
    }
    private void OnChooseImage(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose Face artwork image", Filter = "Image files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) ViewModel.ImagePath = dialog.FileName;
    }
    private void OnCreate(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ViewModel.IsImage && string.IsNullOrWhiteSpace(ViewModel.ImagePath))
        {
            ViewModel.ErrorMessage = "Choose an image, or select Blank.";
            return;
        }
        DialogResult = true;
    }
}
