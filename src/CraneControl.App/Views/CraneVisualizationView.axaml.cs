using Avalonia.Controls;
using Avalonia.Input;
using CraneControl.App.ViewModels;

namespace CraneControl.App.Views;

public partial class CraneVisualizationView : UserControl
{
    public CraneVisualizationView()
    {
        InitializeComponent();
    }

    public event EventHandler<VisualizationClickedEventArgs>? VisualizationClicked;

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not CraneVisualizationViewModel vm || sender is not Control canvas)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        var normX = point.X / vm.FieldWidth;
        var normY = point.Y / vm.FieldHeight;
        VisualizationClicked?.Invoke(this, new VisualizationClickedEventArgs(normX, normY));
    }
}

public sealed class VisualizationClickedEventArgs(double normX, double normY) : EventArgs
{
    public double NormX { get; } = normX;

    public double NormY { get; } = normY;
}
