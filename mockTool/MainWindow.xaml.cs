using MockDiagTool.Services;
using MockDiagTool.ViewModels;
using MockDiagTool.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

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
}