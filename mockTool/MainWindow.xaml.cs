using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MockDiagTool.Services;
using MockDiagTool.ViewModels;
using MockDiagTool.Views;

namespace MockDiagTool;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private const string DebugLogFilePath = @"C:\Users\menghl2\OneDrive - kochind.com\Desktop\自动诊断项目\debug-0eaba0.log";

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

        // #region agent log
        AppendDebugLog(
            runId: "initial",
            hypothesisId: "H1",
            location: "MainWindow.xaml.cs:OnLoaded",
            message: "Window loaded with theme mode snapshot",
            data: new
            {
                ThemeMode = _viewModel?.ThemeModeText,
                IsScanning = _viewModel?.IsScanning
            });
        // #endregion

        // #region agent log
        AppendDebugLog(
            runId: "initial",
            hypothesisId: "H2",
            location: "MainWindow.xaml.cs:OnLoaded",
            message: "Theme secondary/background resource colors",
            data: new
            {
                SecondaryButtonBg = BrushToHex(TryFindResource("ThemeSecondaryButtonBg")),
                CardBg = BrushToHex(TryFindResource("ThemeCardBgBrush")),
                TextPrimary = BrushToHex(TryFindResource("ThemeTextPrimaryBrush"))
            });
        // #endregion

        // #region agent log
        AppendDebugLog(
            runId: "initial",
            hypothesisId: "H3",
            location: "MainWindow.xaml.cs:OnLoaded",
            message: "Action buttons visual states",
            data: new
            {
                Start = DumpButtonState(StartScanButton),
                SendToMims = DumpButtonState(SendToMimsButton),
                RunbookEditor = DumpButtonState(RunbookEditorButton)
            });
        // #endregion

        // #region agent log
        AppendDebugLog(
            runId: "initial",
            hypothesisId: "H4",
            location: "MainWindow.xaml.cs:OnLoaded",
            message: "Computed style key for visibility issue check",
            data: new
            {
                StartStyle = StartScanButton.Style?.ToString(),
                SendToMimsStyle = SendToMimsButton.Style?.ToString(),
                RunbookEditorStyle = RunbookEditorButton.Style?.ToString(),
                SendToMimsVisible = SendToMimsButton.Visibility.ToString(),
                RunbookEditorVisible = RunbookEditorButton.Visibility.ToString()
            });
        // #endregion
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
        if (e.PropertyName == nameof(MainViewModel.CurrentDiagnosticItem))
        {
            if (_viewModel?.CurrentDiagnosticItem is null)
            {
                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                DiagnosticListBox.ScrollIntoView(_viewModel.CurrentDiagnosticItem);
            });
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentScanItem))
        {
            Dispatcher.InvokeAsync(() =>
            {
                AnimateCurrentScanText();
                AnimateScoreValuePulse();
                AnimateScoreRingPulse();
            });
        }
    }

    private void AnimateCurrentScanText()
    {
        if (CurrentScanText.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            CurrentScanText.RenderTransform = translate;
        }

        var moveAnim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(240)
        };
        moveAnim.KeyFrames.Add(new EasingDoubleKeyFrame(6, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        moveAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        translate.BeginAnimation(TranslateTransform.YProperty, moveAnim);

        var opacityAnim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(240)
        };
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.35, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        CurrentScanText.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }

    private void AnimateScoreValuePulse()
    {
        if (ScoreValueText.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            ScoreValueText.RenderTransform = scale;
        }

        var scaleAnim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(260)
        };
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
    }

    private void AnimateScoreRingPulse()
    {
        var thicknessAnim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(260)
        };
        thicknessAnim.KeyFrames.Add(new EasingDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        thicknessAnim.KeyFrames.Add(new EasingDoubleKeyFrame(13, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        thicknessAnim.KeyFrames.Add(new EasingDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
        ScoreRing.BeginAnimation(Shape.StrokeThicknessProperty, thicknessAnim);

        var opacityAnim = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(260)
        };
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.7, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
        ScoreRing.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
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

    private static string BrushToHex(object? resource)
    {
        if (resource is SolidColorBrush solid)
        {
            var c = solid.Color;
            return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        return resource?.ToString() ?? "null";
    }

    private static object DumpButtonState(System.Windows.Controls.Button button)
    {
        return new
        {
            button.Name,
            button.Content,
            button.IsEnabled,
            Visibility = button.Visibility.ToString(),
            Opacity = button.Opacity,
            Foreground = BrushToHex(button.Foreground),
            Background = BrushToHex(button.Background)
        };
    }

    private static void AppendDebugLog(string runId, string hypothesisId, string location, string message, object data)
    {
        var payload = new
        {
            sessionId = "0eaba0",
            runId,
            hypothesisId,
            location,
            message,
            data,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        try
        {
            File.AppendAllText(DebugLogFilePath, System.Text.Json.JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // Keep app flow unaffected if debug logging fails.
        }
    }
}
