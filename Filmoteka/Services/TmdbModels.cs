using System.Text.Json.Serialization;

namespace Filmoteka.Services;

/// <summary>Грешка при комуникация с TMDB с разбираемо съобщение на български.</summary>
public class TmdbException(string message) : Exception(message);

public class TmdbSearchResponse
{
    [JsonPropertyName("results")]
    public List<TmdbMovie> Results { get; set; } = [];
}

/// <summary>Резултат от търсене в TMDB.</summary>
public class TmdbMovie
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("original_title")]
    public string OriginalTitle { get; set; } = "";

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }

    [JsonPropertyName("genre_ids")]
    public List<int> GenreIds { get; set; } = [];

    [JsonIgnore]
    public int? Year =>
        ReleaseDate is { Length: >= 4 } && int.TryParse(ReleaseDate[..4], out int y) ? y : null;

    [JsonIgnore]
    public string? ThumbUrl =>
        string.IsNullOrEmpty(PosterPath) ? null : TmdbService.ImageBase + "w92" + PosterPath;

    /// <summary>Ред под заглавието в списъка: година · оригинално заглавие · рейтинг в TMDB.</summary>
    [JsonIgnore]
    public string Info
    {
        get
        {
            var parts = new List<string>();
            if (Year is int y) parts.Add(y.ToString());
            if (!string.IsNullOrWhiteSpace(OriginalTitle) && OriginalTitle != Title) parts.Add(OriginalTitle);
            if (VoteAverage > 0) parts.Add($"TMDB ★ {VoteAverage:0.0}");
            return string.Join("  ·  ", parts);
        }
    }
}

/// <summary>Детайли за филм (с екипа, за да вземем режисьора).</summary>
public class TmdbDetails
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("genres")]
    public List<TmdbGenre> Genres { get; set; } = [];

    [JsonPropertyName("credits")]
    public TmdbCredits? Credits { get; set; }
}

public class TmdbGenre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class TmdbCredits
{
    [JsonPropertyName("crew")]
    public List<TmdbCrewMember> Crew { get; set; } = [];
}

public class TmdbCrewMember
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("job")]
    public string Job { get; set; } = "";
}
