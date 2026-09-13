namespace CraneControl.App.Models;

/// <summary>
/// Wylicza prędkość, przyspieszenie i przebytą drogę jednej osi na podstawie kolejnych
/// odczytów położenia (jedyna informacja dostępna z PLC). Stosuje lekkie wygładzanie (EMA),
/// żeby wykresy nie "skakały" przez kwantyzację rejestru (0.1 mm).
/// </summary>
public sealed class AxisMotionTracker
{
    private const double SmoothingFactor = 0.35;

    private double _lastPosition;
    private DateTime _lastTime;
    private bool _initialized;

    public double VelocityMmPerS { get; private set; }
    public double AccelerationMmPerS2 { get; private set; }
    public double DistanceTraveledMm { get; private set; }

    public void Update(double positionMm, DateTime timestamp)
    {
        if (!_initialized)
        {
            _lastPosition = positionMm;
            _lastTime = timestamp;
            _initialized = true;
            return;
        }

        var dt = (timestamp - _lastTime).TotalSeconds;
        if (dt <= 0)
        {
            return;
        }

        var instantVelocity = (positionMm - _lastPosition) / dt;
        var instantAcceleration = (instantVelocity - VelocityMmPerS) / dt;

        VelocityMmPerS += SmoothingFactor * (instantVelocity - VelocityMmPerS);
        AccelerationMmPerS2 += SmoothingFactor * (instantAcceleration - AccelerationMmPerS2);
        DistanceTraveledMm += Math.Abs(positionMm - _lastPosition);

        _lastPosition = positionMm;
        _lastTime = timestamp;
    }

    public void Reset()
    {
        _initialized = false;
        VelocityMmPerS = 0;
        AccelerationMmPerS2 = 0;
        DistanceTraveledMm = 0;
    }
}
