using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Filmoteka.Services;

/// <summary>
/// Връзка с The Movie Database (TMDB) – търсене на филми, детайли и режисьор.
/// Работи както с „API Key“ (v3), така и с „API Read Access Token“ (v4).
/// </summary>
public static class TmdbService
{
    public const string ImageBase = "https://image.tmdb.org/t/p/";

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.themoviedb.org/3/"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Жанровете в TMDB имат постоянни номера – превеждаме ги към нашите имена.
    private static readonly Dictionary<int, string> GenreMap = new()
    {
        [28] = "Екшън",
        [12] = "Приключенски",
        [16] = "Анимация",
        [35] = "Комедия",
        [80] = "Криминален",
        [99] = "Документален",
        [18] = "Драма",
        [10751] = "Семеен",
        [14] = "Фентъзи",
        [36] = "Исторически",
        [27] = "Ужаси",
        [10402] = "Музикален",
        [9648] = "Мистерия",
        [10749] = "Романтичен",
        [878] = "Научна фантастика",
        [53] = "Трилър",
        [10752] = "Военен",
        [37] = "Уестърн",
    };

    public static bool HasKey => !string.IsNullOrWhiteSpace(AppSettings.Current.TmdbApiKey);

    public static async Task<List<TmdbMovie>> SearchAsync(string query, int? year = null)
    {
        var url = $"search/movie?query={Uri.EscapeDataString(query)}&language=bg-BG&include_adult=false&page=1";
        if (year is int y)
            url += $"&year={y}";

        var response = await GetAsync<TmdbSearchResponse>(url);
        return response.Results.Take(12).ToList();
    }

    public static Task<TmdbDetails> GetDetailsAsync(int tmdbId)
        => GetAsync<TmdbDetails>($"movie/{tmdbId}?language=bg-BG&append_to_response=credits");

    /// <summary>Проверява дали ключът е валиден.</summary>
    public static Task TestKeyAsync(string key)
        => GetAsync<JsonElement>("configuration", key);

    public static string? GetDirector(TmdbDetails details)
        => details.Credits?.Crew.FirstOrDefault(c => c.Job == "Director")?.Name;

    /// <summary>Връща името на първия жанр, който имаме в базата.</summary>
    public static string? MapGenre(IEnumerable<int> tmdbGenreIds)
    {
        foreach (var id in tmdbGenreIds)
        {
            if (GenreMap.TryGetValue(id, out var name))
                return name;
        }
        return null;
    }

    private static async Task<T> GetAsync<T>(string relativeUrl, string? keyOverride = null)
    {
        var key = (keyOverride ?? AppSettings.Current.TmdbApiKey).Trim();
        if (key.Length == 0)
            throw new TmdbException("Не е въведен TMDB API ключ. Отворете „Настройки“ и го добавете.");

        // v4 токенът е JWT и започва с "eyJ"; v3 ключът е 32 символа.
        bool isBearerToken = key.StartsWith("eyJ", StringComparison.Ordinal);
        if (!isBearerToken)
            relativeUrl += (relativeUrl.Contains('?') ? "&" : "?") + "api_key=" + Uri.EscapeDataString(key);

        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (isBearerToken)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request);
        }
        catch (TaskCanceledException)
        {
            throw new TmdbException("TMDB не отговаря (изтече времето за изчакване). Проверете интернет връзката.");
        }
        catch (HttpRequestException)
        {
            throw new TmdbException("Няма връзка с TMDB. Проверете интернет връзката.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new TmdbException("TMDB отказа достъп – API ключът е невалиден. Проверете го в „Настройки“.");

            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new TmdbException("Филмът не е намерен в TMDB.");

            if (!response.IsSuccessStatusCode)
                throw new TmdbException($"TMDB върна грешка {(int)response.StatusCode}. Опитайте отново по-късно.");

            var json = await response.Content.ReadAsStringAsync();
            T? result;
            try
            {
                result = JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException)
            {
                throw new TmdbException("Неочакван отговор от TMDB.");
            }

            if (result is null)
                throw new TmdbException("Неочакван отговор от TMDB.");
            return result;
        }
    }
}
