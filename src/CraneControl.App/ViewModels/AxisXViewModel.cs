using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraneControl.App.Models;
using CraneControl.App.Services;

namespace CraneControl.App.ViewModels;

/// <summary>Zakładka osi X: ruch poziomy (pozycja zadana, kliknięcie na skali, strzałki, bazowanie) i wykresy.</summary>
public partial class AxisXViewModel : ObservableObject
{
    private const double SampleIntervalSeconds = 0.009;
    private const double StopAfterSeconds = 0.3;
    private const double PositionChangeEpsilonCm = 0.005;

    private readonly PlcCommandSequencer _commands;
    private readonly AxisMotionTracker _tracker = new();
    private bool _leftHeld;
    private bool _rightHeld;
    private double _lastSampleTime = double.NegativeInfinity;
    private double _lastSamplePositionCm;
    private bool _hasLastSample;
    private bool _runActive;
    private double _lastChangeTime;

    [ObservableProperty]
    private double targetCm;

    [ObservableProperty]
    private double velocityMps = 1.0;

    [ObservableProperty]
    private double accelMps2 = 5.0;

    [ObservableProperty]
    private double decelMps2 = 5.0;

    [ObservableProperty]
    private bool isReferenced;

    [ObservableProperty]
    private bool isAxisEnabled;

    [ObservableProperty]
    private bool isPozEnForcedOff;

    public AxisXViewModel(PlcCommandSequencer commands)
    {
        _commands = commands;
        Visualization = new CraneVisualizationViewModel();
        Charts = new AxisChartGroupViewModel(
            "Oś X — wykresy ruchu",
            columns: 1,
            new MiniChartViewModel("Pozycja", " cm", 0xFF3DA5FF, minSpan: 2, format: "0.#"),
            new MiniChartViewModel("Prędkość", " m/s", 0xFFFFB020, minSpan: 0.2, format: "0.##"),
            new MiniChartViewModel("Przyspieszenie", " m/s²", 0xFF34D399, minSpan: 2, format: "0.#"));
        PushParameters();
    }

    public CraneVisualizationViewModel Visualization { get; }

    public AxisChartGroupViewModel Charts { get; }

    partial void OnVelocityMpsChanged(double value) => PushParameters();

    partial void OnIsPozEnForcedOffChanged(bool value) => _commands.SetPozEnableForcedOff(value);

    partial void OnAccelMps2Changed(double value) => PushParameters();

    partial void OnDecelMps2Changed(double value) => PushParameters();

    private void PushParameters() => _commands.SetPozMotionParameters(VelocityMps, AccelMps2, DecelMps2);

    [RelayCommand]
    private void GoToTarget() => MoveTo(TargetCm);

    [RelayCommand]
    private void Home() => _commands.Home();

    [RelayCommand]
    private void AckError() => _commands.AckPozError();

    /// <summary>Kliknięcie na skali wizualizacji zadaje nową pozycję docelową.</summary>
    public void MoveTo(double positionCm)
    {
        var clamped = Math.Clamp(positionCm, 0, PlcScaling.MaxPositionCm);
        TargetCm = clamped;
        Visualization.TargetCm = clamped;
        Visualization.HasTarget = true;
        _commands.MoveAbsolute(clamped);
    }

    /// <summary>Strzałka lewo/prawo: jedno wciśnięcie = jeden skok (autorepeat klawiatury jest ignorowany).</summary>
    public void OnJogKeyDown(bool right)
    {
        if (right ? _rightHeld : _leftHeld)
        {
            return;
        }

        if (right)
        {
            _rightHeld = true;
        }
        else
        {
            _leftHeld = true;
        }

        _commands.JogStep(right);
    }

    public void OnJogKeyUp(bool right)
    {
        if (right)
        {
            _rightHeld = false;
        }
        else
        {
            _leftHeld = false;
        }
    }

    public void ReleaseKeys()
    {
        _leftHeld = false;
        _rightHeld = false;
    }

    public void ApplyState(CraneState state, double elapsedSeconds)
    {
        Visualization.PositionCm = state.PositionXcm;
        IsReferenced = state.AxisXReferenced;
        IsAxisEnabled = state.AxisXEnabled;

        if (elapsedSeconds - _lastSampleTime < SampleIntervalSeconds)
        {
            return;
        }

        _lastSampleTime = elapsedSeconds;
        var positionCm = state.PositionXcm;
        _tracker.Update(positionCm / 100.0, elapsedSeconds);

        // Ruch trwa, dopóki pozycja się zmienia; po kilku próbkach bez zmiany wykresy zamarzają.
        var changed = _hasLastSample && Math.Abs(positionCm - _lastSamplePositionCm) > PositionChangeEpsilonCm;
        _lastSamplePositionCm = positionCm;
        _hasLastSample = true;

        if (changed)
        {
            _lastChangeTime = elapsedSeconds;
            if (!_runActive)
            {
                _runActive = true;
                Charts.Restart(elapsedSeconds);
            }
        }

        if (_runActive)
        {
            Charts.AddSample(elapsedSeconds, positionCm, _tracker.Velocity, _tracker.Acceleration);
            if (elapsedSeconds - _lastChangeTime >= StopAfterSeconds)
            {
                _runActive = false;
            }
        }
    }
}
