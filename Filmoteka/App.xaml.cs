using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Filmoteka.Data;

namespace Filmoteka;

/// <summary>
/// Входна точка на приложението: настройва българска култура,
/// създава базата данни и отваря главния прозорец.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Български формат на числа и дати навсякъде в интерфейса.
        var culture = new CultureInfo("bg-BG");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        // Всяка неприхваната грешка се показва с разбираемо съобщение, вместо програмата да спре.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "Възникна неочаквана грешка:\n\n" + args.Exception.Message,
                "Филмотека – грешка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            Database.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Базата данни не можа да бъде създадена или отворена.\n\nПодробности: " + ex.Message,
                "Филмотека – грешка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }
}
