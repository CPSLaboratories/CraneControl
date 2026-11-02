using Avalonia.Controls;
using Avalonia.Input;
using CraneControl.App.ViewModels;

namespace CraneControl.App.Views;

public partial class AxisYView : UserControl
{
    public AxisYView()
    {
        InitializeComponent();
    }

    private void OnHoldPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && DataContext is AxisYViewModel vm)
        {
            e.Pointer.Capture(control);
            SetHeld(vm, control, true);
        }
    }

    private void OnHoldReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control && DataContext is AxisYViewModel vm)
        {
            e.Pointer.Capture(null);
            SetHeld(vm, control, false);
        }
    }

    private void OnHoldCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (sender is Control control && DataContext is AxisYViewModel vm)
        {
            SetHeld(vm, control, false);
        }
    }

    private static void SetHeld(AxisYViewModel vm, Control control, bool held)
    {
        if (control.Tag is "up")
        {
            vm.SetUpHeld(held);
        }
        else
        {
            vm.SetDownHeld(held);
        }
    }
}
