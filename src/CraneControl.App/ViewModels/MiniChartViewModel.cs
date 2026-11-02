using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CraneControl.App.ViewModels;

/// <summary>Pojedynczy wykres liniowy wartości w funkcji czasu trwania ruchu.</summary>
public sealed class MiniChartViewModel
{
    private readonly ObservableCollection<ObservablePoint> _points = new();
    private readonly double _minSpan;

    public MiniChartViewModel(string title, string unit, uint colorArgb, double minSpan, string format = "0.##")
    {
        Title = title;
        _minSpan = minSpan;
        var color = new SKColor(colorArgb);
        Series =
        [
            new LineSeries<ObservablePoint>
            {
                Values = _points,
                Stroke = new SolidColorPaint(color, 2.5f),
                Fill = new SolidColorPaint(color.WithAlpha(40)),
                GeometrySize = 0,
                LineSmoothness = 0,
            }
        ];

        XAxes = [new Axis { Labeler = v => $"{v:0.0}s", MinLimit = 0, TextSize = 11 }];
        YAxes = [new Axis { Labeler = v => $"{v.ToString(format)}{unit}", TextSize = 11 }];
    }

    public string Title { get; }

    public ISeries[] Series { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    public void Add(double x, double y)
    {
        _points.Add(new ObservablePoint(x, y));
        UpdateYRange();
    }

    public void Clear()
    {
        _points.Clear();
        XAxes[0].MinLimit = 0;
        UpdateYRange();
    }

    /// <summary>Usuwa punkty starsze niż <paramref name="x"/> i przesuwa lewy kraniec osi czasu.</summary>
    public void TrimBefore(double x)
    {
        while (_points.Count > 0 && _points[0].X < x)
        {
            _points.RemoveAt(0);
        }

        XAxes[0].MinLimit = Math.Max(0, x);
        UpdateYRange();
    }

    /// <summary>
    /// Wymusza minimalny rozstaw osi Y, żeby wykres nie "skakał" i nie dążył do
    /// nieskończonego przybliżenia, gdy wartości są bliskie zeru.
    /// </summary>
    private void UpdateYRange()
    {
        if (_points.Count == 0)
        {
            YAxes[0].MinLimit = null;
            YAxes[0].MaxLimit = null;
            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var point in _points)
        {
            var y = point.Y ?? 0;
            min = Math.Min(min, y);
            max = Math.Max(max, y);
        }

        var center = (min + max) / 2.0;
        var span = Math.Max((max - min) * 1.3, _minSpan);
        YAxes[0].MinLimit = center - (span / 2.0);
        YAxes[0].MaxLimit = center + (span / 2.0);
    }
}
