using System.Text.Json;
using System.Text.Json.Serialization;
using BrokenithmWindows.Core.Models;

namespace BrokenithmWindows.Services;

public sealed record AppSettings
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 52468;
    public bool Tcp { get; init; }
    public InputOptions Input { get; init; } = new();
    public bool ShowLatency { get; init; }
    public bool KeepScreenOn { get; init; } = true;
    public bool SendEmptyCard { get; init; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host) || Host.Any(char.IsWhiteSpace))
            throw new ArgumentException("Enter a host name or IP address without spaces.");
        if (Port is < 1 or > 65535)
            throw new ArgumentException("Port must be between 1 and 65535.");
        if (
            Input is null
            || !double.IsFinite(Input.FatThreshold)
            || !double.IsFinite(Input.ExtraFatThreshold)
            || Input.FatThreshold <= 0
            || Input.ExtraFatThreshold < Input.FatThreshold
            || Input.ExtraFatThreshold > 1
        )
            throw new ArgumentException(
                "Contact thresholds must be between 0 and 1, with the larger threshold last."
            );
    }
}

public sealed class SettingsService(string folder)
{
    private readonly string _path = Path.Combine(folder, "settings.json");

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_path))
            return new();
        await using var file = File.OpenRead(_path);
        var settings =
            await JsonSerializer.DeserializeAsync(file, SettingsJsonContext.Default.AppSettings)
            ?? throw new InvalidDataException("Settings are empty.");
        settings.Validate();
        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporary = _path + ".tmp";
        await using (var file = File.Create(temporary))
            await JsonSerializer.SerializeAsync(
                file,
                settings,
                SettingsJsonContext.Default.AppSettings
            );
        File.Move(temporary, _path, true);
    }
}

[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext { }
