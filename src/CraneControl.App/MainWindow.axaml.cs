using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CraneControl.App.ViewModels;

namespace CraneControl.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Tunnel: strzałki sterują dźwignicą niezależnie od tego, który element ma fokus (poza polami tekstowymi).
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => ViewModel?.ReleaseAllKeys();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private static bool IsTextInput(object? source) =>
        source is Visual visual && visual.FindAncestorOfType<TextBox>(includeSelf: true) is not null;

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsTextInput(e.Source) && ViewModel?.OnKeyDown(e.Key) == true)
        {
            e.Handled = true;
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e) => ViewModel?.OnKeyUp(e.Key);

    // Kliknięcie poza polem tekstowym oddaje fokus oknu, żeby strzałki znów sterowały dźwignicą.
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsTextInput(e.Source))
        {
            Focus();
        }
    }
}
