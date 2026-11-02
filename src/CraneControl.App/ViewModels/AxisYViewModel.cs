using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraneControl.App.Models;
using CraneControl.App.Services;

namespace CraneControl.App.ViewModels;

/// <summary>Zakładka osi Y: podnoszenie silnikiem (obrotowo) i wykresy mechaniczne policzone z prędkości obrotowej.</summary>
public partial class AxisYViewModel : ObservableObject
{
    private const double ArmM = 0.03;
    private const double Gravity = 9.81;
    private const double SampleIntervalSeconds = 0.009;
    private const double StopAfterSeconds = 0.3;
    private const double ContinuousWindowSeconds = 60;
    private const double MovingThresholdRpm = 0.2;

    private readonly PlcCommandSequencer _commands;
    private bool _upHeld;
    private bool _downHeld;
    private double _lastSampleTime = double.NegativeInfinity;
    private bool _runActive;
    private double _lastMovingTime;
    private double _heightM;

    [ObservableProperty]
    private double speedSetRpm = 60;

    [ObservableProperty]
    private double loadKg;

    [ObservableProperty]
    private double torqueOffsetNm;

    [ObservableProperty]
    private double speedRpm;

    [ObservableProperty]
    private double linearSpeedMps;

    [ObservableProperty]
    private double torqueNm;

    [ObservableProperty]
    private double powerW;

    [ObservableProperty]
    private bool isMovingUp;

    [ObservableProperty]
    private bool isMovingDown;

    public AxisYViewModel(PlcCommandSequencer commands)
    {
        _commands = commands;
        Charts = new AxisChartGroupViewModel(
            "Oś Y — wykresy ruchu",
            columns: 2,
            new MiniChartViewModel("Prędkość obrotowa", " obr/min", 0xFFB37FEB, minSpan: 10, format: "0.#"),
            new MiniChartViewModel("Moment statyczny", " Nm", 0xFFFF6B6B, minSpan: 0.1, format: "0.##"),
            new MiniChartViewModel("Moc mechaniczna", " W", 0xFFFFB020, minSpan: 1, format: "0.##"),
            new MiniChartViewModel("Energia potencjalna", " J", 0xFF34D399, minSpan: 0.1, format: "0.##"));
        _commands.SetPionSpeed(SpeedSetRpm);
    }

    public AxisChartGroupViewModel Charts { get; }

    /// <summary>Tryb PLC: wykresy rysują się stale (okno 60 s), nie tylko w trakcie ruchu.</summary>
    public bool ContinuousRecording { get; set; }

    partial void OnSpeedSetRpmChanged(double value) => _commands.SetPionSpeed(value);

    [RelayCommand]
    private void AckError() => _commands.AckPionError();

    public void SetUpHeld(bool held)
    {
        _upHeld = held;
        UpdateDrive();
    }

    public void SetDownHeld(bool held)
    {
        _downHeld = held;
        UpdateDrive();
    }

    public void ReleaseKeys()
    {
        _upHeld = false;
        _downHeld = false;
        UpdateDrive();
    }

    // Obie strzałki naraz = stop; puszczenie = natychmiast PionEN = 0.
    private void UpdateDrive()
    {
        var up = _upHeld && !_downHeld;
        var down = _downHeld && !_upHeld;
        IsMovingUp = up;
        IsMovingDown = down;
        _commands.SetPionDrive(up || down, down);
    }

    public void ApplyState(CraneState state, double elapsedSeconds)
    {
        var rpm = state.SpeedYRpm;
        var omega = rpm * Math.PI / 30.0;
        var linearSpeed = omega * ArmM;
        var torque = (LoadKg * Gravity * ArmM) + TorqueOffsetNm;
        var power = torque * omega;

        SpeedRpm = rpm;
        LinearSpeedMps = linearSpeed;
        TorqueNm = torque;
        PowerW = power;

        var dt = elapsedSeconds - _lastSampleTime;
        if (dt < SampleIntervalSeconds)
        {
            return;
        }

        _lastSampleTime = elapsedSeconds;

        if (ContinuousRecording || Math.Abs(rpm) > MovingThresholdRpm)
        {
            _lastMovingTime = elapsedSeconds;
            if (!_runActive)
            {
                _runActive = true;
                _heightM = 0;
                Charts.Restart(elapsedSeconds);
                dt = SampleIntervalSeconds;
            }
        }

        if (!_runActive)
        {
            return;
        }

        _heightM += linearSpeed * dt;
        var potential = LoadKg * Gravity * _heightM;
        Charts.AddSample(elapsedSeconds, rpm, torque, power, potential);
        if (ContinuousRecording)
        {
            Charts.Trim(elapsedSeconds, ContinuousWindowSeconds);
        }

        if (elapsedSeconds - _lastMovingTime >= StopAfterSeconds)
        {
            _runActive = false;
        }
    }
}
