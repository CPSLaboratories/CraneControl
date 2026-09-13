using CommunityToolkit.Mvvm.ComponentModel;

namespace CraneControl.App.ViewModels;

/// <summary>
/// Stan wizualizacji bocznego widoku dźwignicy. Wszystkie wymiary rysunku (Field*) są
/// umowne i niezależne od rozmiaru okna - to one decydują o proporcjach widoku i można
/// je swobodnie zmieniać, gdy rzeczywiste wymiary konstrukcji ulegną zmianie.
/// </summary>
public partial class CraneVisualizationViewModel : ObservableObject
{
    // --- Proporcje wizualizacji (jednostki umowne) ---
    [ObservableProperty]
    private double fieldWidth = 380;

    [ObservableProperty]
    private double fieldHeight = 230;

    // --- Rzeczywisty zakres ruchu osi (do kalibracji po pomiarze konstrukcji), w mm ---
    [ObservableProperty]
    private double travelXmm = 3000;

    [ObservableProperty]
    private double travelYmm = 2000;

    // --- Bieżąca pozycja haka, w mm (z rejestrów PLC) ---
    [ObservableProperty]
    private double positionXmm;

    [ObservableProperty]
    private double positionYmm;

    [ObservableProperty]
    private bool hasTarget;

    [ObservableProperty]
    private double targetXmm;

    [ObservableProperty]
    private double targetYmm;

    [ObservableProperty]
    private bool isAutoMode;

    public double HookDiameter => Math.Min(FieldWidth, FieldHeight) * 0.09;

    public double RailY => FieldHeight * 0.12;

    public double TrolleyLeft => Normalize(PositionXmm, TravelXmm) * (FieldWidth - HookDiameter);

    public double TrolleyTop => RailY - (HookDiameter * 0.35);

    public double HookLeft => TrolleyLeft;

    public double HookTop => RailY + Normalize(PositionYmm, TravelYmm) * (FieldHeight - RailY - HookDiameter);

    public double RopeLeft => TrolleyLeft + (HookDiameter / 2) - 1;

    public double RopeLength => Math.Max(0, HookTop - RailY);

    public double TargetLeft => Normalize(TargetXmm, TravelXmm) * (FieldWidth - HookDiameter);

    public double TargetTop => RailY + Normalize(TargetYmm, TravelYmm) * (FieldHeight - RailY - HookDiameter);

    private static double Normalize(double value, double max) => max <= 0 ? 0 : Math.Clamp(value / max, 0, 1);

    partial void OnPositionXmmChanged(double value) => RaiseGeometryChanged();

    partial void OnPositionYmmChanged(double value) => RaiseGeometryChanged();

    partial void OnTargetXmmChanged(double value) => OnPropertyChanged(nameof(TargetLeft));

    partial void OnTargetYmmChanged(double value) => OnPropertyChanged(nameof(TargetTop));

    partial void OnFieldWidthChanged(double value) => RaiseGeometryChanged();

    partial void OnFieldHeightChanged(double value) => RaiseGeometryChanged();

    partial void OnTravelXmmChanged(double value) => RaiseGeometryChanged();

    partial void OnTravelYmmChanged(double value) => RaiseGeometryChanged();

    private void RaiseGeometryChanged()
    {
        OnPropertyChanged(nameof(HookDiameter));
        OnPropertyChanged(nameof(RailY));
        OnPropertyChanged(nameof(TrolleyLeft));
        OnPropertyChanged(nameof(TrolleyTop));
        OnPropertyChanged(nameof(HookLeft));
        OnPropertyChanged(nameof(HookTop));
        OnPropertyChanged(nameof(RopeLeft));
        OnPropertyChanged(nameof(RopeLength));
        OnPropertyChanged(nameof(TargetLeft));
        OnPropertyChanged(nameof(TargetTop));
    }

    /// <summary>Przelicza znormalizowany klik na wizualizacji (0..1) na milimetry.</summary>
    public (double Xmm, double Ymm) NormalizedClickToMillimeters(double normX, double normY)
    {
        return (Math.Clamp(normX, 0, 1) * TravelXmm, Math.Clamp(normY, 0, 1) * TravelYmm);
    }
}
