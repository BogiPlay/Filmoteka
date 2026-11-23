using System.Windows.Media;

namespace Filmoteka.Models;

/// <summary>
/// Ред от таблица Movies (плюс името на жанра от JOIN и заредения постер).
/// </summary>
public class Movie : ObservableObject
{
    private string _title = "";
    private string _director = "";
    private int _year;
    private int _rating;
    private string _genreName = "";
    private string? _overview;
    private ImageSource? _posterImage;

    public int Id { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
                OnPropertyChanged(nameof(Initials));
        }
    }

    public string Director
    {
        get => _director;
        set => Set(ref _director, value);
    }

    public int Year
    {
        get => _year;
        set
        {
            if (Set(ref _year, value))
                OnPropertyChanged(nameof(Subtitle));
        }
    }

    /// <summary>Лична оценка от 1 до 10. Стойност 0 означава „без оценка“.</summary>
    public int Rating
    {
        get => _rating;
        set
        {
            if (Set(ref _rating, value))
            {
                OnPropertyChanged(nameof(RatingText));
                OnPropertyChanged(nameof(IsRated));
            }
        }
    }

    public int GenreId { get; set; }

    public string GenreName
    {
        get => _genreName;
        set
        {
            if (Set(ref _genreName, value))
                OnPropertyChanged(nameof(Subtitle));
        }
    }

    /// <summary>Път към постера в TMDB, напр. "/abc123.jpg".</summary>
    public string? PosterPath { get; set; }

    public int? TmdbId { get; set; }

    public string? Overview
    {
        get => _overview;
        set
        {
            if (Set(ref _overview, value))
                OnPropertyChanged(nameof(HasOverview));
        }
    }

    public string CreatedAt { get; set; } = "";

    /// <summary>Зареденото изображение на постера (не се пази в базата).</summary>
    public ImageSource? PosterImage
    {
        get => _posterImage;
        set => Set(ref _posterImage, value);
    }

    // ---- Помощни свойства за показване ----

    public bool IsRated => Rating > 0;

    public string RatingText => Rating > 0 ? $"{Rating}/10" : "без оценка";

    public bool HasOverview => !string.IsNullOrWhiteSpace(Overview);

    public string Subtitle => $"{Year} · {GenreName}";

    public string Initials => string.IsNullOrWhiteSpace(Title) ? "?" : Title.Trim()[0].ToString().ToUpperInvariant();
}
