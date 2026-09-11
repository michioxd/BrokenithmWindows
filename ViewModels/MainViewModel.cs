using BrokenithmWindows.Core.Models;
using BrokenithmWindows.Services;
using BrokenithmWindows.Utilities;

namespace BrokenithmWindows.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IAppLogger _logger;
    public InputService Input { get; }
    public BrokenithmService Service { get; }
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand DisconnectCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand CoinCommand { get; }
    public AsyncCommand CardCommand { get; }
    private string _host = "127.0.0.1",
        _error = "",
        _status = "Disconnected",
        _diagnostics = "",
        _inputStatus = "";
    private double _port = 52468,
        _fat = .027,
        _extraFat = .035;
    private int _mode;
    private bool _air = true,
        _simple,
        _size,
        _latency,
        _keepAwake = true,
        _card = true,
        _editable = true,
        _active,
        _busy;
    public string Host
    {
        get => _host;
        set => Set(ref _host, value);
    }
    public double Port
    {
        get => _port;
        set => Set(ref _port, value);
    }
    public int Mode
    {
        get => _mode;
        set => Set(ref _mode, value);
    }
    public bool EnableAir
    {
        get => _air;
        set
        {
            Set(ref _air, value);
            Service.SetAirEnabled(value);
            ConfigureInput();
        }
    }
    public bool SimpleAir
    {
        get => _simple;
        set
        {
            Set(ref _simple, value);
            ConfigureInput();
        }
    }
    public bool UseContactSize
    {
        get => _size;
        set
        {
            Set(ref _size, value);
            ConfigureInput();
        }
    }
    public double FatThreshold
    {
        get => _fat;
        set
        {
            Set(ref _fat, value);
            ConfigureInput();
        }
    }
    public double ExtraFatThreshold
    {
        get => _extraFat;
        set
        {
            Set(ref _extraFat, value);
            ConfigureInput();
        }
    }
    public bool ShowLatency
    {
        get => _latency;
        set => Set(ref _latency, value);
    }
    public bool KeepScreenOn
    {
        get => _keepAwake;
        set => Set(ref _keepAwake, value);
    }
    public bool SendEmptyCard
    {
        get => _card;
        set => Set(ref _card, value);
    }
    public string Error
    {
        get => _error;
        private set
        {
            Set(ref _error, value);
            Notify(nameof(HasError));
        }
    }
    public bool HasError => Error.Length > 0;
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }
    public string Diagnostics
    {
        get => _diagnostics;
        private set => Set(ref _diagnostics, value);
    }
    public string InputStatus
    {
        get => _inputStatus;
        private set => Set(ref _inputStatus, value);
    }
    public bool Editable
    {
        get => _editable;
        private set => Set(ref _editable, value);
    }
    public bool Active
    {
        get => _active;
        private set => Set(ref _active, value);
    }
    public bool Busy
    {
        get => _busy;
        private set => Set(ref _busy, value);
    }
    private ConnectionStatus? _lastStatus;
    private int _requestVersion;
    private InputOptions Options =>
        new(EnableAir, SimpleAir, UseContactSize, FatThreshold, ExtraFatThreshold);

    public string AppVersion
    {
        get
        {
            try
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"v{version.Major}.{version.Minor}.{version.Build}";
            }
            catch
            {
                return $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";
            }
        }
    }

    public MainViewModel(
        SettingsService settings,
        InputService input,
        BrokenithmService service,
        IAppLogger logger
    )
    {
        _settings = settings;
        Input = input;
        Service = service;
        _logger = logger;
        ConnectCommand = new(ConnectAsync, Report);
        DisconnectCommand = new(
            async () =>
            {
                _requestVersion++;
                await Service.DisconnectAsync();
                Refresh();
            },
            Report
        );
        SaveCommand = new(
            async () =>
            {
                await _settings.SaveAsync(Configuration());
                Error = "";
            },
            Report
        );
        CoinCommand = new(() => Service.SendFunctionAsync(false), Report);
        CardCommand = new(() => Service.SendFunctionAsync(true), Report);
    }

    private void ConfigureInput() => Input.Configure(Options);

    private async Task ConnectAsync()
    {
        int request = ++_requestVersion;
        Error = "";
        var config = Configuration();
        Editable = false;
        Busy = true;
        try
        {
            await _settings.SaveAsync(config);
            if (request == _requestVersion)
                await Service.ConnectAsync(config);
        }
        finally
        {
            _lastStatus = null;
            Refresh();
        }
    }

    public async Task ShutdownAsync()
    {
        _requestVersion++;
        await Service.DisposeAsync();
    }

    private AppSettings Configuration()
    {
        if (!double.IsFinite(Port) || Port != Math.Truncate(Port) || Port < 1 || Port > 65535)
            throw new ArgumentException("Enter a whole-number port from 1 to 65535.");
        var config = new AppSettings
        {
            Host = Host.Trim(),
            Port = (int)Port,
            Tcp = Mode == 1,
            Input = Options,
            ShowLatency = ShowLatency,
            KeepScreenOn = KeepScreenOn,
            SendEmptyCard = SendEmptyCard,
        };
        config.Validate();
        return config;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var config = await _settings.LoadAsync();
            Host = config.Host;
            Port = config.Port;
            Mode = config.Tcp ? 1 : 0;
            EnableAir = config.Input.EnableAir;
            SimpleAir = config.Input.SimpleAir;
            UseContactSize = config.Input.UseContactSize;
            FatThreshold = config.Input.FatThreshold;
            ExtraFatThreshold = config.Input.ExtraFatThreshold;
            ShowLatency = config.ShowLatency;
            KeepScreenOn = config.KeepScreenOn;
            SendEmptyCard = config.SendEmptyCard;
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    public void Refresh()
    {
        var status = Service.Status;
        if (status != _lastStatus)
        {
            _lastStatus = status;
            Status = status.Message;
            Active = status.Active;
            Busy = status.Busy;
            Editable = !status.Active && !status.Busy;
            if (status.Error != null)
                Error = status.Error;
        }
        var state = Input.Snapshot;
        InputStatus =
            $"{Input.ContactCount} touches - {System.Numerics.BitOperations.PopCount(state.Slider)} slider sensors - air {(Active ? Service.AirHeight : state.AirHeight)}/6";
        Diagnostics =
            $"Sent {Service.Sent:N0} - received {Service.Received:N0}"
            + (
                ShowLatency && Service.Delay >= 0
                    ? $" - estimated one-way delay {Service.Delay:F1} ms"
                    : ""
            );
    }

    public void Report(Exception ex)
    {
        _logger.Error("User operation", ex);
        Error = ex is ArgumentException or InvalidOperationException
            ? ex.Message
            : "The operation failed. Check your connection or settings. Details are in debug output.";
    }
}
