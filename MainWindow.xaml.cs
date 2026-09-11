using System;
using System.Runtime.InteropServices;
using BrokenithmWindows.Services;
using BrokenithmWindows.Utilities;
using BrokenithmWindows.ViewModels;
using BrokenithmWindows.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Storage;
using WinRT.Interop;

namespace BrokenithmWindows;

public sealed partial class MainWindow : Window
{
    private readonly MainPage _page;
    private bool _closing;
    private bool _readyToClose;

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr uIdSubclass,
        uint dwRefData
    );

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        IntPtr uIdSubclass,
        uint dwRefData
    );

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private SubclassProc? _subclassDelegate;
    private const int WM_GETMINMAXINFO = 0x0024;

    public MainWindow()
    {
        InitializeComponent();
        var logger = new AppLogger();
        var input = new InputService();
        var service = new BrokenithmService(input, logger);
        _page = new(
            new MainViewModel(
                new SettingsService(ApplicationData.Current.LocalFolder.Path),
                input,
                service,
                logger
            )
        );
        AppTitleBar.DataContext = _page.DataContext;
        _page.CanGoBackChanged += (s, canGoBack) =>
        {
            AppBackButton.IsEnabled = canGoBack;
            // Optionally, you can also change visibility: AppBackButton.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
        };
        MainContent.Content = _page;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Title = "Brokenithm";

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.Gray;
            titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(
                255,
                32,
                32,
                32
            );
            titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(
                255,
                16,
                16,
                16
            );
            titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
        }

        RightTitleBarStack.Margin = new Thickness(0, 0, AppWindow.TitleBar.RightInset, 0);
        MainContent.SizeChanged += (s, e) =>
        {
            RightTitleBarStack.Margin = new Thickness(0, 0, AppWindow.TitleBar.RightInset, 0);
        };

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
                _page.ReleaseInput();
        };
        AppWindow.Closing += Closing;

        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        _subclassDelegate = new SubclassProc(WindowSubClass);
        SetWindowSubclass(hWnd, _subclassDelegate, (IntPtr)1, 0);
    }

    private void AppBackButton_Click(object sender, RoutedEventArgs e)
    {
        _page.GoBack();
    }

    private void ControlsButton_Click(object sender, RoutedEventArgs e)
    {
        _page.TogglePanel();
    }

    private void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
            FullScreenIcon.Glyph = "\uE740";
        }
        else
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            FullScreenIcon.Glyph = "\uE73F";
        }
    }

    private IntPtr WindowSubClass(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr uIdSubclass,
        uint dwRefData
    )
    {
        if (uMsg == WM_GETMINMAXINFO)
        {
            double dpi = GetDpiForWindow(hWnd) / 96.0;
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
            mmi.ptMinTrackSize.x = (int)(800 * dpi);
            mmi.ptMinTrackSize.y = (int)(400 * dpi);
            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private async void Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_readyToClose)
            return;
        args.Cancel = true;
        if (_closing)
            return;
        _closing = true;
        try
        {
            await _page.ShutdownAsync();
        }
        finally
        {
            _readyToClose = true;
            Close();
        }
    }
}
