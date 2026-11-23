using System.Windows;
using System.Windows.Controls;
using Filmoteka.Data;
using Filmoteka.Helpers;
using Filmoteka.Models;

namespace Filmoteka.Views;

/// <summary>
/// CRUD за таблица Genres.
/// </summary>
public partial class GenresWindow : Window
{
    public GenresWindow()
    {
        InitializeComponent();
        WindowStyler.ApplyDarkTitleBar(this);
        LoadGenres();
    }

    private Genre? SelectedGenre => GenresGrid.SelectedItem as Genre;

    private void LoadGenres(int? selectId = null)
    {
        try
        {
            var genres = Database.GetGenres();
            GenresGrid.ItemsSource = genres;
            if (selectId != null)
            {
                var item = genres.FirstOrDefault(g => g.Id == selectId);
                GenresGrid.SelectedItem = item;
                if (item != null) GenresGrid.ScrollIntoView(item);
            }
        }
        catch (Exception ex)
        {
            ShowMessage("Жанровете не можаха да бъдат заредени: " + ex.Message, isError: true);
        }
    }

    private void GenresGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedGenre is Genre genre)
            NameBox.Text = genre.Name;
    }

    /// <summary>Проверява името. Връща текст на грешката или null, ако е наред.</summary>
    private static string? Validate(string name, int excludeId)
    {
        if (name.Length == 0)
            return "Името на жанра не може да е празно.";
        if (name.Length > 50)
            return "Името на жанра е твърде дълго (най-много 50 символа).";
        if (Database.GenreExists(name, excludeId))
            return $"Жанр „{name}“ вече съществува.";
        return null;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        try
        {
            var error = Validate(name, 0);
            if (error != null)
            {
                ShowMessage(error, isError: true);
                return;
            }

            int id = Database.AddGenre(name);
            LoadGenres(id);
            ShowMessage($"Жанрът „{name}“ е добавен.", isError: false);
        }
        catch (Exception ex)
        {
            ShowMessage("Жанрът не можа да бъде добавен: " + ex.Message, isError: true);
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGenre is not Genre genre)
        {
            ShowMessage("Изберете жанр от списъка, който да преименувате.", isError: true);
            return;
        }

        var name = NameBox.Text.Trim();
        try
        {
            var error = Validate(name, genre.Id);
            if (error != null)
            {
                ShowMessage(error, isError: true);
                return;
            }

            Database.RenameGenre(genre.Id, name);
            LoadGenres(genre.Id);
            ShowMessage($"Жанрът е преименуван на „{name}“.", isError: false);
        }
        catch (Exception ex)
        {
            ShowMessage("Жанрът не можа да бъде преименуван: " + ex.Message, isError: true);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGenre is not Genre genre)
        {
            ShowMessage("Изберете жанр от списъка, който да изтриете.", isError: true);
            return;
        }

        if (genre.MovieCount > 0)
        {
            ShowMessage($"Жанрът „{genre.Name}“ се използва от {genre.MovieCount} филм(а) и не може да бъде изтрит.", isError: true);
            return;
        }

        var answer = MessageBox.Show(this, $"Да изтрия ли жанра „{genre.Name}“?", "Изтриване на жанр",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            Database.DeleteGenre(genre.Id);
            NameBox.Text = "";
            LoadGenres();
            ShowMessage($"Жанрът „{genre.Name}“ е изтрит.", isError: false);
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, isError: true);
        }
    }

    private void ShowMessage(string text, bool isError)
    {
        MessageText.Text = text;
        MessageText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "DangerBrush" : "SuccessBrush");
    }
}
