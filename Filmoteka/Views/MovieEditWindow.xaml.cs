using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Filmoteka.Data;
using Filmoteka.Helpers;
using Filmoteka.Models;
using Filmoteka.Services;

namespace Filmoteka.Views;

/// <summary>
/// Форма за добавяне и редактиране на филм, с търсене в TMDB и валидация преди запис.
/// </summary>
public partial class MovieEditWindow : Window
{
    private readonly Movie? _existing;
    private string? _posterPath;
    private int? _tmdbId;
    private int _applyVersion; // предпазва от объркване при бързо кликане на няколко резултата

    /// <summary>Id на записания филм (след успешно „Запази“).</summary>
    public int SavedMovieId { get; private set; }

    public MovieEditWindow(Movie? existing, IList<Genre> genres)
    {
        InitializeComponent();
        WindowStyler.ApplyDarkTitleBar(this);

        _existing = existing;
        GenreBox.ItemsSource = genres;
        RatingStars.RatingChanged += (_, _) => UpdateRatingLabel();

        if (existing != null)
        {
            Title = "Редактиране на филм";
            HeaderText.Text = "Редактиране на филм";
            TitleBox.Text = existing.Title;
            DirectorBox.Text = existing.Director;
            YearBox.Text = existing.Year.ToString();
            GenreBox.SelectedItem = genres.FirstOrDefault(g => g.Id == existing.GenreId);
            RatingStars.Value = existing.Rating;
            OverviewBox.Text = existing.Overview ?? "";
            TmdbQuery.Text = existing.Title;
            _posterPath = existing.PosterPath;
            _tmdbId = existing.TmdbId;
        }

        UpdateRatingLabel();

        if (!TmdbService.HasKey)
            TmdbStatus.Text = "За търсене в TMDB добавете API ключ от „Настройки“ в главния прозорец. Можете да попълните данните и ръчно.";

        Loaded += async (_, _) =>
        {
            (existing == null ? TmdbQuery : TitleBox).Focus();
            await ShowPosterAsync();
        };
    }

    // ==================================================================
    //  Постер
    // ==================================================================

    private async Task ShowPosterAsync()
    {
        var path = _posterPath;
        RemovePosterButton.IsEnabled = !string.IsNullOrWhiteSpace(path);

        if (string.IsNullOrWhiteSpace(path))
        {
            PosterBrush.ImageSource = null;
            PosterInfo.Text = "Няма избран постер. Потърсете филма в TMDB.";
            return;
        }

        PosterInfo.Text = "Зареждане на постера…";
        ImageSource? image = await PosterCache.GetAsync(path);
        if (path != _posterPath)
            return; // междувременно е избран друг филм

        PosterBrush.ImageSource = image;
        PosterInfo.Text = image != null
            ? "Постер от TMDB"
            : "Постерът не може да бъде изтеглен. Проверете интернет връзката.";
    }

    private async void RemovePoster_Click(object sender, RoutedEventArgs e)
    {
        _posterPath = null;
        await ShowPosterAsync();
    }

    // ==================================================================
    //  Търсене в TMDB
    // ==================================================================

    private async void TmdbSearch_Click(object sender, RoutedEventArgs e) => await SearchTmdbAsync();

    private async void TmdbQuery_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SearchTmdbAsync();
        }
    }

    private async Task SearchTmdbAsync()
    {
        var query = TmdbQuery.Text.Trim();
        if (query.Length == 0)
            query = TitleBox.Text.Trim();

        if (query.Length == 0)
        {
            TmdbStatus.Text = "Въведете заглавие, което да потърсим.";
            TmdbQuery.Focus();
            return;
        }

        TmdbSearchButton.IsEnabled = false;
        TmdbStatus.Text = "Търсене…";
        try
        {
            var results = await TmdbService.SearchAsync(query);
            TmdbResults.ItemsSource = results;
            TmdbStatus.Text = results.Count == 0
                ? $"Няма резултати за „{query}“. Опитайте с оригиналното (английско) заглавие."
                : $"Намерени: {results.Count}. Изберете филм от списъка.";
        }
        catch (TmdbException ex)
        {
            TmdbStatus.Text = ex.Message;
        }
        finally
        {
            TmdbSearchButton.IsEnabled = true;
        }
    }

    private async void TmdbResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TmdbResults.SelectedItem is not TmdbMovie result)
            return;

        int version = ++_applyVersion;

        // Данни, които идват директно от търсенето.
        TitleBox.Text = result.Title;
        if (result.Year is int year)
            YearBox.Text = year.ToString();
        if (!string.IsNullOrWhiteSpace(result.Overview))
            OverviewBox.Text = result.Overview;
        SelectGenre(TmdbService.MapGenre(result.GenreIds));
        _tmdbId = result.Id;
        _posterPath = result.PosterPath;
        TmdbStatus.Text = "Зареждане на режисьора…";

        _ = ShowPosterAsync();

        // Режисьорът е само в детайлите на филма.
        try
        {
            var details = await TmdbService.GetDetailsAsync(result.Id);
            if (version != _applyVersion)
                return;

            var director = TmdbService.GetDirector(details);
            if (!string.IsNullOrWhiteSpace(director))
                DirectorBox.Text = director;
            if (string.IsNullOrWhiteSpace(OverviewBox.Text) && !string.IsNullOrWhiteSpace(details.Overview))
                OverviewBox.Text = details.Overview;

            TmdbStatus.Text = $"Данните за „{result.Title}“ са попълнени. Проверете ги, поставете оценка и натиснете „Запази“.";
        }
        catch (TmdbException ex)
        {
            if (version == _applyVersion)
                TmdbStatus.Text = ex.Message;
        }
    }

    private void SelectGenre(string? genreName)
    {
        if (genreName == null || GenreBox.ItemsSource is not IEnumerable<Genre> genres)
            return;

        var match = genres.FirstOrDefault(g => string.Equals(g.Name, genreName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            GenreBox.SelectedItem = match;
    }

    // ==================================================================
    //  Оценка
    // ==================================================================

    private void ClearRating_Click(object sender, RoutedEventArgs e)
    {
        RatingStars.Value = 0;
        UpdateRatingLabel();
    }

    private void UpdateRatingLabel()
        => RatingLabel.Text = RatingStars.Value > 0 ? $"{RatingStars.Value}/10" : "—";

    // ==================================================================
    //  Валидация и запис
    // ==================================================================

    private void YearBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsDigit);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ClearErrors();
        var errors = new List<string>();

        string title = TitleBox.Text.Trim();
        string director = DirectorBox.Text.Trim();
        string overview = OverviewBox.Text.Trim();
        int maxYear = DateTime.Now.Year + 5;

        if (title.Length == 0)
        {
            errors.Add("Въведете заглавие на филма.");
            MarkError(TitleBox);
        }

        if (director.Length == 0)
        {
            errors.Add("Въведете режисьор.");
            MarkError(DirectorBox);
        }

        if (!int.TryParse(YearBox.Text.Trim(), out int year) || year < 1888 || year > maxYear)
        {
            errors.Add($"Годината трябва да е число между 1888 и {maxYear}.");
            MarkError(YearBox);
        }

        var genre = GenreBox.SelectedItem as Genre;
        if (genre == null)
        {
            errors.Add("Изберете жанр.");
            MarkError(GenreBox);
        }

        int rating = RatingStars.Value;
        if (rating < 0 || rating > 10)
            errors.Add("Оценката трябва да е между 1 и 10.");

        if (errors.Count > 0)
        {
            ErrorText.Text = "Моля, поправете следното:\n" + string.Join("\n", errors.Select(x => "•  " + x));
            ErrorBox.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            int excludeId = _existing?.Id ?? 0;
            if (Database.MovieExists(title, year, excludeId))
            {
                var answer = MessageBox.Show(this,
                    $"Във филмотеката вече има „{title}“ ({year}).\n\nДа го запиша ли въпреки това?",
                    "Възможен дубликат", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes)
                    return;
            }

            var movie = new Movie
            {
                Id = excludeId,
                Title = title,
                Director = director,
                Year = year,
                Rating = rating,
                GenreId = genre!.Id,
                PosterPath = string.IsNullOrWhiteSpace(_posterPath) ? null : _posterPath,
                TmdbId = _tmdbId,
                Overview = overview.Length == 0 ? null : overview
            };

            if (_existing == null)
            {
                SavedMovieId = Database.InsertMovie(movie);
            }
            else
            {
                Database.UpdateMovie(movie);
                SavedMovieId = movie.Id;
            }

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Филмът не можа да бъде записан в базата данни.\n\nПодробности: " + ex.Message,
                "Грешка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MarkError(Control control)
        => control.BorderBrush = (Brush)FindResource("DangerBrush");

    private void ClearErrors()
    {
        foreach (var control in new Control[] { TitleBox, DirectorBox, YearBox, GenreBox })
            control.ClearValue(BorderBrushProperty);

        ErrorBox.Visibility = Visibility.Collapsed;
    }
}
