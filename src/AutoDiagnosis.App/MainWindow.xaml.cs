using System.Windows;
using AutoDiagnosis.App.ViewModels;

namespace AutoDiagnosis.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}