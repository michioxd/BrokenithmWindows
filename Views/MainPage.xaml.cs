using BrokenithmWindows.Platform;
using BrokenithmWindows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System.Display;

namespace BrokenithmWindows.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DisplayRequest _display = new();
    private TouchInputProvider? _test;
    private TouchInputProvider? _service;
    private bool _awake;
    private bool _loaded;
    private bool _panelOpen;
    private bool _isSettingsOpen;
    private Task? _shutdownTask;

    public event EventHandler<bool>? CanGoBackChanged;

    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _refresh.Tick += Refresh;
        HidePanelStoryboard.Completed += (_, _) =>
        {
            if (!_panelOpen)
            {
                _isSettingsOpen = false;
                Overlay.Visibility = Visibility.Collapsed;
                ControlsScrollViewer.Visibility = Visibility.Visible;
                ControlsTransform.X = 0;
                SettingsScrollViewer.Visibility = Visibility.Collapsed;
                SettingsTransform.X = -620;
                FloatingPanel.Height = double.NaN;
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
            return;
        _loaded = true;
        await ViewModel.InitializeAsync();
        if (!_loaded)
            return;
        Surface.Attach(ViewModel.Input);
        _test = new TouchInputProvider(TestPad, ViewModel.Input, true);
        _service = new TouchInputProvider(ServicePad, ViewModel.Input, false);
        _refresh.Start();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e) => await ShutdownAsync();

    private void Refresh(object? sender, object e)
    {
        ViewModel.Refresh();
        Surface.Render(
            ViewModel.Input,
            ViewModel.Service.Leds,
            ViewModel.EnableAir,
            ViewModel.Active ? ViewModel.Service.AirHeight : ViewModel.Input.Snapshot.AirHeight
        );
        SetAwake(ViewModel.Active && ViewModel.KeepScreenOn);
    }

    private void SetAwake(bool awake)
    {
        if (_awake == awake)
            return;
        try
        {
            if (awake)
                _display.RequestActive();
            else
                _display.RequestRelease();
            _awake = awake;
        }
        catch (Exception ex)
        {
            ViewModel.Report(ex);
        }
    }

    public void ReleaseInput()
    {
        Surface.Reset();
        _test?.Reset();
        _service?.Reset();
    }

    public void TogglePanel()
    {
        if (_panelOpen)
            ClosePanel();
        else
            OpenPanel();
    }

    private void OpenPanel()
    {
        if (_panelOpen)
            return;
        ReleaseInput();
        _panelOpen = true;
        CanGoBackChanged?.Invoke(this, true);
        Overlay.Visibility = Visibility.Visible;
        HidePanelStoryboard.Stop();
        ShowPanelStoryboard.Begin();
    }

    private void ClosePanel()
    {
        if (!_panelOpen)
            return;
        ReleaseInput();
        _panelOpen = false;
        CanGoBackChanged?.Invoke(this, false);
        ShowPanelStoryboard.Stop();
        HidePanelStoryboard.Begin();
    }

    public bool GoBack()
    {
        if (!_panelOpen)
            return false;

        if (_isSettingsOpen)
        {
            CloseSettingsClicked(this, new RoutedEventArgs());
        }
        else
        {
            ClosePanel();
        }
        return true;
    }

    private void BackdropTapped(object sender, TappedRoutedEventArgs e)
    {
        ClosePanel();
        e.Handled = true;
    }

    private void PanelTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void ClosePanelClicked(object sender, RoutedEventArgs e) => ClosePanel();

    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        if (_isSettingsOpen)
            return;
        _isSettingsOpen = true;

        SettingsScrollViewer.Visibility = Visibility.Visible;
        SettingsScrollViewer.Measure(new Windows.Foundation.Size(620, double.PositiveInfinity));
        var targetHeight = SettingsScrollViewer.DesiredSize.Height;

        var currentHeight = FloatingPanel.ActualHeight;
        FloatingPanel.Height = currentHeight;

        double maxAvailableHeight = Math.Max(0, this.ActualHeight - 40);
        var finalTargetHeight = Math.Min(targetHeight, maxAvailableHeight);

        var sb = new Storyboard();
        var slideControls = new DoubleAnimation
        {
            To = 620,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(slideControls, ControlsTransform);
        Storyboard.SetTargetProperty(slideControls, "X");
        sb.Children.Add(slideControls);

        var slideSettings = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(slideSettings, SettingsTransform);
        Storyboard.SetTargetProperty(slideSettings, "X");
        sb.Children.Add(slideSettings);

        var heightAnim = new DoubleAnimation
        {
            From = currentHeight,
            To = finalTargetHeight,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(heightAnim, FloatingPanel);
        Storyboard.SetTargetProperty(heightAnim, "Height");
        sb.Children.Add(heightAnim);

        sb.Completed += (_, _) =>
        {
            ControlsScrollViewer.Visibility = Visibility.Collapsed;
            FloatingPanel.Height = double.NaN;
        };
        sb.Begin();
    }

    private void CloseSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (!_isSettingsOpen)
            return;
        _isSettingsOpen = false;

        ControlsScrollViewer.Visibility = Visibility.Visible;
        ControlsScrollViewer.Measure(new Windows.Foundation.Size(620, double.PositiveInfinity));
        var targetHeight = ControlsScrollViewer.DesiredSize.Height;

        var currentHeight = FloatingPanel.ActualHeight;
        FloatingPanel.Height = currentHeight;

        double maxAvailableHeight = Math.Max(0, this.ActualHeight - 40);
        var finalTargetHeight = Math.Min(targetHeight, maxAvailableHeight);

        var sb = new Storyboard();
        var slideControls = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(slideControls, ControlsTransform);
        Storyboard.SetTargetProperty(slideControls, "X");
        sb.Children.Add(slideControls);

        var slideSettings = new DoubleAnimation
        {
            To = -620,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(slideSettings, SettingsTransform);
        Storyboard.SetTargetProperty(slideSettings, "X");
        sb.Children.Add(slideSettings);

        var heightAnim = new DoubleAnimation
        {
            From = currentHeight,
            To = finalTargetHeight,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(heightAnim, FloatingPanel);
        Storyboard.SetTargetProperty(heightAnim, "Height");
        sb.Children.Add(heightAnim);

        sb.Completed += (_, _) =>
        {
            SettingsScrollViewer.Visibility = Visibility.Collapsed;
            FloatingPanel.Height = double.NaN;
        };
        sb.Begin();
    }

    public Task ShutdownAsync() => _shutdownTask ??= ShutdownCoreAsync();

    private async Task ShutdownCoreAsync()
    {
        _loaded = false;
        _refresh.Stop();
        SetAwake(false);
        Surface.Detach();
        _test?.Dispose();
        _service?.Dispose();
        _test = _service = null;
        await ViewModel.ShutdownAsync();
    }
}
