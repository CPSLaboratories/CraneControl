using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CraneControl.App.ViewModels;

namespace CraneControl.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Aplikacja startuje w trybie symulacji; rzeczywisty PLC podłącza się z poziomu UI (adres IP + "Połącz").
            var mainWindowViewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = mainWindowViewModel };
            desktop.Startup += async (_, _) => await mainWindowViewModel.StartAsync();
            desktop.Exit += async (_, _) => await mainWindowViewModel.DisposeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
