using System.ComponentModel;
using System.Windows;
using MockDiagTool.Services;
using MockDiagTool.ViewModels;
using MockDiagTool.Views;

namespace MockDiagTool;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as MainViewModel);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(e.NewValue as MainViewModel);
    }

    private void AttachViewModel(MainViewModel? vm)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.RunbookEditorRequested -= OnRunbookEditorRequested;
        }

        _viewModel = vm;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.RunbookEditorRequested += OnRunbookEditorRequested;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentDiagnosticItem))
        {
            return;
        }

        if (_viewModel?.CurrentDiagnosticItem is null)
        {
            return;
        }

        Dispatcher.InvokeAsync(() =>
        {
            DiagnosticListBox.ScrollIntoView(_viewModel.CurrentDiagnosticItem);
        });
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        AttachViewModel(null);
        Loaded -= OnLoaded;
        DataContextChanged -= OnDataContextChanged;
        Closed -= OnClosed;
    }

    private void OnRunbookEditorRequested(object? sender, EventArgs e)
    {
        var editorVm = new RunbookEditorViewModel(new RunbookFileService(), "default");
        var window = new RunbookEditorWindow(editorVm)
        {
            Owner = this
        };

        var saved = window.ShowDialog() == true;
        if (saved)
        {
            _viewModel?.ReloadRunbook();
        }
    }
}
