using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Filmoteka.Services;

/// <summary>
/// Изтегля постерите от TMDB веднъж и ги пази локално в
/// %LocalAppData%\Filmoteka\Posters, така че после се показват и без интернет.
/// За самите изображения не е нужен API ключ.
/// </summary>
public static class PosterCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly Dictionary<string, ImageSource> Memory = new();

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Filmoteka", "Posters");

    private static string FileFor(string posterPath)
        => Path.Combine(Folder, posterPath.Trim('/').Replace('/', '_'));

    /// <summary>Връща постера, ако вече е изтеглен; иначе null.</summary>
    public static ImageSource? TryGetCached(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
            return null;

        if (Memory.TryGetValue(posterPath, out var cached))
            return cached;

        var file = FileFor(posterPath);
        if (!File.Exists(file))
            return null;

        var image = LoadFromFile(file);
        if (image != null)
            Memory[posterPath] = image;
        return image;
    }

    /// <summary>Връща постера – от кеша или като го изтегли. При грешка връща null.</summary>
    public static async Task<ImageSource?> GetAsync(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
            return null;

        var cached = TryGetCached(posterPath);
        if (cached != null)
            return cached;

        try
        {
            Directory.CreateDirectory(Folder);
            var bytes = await Http.GetByteArrayAsync(TmdbService.ImageBase + "w342" + posterPath);
            await File.WriteAllBytesAsync(FileFor(posterPath), bytes);
            return TryGetCached(posterPath);
        }
        catch
        {
            return null; // няма интернет или постерът липсва – показваме заместител
        }
    }

    private static ImageSource? LoadFromFile(string file)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(file, UriKind.Absolute);
            bitmap.DecodePixelWidth = 342;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            try { File.Delete(file); } catch { /* игнорираме */ }
            return null;
        }
    }
}
