using System.Text.Json;

namespace LightCapture.App.Services;

internal sealed class UserSettings
{
    public bool ControlHotkey { get; set; }
    public bool SeenWelcome { get; set; }
}
internal sealed class SettingsStore
{
    internal string DataDirectory { get; }
    internal UserSettings Value { get; private set; } = new();
    internal SettingsStore(string? directory = null)
    {
        DataDirectory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightCapture");
        Directory.CreateDirectory(DataDirectory);
        var path = Path.Combine(DataDirectory, "settings.json");
        try { if (File.Exists(path)) Value = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? new(); }
        catch (JsonException) { }
    }
    internal void Save()
    {
        var path = Path.Combine(DataDirectory, "settings.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
