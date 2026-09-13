using Avalonia.Controls;
using Avalonia.Input;
using CraneControl.App.ViewModels;
using CraneControl.App.Views;

namespace CraneControl.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnWindowKeyDown(object? sender, KeyEventArgs e) => ViewModel?.OnArrowKeyDown(e.Key);

    private void OnWindowKeyUp(object? sender, KeyEventArgs e) => ViewModel?.OnArrowKeyUp(e.Key);

    private void OnVisualizationClicked(object? sender, VisualizationClickedEventArgs e) =>
        ViewModel?.OnVisualizationClicked(e.NormX, e.NormY);
}