using System.Text.Json;

namespace Filmoteka.Services;

/// <summary>
/// Настройки на приложението (TMDB API ключ). Пазят се в
/// %AppData%\Filmoteka\settings.json, за да не се губят при прекомпилиране.
/// </summary>
public class AppSettings
{
    public string TmdbApiKey { get; set; } = "";

    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Filmoteka");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    public static AppSettings Current { get; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // Повреден файл – започваме с празни настройки.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
