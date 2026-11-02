namespace CraneControl.App.Models;

/// <summary>
/// Wylicza prędkość i przyspieszenie na podstawie kolejnych odczytów położenia (jedyna informacja
/// dostępna z PLC) jako surowe różnice bez wygładzania.
/// Jednostki wynikowe wynikają z jednostek położenia (położenie w m -> m/s, m/s²).
/// </summary>
public sealed class AxisMotionTracker
{
    private double _lastPosition;
    private double _lastTime;
    private bool _initialized;

    public double Velocity { get; private set; }
    public double Acceleration { get; private set; }

    public void Update(double position, double timeSeconds)
    {
        if (!_initialized)
        {
            _lastPosition = position;
            _lastTime = timeSeconds;
            _initialized = true;
            return;
        }

        var dt = timeSeconds - _lastTime;
        if (dt <= 0)
        {
            return;
        }

        var newVelocity = (position - _lastPosition) / dt;
        Acceleration = (newVelocity - Velocity) / dt;
        Velocity = newVelocity;

        _lastPosition = position;
        _lastTime = timeSeconds;
    }
}
