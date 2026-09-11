using BrokenithmWindows.Core.Models;
using BrokenithmWindows.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace BrokenithmWindows.Platform;

/// <summary>Native pointer capture adapter. Coordinates and dimensions are both view-local DIPs.</summary>
public sealed class TouchInputProvider : IDisposable
{
    private readonly FrameworkElement _element;
    private readonly InputService _input;
    private readonly bool? _testButton;

    public TouchInputProvider(FrameworkElement element, InputService input, bool? testButton = null)
    {
        _element = element;
        _input = input;
        _testButton = testButton;
        element.ManipulationMode = ManipulationModes.None;
        element.PointerPressed += Pressed;
        element.PointerMoved += Moved;
        element.PointerReleased += Released;
        element.PointerCanceled += Released;
        element.PointerCaptureLost += Released;
        element.SizeChanged += SizeChanged;
    }

    private TouchPoint Point(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_element);
        return new(
            point.PointerId,
            point.Position.X / _element.ActualWidth,
            point.Position.Y / _element.ActualHeight,
            point.Properties.ContactRect.Width / _element.ActualWidth
        );
    }

    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsSupported(e) || _element.ActualWidth <= 0 || _element.ActualHeight <= 0)
            return;
        if (!_element.CapturePointer(e.Pointer))
            return;
        if (_testButton is bool test)
            _input.SetButton(e.Pointer.PointerId, test);
        else
            _input.Press(Point(e));
        e.Handled = true;
    }

    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (
            _testButton != null
            || !IsSupported(e)
            || _element.ActualWidth <= 0
            || _element.ActualHeight <= 0
        )
            return;
        _input.Move(Point(e));
        e.Handled = true;
    }

    private void Released(object sender, PointerRoutedEventArgs e)
    {
        _input.Release(e.Pointer.PointerId);
        _element.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void SizeChanged(object sender, SizeChangedEventArgs e) => Reset();

    private bool IsSupported(PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
            return true;
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            return false;
        return e.GetCurrentPoint(_element).Properties.IsLeftButtonPressed;
    }

    public void Reset()
    {
        _input.Clear();
        _element.ReleasePointerCaptures();
    }

    public void Dispose()
    {
        Reset();
        _element.PointerPressed -= Pressed;
        _element.PointerMoved -= Moved;
        _element.PointerReleased -= Released;
        _element.PointerCanceled -= Released;
        _element.PointerCaptureLost -= Released;
        _element.SizeChanged -= SizeChanged;
    }
}
