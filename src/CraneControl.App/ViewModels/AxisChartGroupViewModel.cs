namespace CraneControl.App.ViewModels;

/// <summary>
/// Zestaw wykresów jednej osi. Rejestruje tylko podczas ruchu: każdy nowy ruch czyści wykresy
/// i zaczyna od t = 0, a po zakończeniu ruchu wykresy pozostają zamrożone.
/// </summary>
public sealed class AxisChartGroupViewModel
{
    private double _runStart;

    public AxisChartGroupViewModel(string title, int columns, params MiniChartViewModel[] charts)
    {
        GroupTitle = title;
        Columns = columns;
        Charts = charts;
    }

    public string GroupTitle { get; }

    public int Columns { get; }

    public IReadOnlyList<MiniChartViewModel> Charts { get; }

    public void Restart(double elapsedSeconds)
    {
        _runStart = elapsedSeconds;
        foreach (var chart in Charts)
        {
            chart.Clear();
        }
    }

    /// <summary>Zachowuje tylko ostatnie <paramref name="windowSeconds"/> sekund (rejestracja ciągła).</summary>
    public void Trim(double elapsedSeconds, double windowSeconds)
    {
        var cutoff = (elapsedSeconds - _runStart) - windowSeconds;
        if (cutoff <= 0)
        {
            return;
        }

        foreach (var chart in Charts)
        {
            chart.TrimBefore(cutoff);
        }
    }

    /// <summary>Dodaje próbkę; wartości muszą być w kolejności zgodnej z <see cref="Charts"/>.</summary>
    public void AddSample(double elapsedSeconds, params double[] values)
    {
        var t = elapsedSeconds - _runStart;
        for (var i = 0; i < Charts.Count && i < values.Length; i++)
        {
            Charts[i].Add(t, values[i]);
        }
    }
}
