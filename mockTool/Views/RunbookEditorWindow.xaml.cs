using System.Windows;
using MockDiagTool.ViewModels;

namespace MockDiagTool.Views;

public partial class RunbookEditorWindow : Window
{
    public RunbookEditorViewModel EditorViewModel { get; }

    public RunbookEditorWindow(RunbookEditorViewModel? viewModel = null)
    {
        InitializeComponent();
        EditorViewModel = viewModel ?? new RunbookEditorViewModel();
        DataContext = EditorViewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = EditorViewModel.SavedChanges;
        Close();
    }
}
