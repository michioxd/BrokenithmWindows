using BrokenithmWindows.Core.Protocol;
using BrokenithmWindows.Platform;
using BrokenithmWindows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace BrokenithmWindows.Views;

public sealed partial class ControllerSurface : UserControl
{
    private TouchInputProvider? _provider;
    private readonly Border[] _leds = new Border[32],
        _sensors = new Border[32],
        _bands = new Border[6];
    private LedColor[]? _lastLeds;
    private uint _lastSensors = uint.MaxValue;
    private int _lastAir = -1;

    public ControllerSurface()
    {
        InitializeComponent();
        for (int i = 0; i < 32; i++)
        {
            LedStrip.ColumnDefinitions.Add(
                new() { Width = new GridLength(i % 2 == 0 ? 1 : 7.428571, GridUnitType.Star) }
            );
            var led = new Border { Background = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)) };
            Grid.SetColumn(led, i);
            LedStrip.Children.Add(led);
            _leds[i] = led;
            SensorOverlay.ColumnDefinitions.Add(new());
            var sensor = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 24)),
                BorderThickness = new Thickness(0, 1, 1, 0),
            };
            if (i % 2 == 0)
                sensor.Child = new TextBlock
                {
                    Text = (i / 2 + 1).ToString(),
                    Margin = new Thickness(4, 10, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                };
            Grid.SetColumn(sensor, i);
            SensorOverlay.Children.Add(sensor);
            _sensors[i] = sensor;
        }
        for (int i = 0; i < 6; i++)
        {
            AirBands.RowDefinitions.Add(new());
            var band = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 24)),
                BorderThickness = new Thickness(0, 1, 0, 0),
            };
            Grid.SetRow(band, i);
            AirBands.Children.Add(band);
            _bands[i] = band;
        }
    }

    public void Attach(InputService input)
    {
        Detach();
        _provider = new(TouchArea, input);
    }

    public void Reset() => _provider?.Reset();

    public void Detach()
    {
        _provider?.Dispose();
        _provider = null;
    }

    public void Render(InputService input, LedColor[]? leds, bool airEnabled, int airHeight)
    {
        AirRow.Height = new GridLength(airEnabled ? 3 : 0, GridUnitType.Star);
        AirVisual.Visibility = airEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (leds != _lastLeds)
        {
            _lastLeds = leds;
            for (int i = 0; i < 32; i++)
            {
                var color = leds?[i] ?? new LedColor(0, 0, 0);
                ((SolidColorBrush)_leds[i].Background).Color = Color.FromArgb(
                    255,
                    color.Red,
                    color.Green,
                    color.Blue
                );
            }
        }
        uint sensors = input.Snapshot.Slider;
        if (sensors != _lastSensors)
        {
            _lastSensors = sensors;
            for (int i = 0; i < 32; i++)
                _sensors[i].Background =
                    (sensors & (1u << i)) != 0
                        ? new SolidColorBrush(Color.FromArgb(120, 255, 255, 255))
                        : null;
        }
        if (airHeight != _lastAir)
        {
            _lastAir = airHeight;
            for (int i = 0; i < 6; i++)
                _bands[i].Background =
                    i >= airHeight ? new SolidColorBrush(Color.FromArgb(100, 80, 160, 255)) : null;
        }
    }
}
