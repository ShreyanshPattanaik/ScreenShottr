using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace ShottrClone;

public enum PostCaptureAction
{
    Preview,
    Editor,
    CopyOnly,
    SaveOnly
}

public sealed class AppSettings
{
    public bool AutoCopy { get; set; } = true;
    public bool AutoSave { get; set; }
    public string SaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ScreenShottr");
    public string FileNamePattern { get; set; } = "Screenshot_{timestamp}";
    public string ImageFormat { get; set; } = "png";
    public PostCaptureAction PostCaptureAction { get; set; } = PostCaptureAction.Preview;
    public bool StartWithWindows { get; set; }

    public AppSettings Clone() => new()
    {
        AutoCopy = AutoCopy,
        AutoSave = AutoSave,
        SaveFolder = SaveFolder,
        FileNamePattern = FileNamePattern,
        ImageFormat = ImageFormat,
        PostCaptureAction = PostCaptureAction,
        StartWithWindows = StartWithWindows
    };
}

internal sealed class SettingsService
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "ScreenShottr";
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsService()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenShottr");
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();
            var json = File.ReadAllText(_settingsPath);
            return Normalize(JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions));
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings = Normalize(settings);
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The settings folder is unavailable.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, _jsonOptions));
        ApplyStartupSetting(settings.StartWithWindows);
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();
        if (string.IsNullOrWhiteSpace(settings.SaveFolder))
            settings.SaveFolder = new AppSettings().SaveFolder;
        if (string.IsNullOrWhiteSpace(settings.FileNamePattern))
            settings.FileNamePattern = "Screenshot_{timestamp}";
        settings.ImageFormat = string.Equals(settings.ImageFormat, "jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(settings.ImageFormat, "jpeg", StringComparison.OrdinalIgnoreCase)
                ? "jpg"
                : "png";
        return settings;
    }

    private static void ApplyStartupSetting(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
        if (enabled)
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("The application path is unavailable.");
            key.SetValue(StartupValueName, $"\"{executable}\"");
        }
        else
        {
            key.DeleteValue(StartupValueName, false);
        }
    }
}
