using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Filmoteka.Controls;
using Filmoteka.Data;
using Filmoteka.Helpers;
using Filmoteka.Models;
using Filmoteka.Services;
using Filmoteka.Views;

namespace Filmoteka;

/// <summary>
/// Главен прозорец: списък с филми (таблица или карти), търсене, филтър по жанр,
/// детайли за избрания филм и бърза оценка със звезди.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<Movie> _movies = [];
    private readonly ICollectionView _view;
    private List<Genre> _genres = [];
    private bool _tmdbBusy;

    public MainWindow()
    {
        InitializeComponent();
        WindowStyler.ApplyDarkTitleBar(this);

        // Таблицата и картите ползват един и същ изглед, затова имат еднакъв филтър, подредба и избран филм.
        _view = CollectionViewSource.GetDefaultView(_movies);
        _view.Culture = new CultureInfo("bg-BG");
        _view.Filter = FilterMovie;
        _view.CurrentChanged += (_, _) => ShowDetails();

        MoviesGrid.ItemsSource = _movies;
        MoviesCards.ItemsSource = _movies;

        DetailRating.RatingChanged += DetailRating_RatingChanged;
        ApplySort();
    }

    private Movie? SelectedMovie => _view.CurrentItem as Movie;

    // ==================================================================
    //  Зареждане на данни
    // ==================================================================

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadGenres();
        LoadMovies();

        if (!TmdbService.HasKey)
        {
            SetStatus("Съвет: добавете безплатен TMDB API ключ от „Настройки“, за да се зареждат постерите на филмите.");
            return;
        }

        if (_movies.Any(m => string.IsNullOrWhiteSpace(m.PosterPath)))
            await FetchMissingPostersAsync(silent: true);
    }

    private void LoadGenres()
    {
        try
        {
            _genres = Database.GetGenres();
        }
        catch (Exception ex)
        {
            ShowError("Жанровете не можаха да бъдат заредени.", ex);
            _genres = [];
        }

        int selectedId = (GenreFilter.SelectedItem as Genre)?.Id ?? 0;
        var items = new List<Genre> { new() { Id = 0, Name = "Всички жанрове" } };
        items.AddRange(_genres);
        GenreFilter.ItemsSource = items;
        GenreFilter.SelectedItem = items.FirstOrDefault(g => g.Id == selectedId) ?? items[0];
    }

    private void LoadMovies(int? selectId = null)
    {
        selectId ??= SelectedMovie?.Id;

        List<Movie> list;
        try
        {
            list = Database.GetMovies();
        }
        catch (Exception ex)
        {
            ShowError("Филмите не можаха да бъдат заредени от базата данни.", ex);
            return;
        }

        _movies.Clear();
        foreach (var movie in list)
        {
            movie.PosterImage = PosterCache.TryGetCached(movie.PosterPath);
            _movies.Add(movie);
        }

        var toSelect = _movies.FirstOrDefault(m => m.Id == selectId);
        if (toSelect == null || !_view.MoveCurrentTo(toSelect))
            _view.MoveCurrentToFirst();

        UpdateStats();
        UpdateEmptyState();
        ShowDetails();
        _ = LoadMissingPosterImagesAsync();
    }

    /// <summary>Изтегля постерите, които са известни, но още не са в локалния кеш.</summary>
    private async Task LoadMissingPosterImagesAsync()
    {
        foreach (var movie in _movies.Where(m => m.PosterImage == null && !string.IsNullOrWhiteSpace(m.PosterPath)).ToList())
            movie.PosterImage = await PosterCache.GetAsync(movie.PosterPath);
    }

    // ==================================================================
    //  Търсене, филтър, подредба, изглед
    // ==================================================================

    private bool FilterMovie(object item)
    {
        if (item is not Movie movie)
            return false;

        var query = SearchBox.Text.Trim();
        if (query.Length > 0
            && !movie.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            && !movie.Director.Contains(query, StringComparison.OrdinalIgnoreCase))
            return false;

        if (GenreFilter.SelectedItem is Genre genre && genre.Id != 0 && movie.GenreId != genre.Id)
            return false;

        return true;
    }

    private void RefreshView()
    {
        if (_view == null) return; // събитията може да се извикат още по време на InitializeComponent

        _view.Refresh();
        if (_view.CurrentItem == null)
            _view.MoveCurrentToFirst();

        UpdateEmptyState();
        ShowDetails();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshView();

    private void GenreFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshView();

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySort();

    private void ApplySort()
    {
        if (_view == null || SortBox == null) return;

        using (_view.DeferRefresh())
        {
            _view.SortDescriptions.Clear();
            switch (SortBox.SelectedIndex)
            {
                case 1:
                    _view.SortDescriptions.Add(new SortDescription(nameof(Movie.Year), ListSortDirection.Descending));
                    _view.SortDescriptions.Add(new SortDescription(nameof(Movie.Title), ListSortDirection.Ascending));
                    break;
                case 2:
                    _view.SortDescriptions.Add(new SortDescription(nameof(Movie.Rating), ListSortDirection.Descending));
                    _view.SortDescriptions.Add(new SortDescription(nameof(Movie.Title), ListSortDirection.Ascending));
                    break;
                case 3:
                    _view.SortDescriptions.Add(new SortDescription(nameof(Movie.Id), ListSortDirection.Descending));
                    break;
                default:
                    _view.SortDescriptions.Add(new SortDescription(nameof(Movie.Title), ListSortDirection.Ascending));
                    break;
            }
        }

        // Махаме стрелката за подредба от колоните на таблицата.
        if (MoviesGrid != null)
        {
            foreach (var column in MoviesGrid.Columns)
                column.SortDirection = null;
        }
    }

    private void ViewMode_Checked(object sender, RoutedEventArgs e)
    {
        if (MoviesGrid == null || MoviesCards == null || CardsViewButton == null) return;

        bool cards = CardsViewButton.IsChecked == true;
        MoviesGrid.Visibility = cards ? Visibility.Collapsed : Visibility.Visible;
        MoviesCards.Visibility = cards ? Visibility.Visible : Visibility.Collapsed;
    }

    // ==================================================================
    //  Показване
    // ==================================================================

    private void ShowDetails()
    {
        var movie = SelectedMovie;
        DetailPanel.DataContext = movie;
        DetailPanel.Visibility = movie == null ? Visibility.Collapsed : Visibility.Visible;
        NoSelectionText.Visibility = movie == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateEmptyState()
        => EmptyState.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateStats()
    {
        int count = _movies.Count;
        string films = count == 1 ? "1 филм" : $"{count} филма";
        var rated = _movies.Where(m => m.Rating > 0).ToList();

        StatsText.Text = rated.Count > 0
            ? $"{films} в колекцията  ·  средна оценка {rated.Average(m => m.Rating):0.0} от {rated.Count} оценени"
            : $"{films} в колекцията";
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void ShowError(string message, Exception ex)
        => MessageBox.Show(this, $"{message}\n\nПодробности: {ex.Message}", "Грешка", MessageBoxButton.OK, MessageBoxImage.Error);

    // ==================================================================
    //  CRUD: добавяне, редактиране, изтриване
    // ==================================================================

    private void Add_Click(object sender, RoutedEventArgs e) => AddMovie();

    private void Edit_Click(object sender, RoutedEventArgs e) => EditMovie();

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteMovie();

    private void AddMovie()
    {
        if (_genres.Count == 0)
        {
            MessageBox.Show(this, "Първо добавете поне един жанр от бутона „Жанрове“.", "Няма жанрове",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new MovieEditWindow(null, _genres) { Owner = this };
        if (window.ShowDialog() == true)
        {
            LoadMovies(window.SavedMovieId);
            SetStatus("Филмът е добавен успешно.");
        }
    }

    private void EditMovie()
    {
        if (SelectedMovie is not Movie movie)
        {
            SetStatus("Изберете филм, който да редактирате.");
            return;
        }

        var window = new MovieEditWindow(movie, _genres) { Owner = this };
        if (window.ShowDialog() == true)
        {
            LoadMovies(movie.Id);
            SetStatus($"Промените по „{movie.Title}“ са запазени.");
        }
    }

    private void DeleteMovie()
    {
        if (SelectedMovie is not Movie movie)
        {
            SetStatus("Изберете филм, който да изтриете.");
            return;
        }

        var answer = MessageBox.Show(this,
            $"Сигурни ли сте, че искате да изтриете „{movie.Title}“ ({movie.Year})?\n\nТова действие не може да бъде отменено.",
            "Изтриване на филм", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            Database.DeleteMovie(movie.Id);
            _movies.Remove(movie);
            UpdateStats();
            UpdateEmptyState();
            ShowDetails();
            SetStatus($"„{movie.Title}“ е изтрит.");
        }
        catch (Exception ex)
        {
            ShowError("Филмът не можа да бъде изтрит.", ex);
        }
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Реагираме само при двоен клик върху ред/карта (не върху заглавие на колона или скролбар).
        if (sender is ItemsControl list && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(list, source) != null)
        {
            EditMovie();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (ctrl && e.Key == Key.N)
        {
            AddMovie();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            EditMovie();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
        {
            DeleteMovie();
            e.Handled = true;
        }
    }

    // ==================================================================
    //  Оценяване
    // ==================================================================

    private void DetailRating_RatingChanged(object? sender, int value)
    {
        if (SelectedMovie is not Movie movie)
            return;

        try
        {
            Database.UpdateRating(movie.Id, value);
            movie.Rating = value;
            UpdateStats();
            SetStatus(value > 0
                ? $"„{movie.Title}“ е оценен с {value}/10."
                : $"Оценката на „{movie.Title}“ е изчистена.");
        }
        catch (Exception ex)
        {
            ShowError("Оценката не можа да бъде записана.", ex);
            DetailRating.SetCurrentValue(StarRating.ValueProperty, movie.Rating);
        }
    }

    // ==================================================================
    //  TMDB
    // ==================================================================

    private async void FetchPosters_Click(object sender, RoutedEventArgs e)
        => await FetchMissingPostersAsync(silent: false);

    private async void RefreshFromTmdb_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMovie is not Movie movie || _tmdbBusy)
            return;

        if (!TmdbService.HasKey)
        {
            AskForApiKey();
            return;
        }

        _tmdbBusy = true;
        SetStatus($"Търсене на „{movie.Title}“ в TMDB…");
        try
        {
            bool found = await UpdateFromTmdbAsync(movie, forcePoster: true);
            SetStatus(found
                ? $"Постерът и описанието на „{movie.Title}“ са обновени."
                : $"Не беше намерен постер за „{movie.Title}“. Опитайте „Редактирай“ → търсене в TMDB.");
        }
        catch (TmdbException ex)
        {
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "TMDB", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ShowError("Грешка при обновяване от TMDB.", ex);
        }
        finally
        {
            _tmdbBusy = false;
        }
    }

    private async Task FetchMissingPostersAsync(bool silent)
    {
        if (_tmdbBusy)
            return;

        if (!TmdbService.HasKey)
        {
            if (!silent) AskForApiKey();
            return;
        }

        var missing = _movies.Where(m => string.IsNullOrWhiteSpace(m.PosterPath)).ToList();
        if (missing.Count == 0)
        {
            if (!silent) SetStatus("Всички филми вече имат постери.");
            return;
        }

        _tmdbBusy = true;
        FetchPostersButton.IsEnabled = false;
        int found = 0;
        try
        {
            for (int i = 0; i < missing.Count; i++)
            {
                SetStatus($"Изтегляне на постери от TMDB… {i + 1}/{missing.Count} – {missing[i].Title}");
                if (await UpdateFromTmdbAsync(missing[i], forcePoster: false))
                    found++;
            }
            SetStatus($"Готово: намерени са постери за {found} от {missing.Count} филма.");
        }
        catch (TmdbException ex)
        {
            SetStatus(ex.Message);
            if (!silent)
                MessageBox.Show(this, ex.Message, "TMDB", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ShowError("Грешка при изтегляне на постерите.", ex);
        }
        finally
        {
            _tmdbBusy = false;
            FetchPostersButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Намира филма в TMDB (по TmdbId или по заглавие и година) и записва постера и описанието.
    /// </summary>
    private static async Task<bool> UpdateFromTmdbAsync(Movie movie, bool forcePoster)
    {
        TmdbDetails? details = null;

        if (movie.TmdbId is int tmdbId)
        {
            details = await TmdbService.GetDetailsAsync(tmdbId);
        }
        else
        {
            var results = await TmdbService.SearchAsync(movie.Title, movie.Year);
            if (results.Count == 0)
                results = await TmdbService.SearchAsync(movie.Title);
            if (results.Count > 0)
                details = await TmdbService.GetDetailsAsync(results[0].Id);
        }

        if (details == null)
            return false;

        movie.TmdbId = details.Id;
        if (!string.IsNullOrWhiteSpace(details.PosterPath) && (forcePoster || string.IsNullOrWhiteSpace(movie.PosterPath)))
            movie.PosterPath = details.PosterPath;
        if (!string.IsNullOrWhiteSpace(details.Overview) && (forcePoster || string.IsNullOrWhiteSpace(movie.Overview)))
            movie.Overview = details.Overview;

        Database.UpdateTmdbInfo(movie.Id, movie.PosterPath, movie.TmdbId, movie.Overview);

        movie.PosterImage = await PosterCache.GetAsync(movie.PosterPath);
        return movie.PosterImage != null;
    }

    private void AskForApiKey()
    {
        var answer = MessageBox.Show(this,
            "За да се зареждат постери от TMDB, е нужен безплатен API ключ.\n\nИскате ли да отворите настройките сега?",
            "Липсва TMDB ключ", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes)
            OpenSettings();
    }

    // ==================================================================
    //  Други прозорци
    // ==================================================================

    private void Genres_Click(object sender, RoutedEventArgs e)
    {
        new GenresWindow { Owner = this }.ShowDialog();
        LoadGenres();
        LoadMovies();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private async void OpenSettings()
    {
        var window = new SettingsWindow { Owner = this };
        if (window.ShowDialog() == true && TmdbService.HasKey)
        {
            SetStatus("Настройките са запазени.");
            await FetchMissingPostersAsync(silent: false);
        }
    }
}
