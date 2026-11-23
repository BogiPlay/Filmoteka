using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using Filmoteka.Data;
using Filmoteka.Helpers;
using Filmoteka.Services;

namespace Filmoteka.Views;

/// <summary>
/// Настройки: TMDB API ключ и информация за файла на базата данни.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        WindowStyler.ApplyDarkTitleBar(this);

        ApiKeyBox.Text = AppSettings.Current.TmdbApiKey;
        DbPathBox.Text = Database.DbPath;
        Loaded += (_, _) => ApiKeyBox.Focus();
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Text.Trim();
        if (key.Length == 0)
        {
            ShowResult("Въведете ключ, който да проверим.", ok: false);
            return;
        }

        TestButton.IsEnabled = false;
        ShowResult("Проверка…", ok: true);
        try
        {
            await TmdbService.TestKeyAsync(key);
            ShowResult("Ключът е валиден. Натиснете „Запази“.", ok: true);
        }
        catch (TmdbException ex)
        {
            ShowResult(ex.Message, ok: false);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AppSettings.Current.TmdbApiKey = ApiKeyBox.Text.Trim();
            AppSettings.Current.Save();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Настройките не можаха да бъдат запазени.\n\nПодробности: " + ex.Message,
                "Грешка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenDbFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{Database.DbPath}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Папката не можа да бъде отворена: " + ex.Message, "Грешка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // няма браузър по подразбиране – игнорираме
        }
        e.Handled = true;
    }

    private void ShowResult(string text, bool ok)
    {
        TestResult.Text = text;
        TestResult.Foreground = (Brush)FindResource(ok ? "SuccessBrush" : "DangerBrush");
    }
}
