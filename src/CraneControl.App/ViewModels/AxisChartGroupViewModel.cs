namespace CraneControl.App.ViewModels;

/// <summary>
/// Grupa trzech wykresów (prędkość, przyspieszenie, droga) dla jednej osi.
/// W trybie ręcznym działa jak bufor kołowy (ostatnie 15 s) - najstarszy punkt jest
/// zawsze przebazowywany do X=0. W trybie automatycznym
/// rysuje od momentu startu ruchu (też od X=0) i zamraża się po jego zakończeniu.
/// </summary>
public sealed class AxisChartGroupViewModel
{
    private const double ManualWindowSeconds = 15;

    private readonly List<double> _timestamps = new();

    public AxisChartGroupViewModel(string axisName, uint velocityColor, uint accelerationColor, uint distanceColor)
    {
        GroupTitle = $"Oś {axisName}";
        Velocity = new MiniChartViewModel($"Prędkość ({axisName})", " mm/s", velocityColor, minSpan: 20);
        Acceleration = new MiniChartViewModel($"Przyspieszenie ({axisName})", " mm/s²", accelerationColor, minSpan: 60);
        Distance = new MiniChartViewModel($"Droga ({axisName})", " mm", distanceColor, minSpan: 20);
    }

    public string GroupTitle { get; }

    public MiniChartViewModel Velocity { get; }

    public MiniChartViewModel Acceleration { get; }

    public MiniChartViewModel Distance { get; }

    /// <summary>Dodaje próbkę i w trybie ręcznym przycina bufor do ostatnich 15 s.</summary>
    public void AddSample(double elapsedSeconds, double velocity, double acceleration, double distance, bool trimToWindow)
    {
        _timestamps.Add(elapsedSeconds);
        Velocity.AddRaw(velocity);
        Acceleration.AddRaw(acceleration);
        Distance.AddRaw(distance);

        if (trimToWindow)
        {
            var cutoff = elapsedSeconds - ManualWindowSeconds;
            while (_timestamps.Count > 0 && _timestamps[0] < cutoff)
            {
                _timestamps.RemoveAt(0);
                Velocity.RemoveOldest();
                Acceleration.RemoveOldest();
                Distance.RemoveOldest();
            }
        }

        RebaseX();
    }

    /// <summary>Czyści wykresy przed nowym ruchem w trybie automatycznym.</summary>
    public void ClearAndRestart()
    {
        _timestamps.Clear();
        Velocity.Clear();
        Acceleration.Clear();
        Distance.Clear();
    }

    /// <summary>Przelicza X wszystkich zachowanych punktów tak, aby najstarszy zawsze wypadał w X=0.</summary>
    private void RebaseX()
    {
        if (_timestamps.Count == 0)
        {
            return;
        }

        var origin = _timestamps[0];
        for (var i = 0; i < _timestamps.Count; i++)
        {
            var relative = _timestamps[i] - origin;
            Velocity.SetX(i, relative);
            Acceleration.SetX(i, relative);
            Distance.SetX(i, relative);
        }
    }
}

