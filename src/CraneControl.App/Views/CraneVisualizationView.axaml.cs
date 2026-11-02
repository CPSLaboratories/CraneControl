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
        if (sender is not Control canvas)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        VisualizationClicked?.Invoke(this, new VisualizationClickedEventArgs(CraneVisualizationViewModel.CanvasXToCm(point.X)));
    }
}

public sealed class VisualizationClickedEventArgs(double positionCm) : EventArgs
{
    public double PositionCm { get; } = positionCm;
}
