using Avalonia.Controls;

namespace CraneControl.App.Views;

public partial class AxisXView : UserControl
{
    public AxisXView()
    {
        InitializeComponent();
    }

    private void OnVisualizationClicked(object? sender, VisualizationClickedEventArgs e) =>
        (DataContext as ViewModels.AxisXViewModel)?.MoveTo(e.PositionCm);
}
