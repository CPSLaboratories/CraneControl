using CommunityToolkit.Mvvm.ComponentModel;
using CraneControl.App.Models;

namespace CraneControl.App.ViewModels;

/// <summary>
/// Stan wizualizacji osi X: pozioma skala 0-80 cm z aktualną pozycją wózka i znacznikiem celu.
/// Wymiary rysunku (jednostki umowne) nie zależą od rozmiaru okna.
/// </summary>
public partial class CraneVisualizationViewModel : ObservableObject
{
    public const double FieldWidth = 800;
    public const double FieldHeight = 150;
    public const double TrackLeft = 40;
    public const double TrackWidth = 720;
    public const double RailY = 80;
    public const double TrolleyWidth = 46;
    public const double TrolleyHeight = 26;
    public const double PositionLabelWidth = 100;
    private const double TargetMarkerWidth = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrolleyLeft), nameof(PositionLabelLeft), nameof(PositionText))]
    private double positionCm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetLeft))]
    private double targetCm;

    [ObservableProperty]
    private bool hasTarget;

    public double TrolleyLeft => TrackLeft + CmToTrack(PositionCm) - (TrolleyWidth / 2);

    public double PositionLabelLeft => TrolleyLeft + (TrolleyWidth / 2) - (PositionLabelWidth / 2);

    public double TargetLeft => TrackLeft + CmToTrack(TargetCm) - (TargetMarkerWidth / 2);

    public string PositionText => $"{PositionCm:0.00} cm";

    /// <summary>Przelicza współrzędną X na kanwie na centymetry (0-80), z przycięciem do zakresu skali.</summary>
    public static double CanvasXToCm(double x) =>
        Math.Clamp((x - TrackLeft) / TrackWidth, 0, 1) * PlcScaling.MaxPositionCm;

    private static double CmToTrack(double cm) =>
        Math.Clamp(cm / PlcScaling.MaxPositionCm, 0, 1) * TrackWidth;
}
