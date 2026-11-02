using System.Diagnostics;
using CraneControl.App.Models;

namespace CraneControl.App.Services;

/// <summary>
/// Symulator dźwignicy. Implementuje ten sam kontrakt rejestrów co realny sterownik:
/// polecenia osi X są wyzwalane zboczami narastającymi, oś Y działa na PionEN/kierunek/prędkość.
/// </summary>
public sealed class SimulatedPlcClient : IPlcClient
{
    private const double JogStepCm = 10;
    private const double HomingSpeedCmPerS = 15;
    private const double HomingAccelCmPerS2 = 100;
    private const double PositionToleranceCm = 0.005;
    private const double PionRampRpmPerS = 400;

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private double _positionCm = 25;
    private double _velocityCmPerS;
    private double _targetCm = 25;
    private double _maxSpeedCmPerS;
    private double _accelCmPerS2;
    private double _decelCmPerS2;
    private bool _isMoving;
    private bool _isHoming;
    private bool _referenced;
    private double _pionRpm;
    private TimeSpan _lastUpdate;
    private PlcOutputs _outputs = PlcOutputs.Default;

    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken ct)
    {
        IsConnected = true;
        _lastUpdate = _stopwatch.Elapsed;
        return Task.CompletedTask;
    }

    /// <summary>Symulator zachowuje się jak PLC: reaguje na zbocza narastające wyzwalaczy.</summary>
    public Task WriteOutputsAsync(PlcOutputs outputs, CancellationToken ct)
    {
        var previous = _outputs;
        _outputs = outputs;

        if (outputs.PozEnable)
        {
            _maxSpeedCmPerS = PlcScaling.RawToVelocityMps(outputs.PozVelocity) * 100;
            _accelCmPerS2 = PlcScaling.RawToAccelMps2(outputs.OverAcc) * 100;
            _decelCmPerS2 = PlcScaling.RawToAccelMps2(outputs.OverDec) * 100;

            if (outputs.PozExecute && !previous.PozExecute)
            {
                if (outputs.PozMode == PozModes.Absolute)
                {
                    StartMove(PlcScaling.RawToPositionCm(outputs.PozPosition), homing: false);
                }
                else if (outputs.PozMode == PozModes.Homing)
                {
                    StartMove(0, homing: true);
                }
            }

            if (outputs.PozMode == PozModes.JogStep)
            {
                if (outputs.PozJog1 && !previous.PozJog1)
                {
                    StartMove(_targetCm - JogStepCm, homing: false);
                }
                else if (outputs.PozJog2 && !previous.PozJog2)
                {
                    StartMove(_targetCm + JogStepCm, homing: false);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<CraneState> ReadStateAsync(CancellationToken ct)
    {
        var now = _stopwatch.Elapsed;
        var dt = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;
        if (dt > 0)
        {
            StepAxisX(dt);
            StepPion(dt);
        }

        var positionRaw = PlcScaling.PositionToRaw(_positionCm);
        return Task.FromResult(new CraneState
        {
            PositionXcm = PlcScaling.RawToPositionCm(positionRaw),
            AxisXEnabled = _outputs.PozEnable,
            AxisXReferenced = _referenced,
            SpeedYRpm = PlcScaling.RawToPionSpeedOutRpm(PlcScaling.PionSpeedOutRpmToRaw(_pionRpm)),
        });
    }

    private void StartMove(double targetCm, bool homing)
    {
        _targetCm = Math.Clamp(targetCm, 0, PlcScaling.MaxPositionCm);
        _isMoving = true;
        _isHoming = homing;
        if (homing)
        {
            _referenced = false;
        }
    }

    private void StepAxisX(double dt)
    {
        if (!_isMoving)
        {
            return;
        }

        var maxSpeed = _isHoming ? HomingSpeedCmPerS : _maxSpeedCmPerS;
        var accel = Math.Max(_isHoming ? HomingAccelCmPerS2 : _accelCmPerS2, 1);
        var decel = Math.Max(_isHoming ? HomingAccelCmPerS2 : _decelCmPerS2, 1);

        var distance = _targetCm - _positionCm;
        var direction = Math.Sign(distance);
        var brakingDistance = _velocityCmPerS * _velocityCmPerS / (2 * decel);

        var desiredVelocity = Math.Abs(distance) <= brakingDistance
            ? direction * Math.Sqrt(2 * decel * Math.Abs(distance))
            : direction * maxSpeed;

        var rate = Math.Abs(desiredVelocity) > Math.Abs(_velocityCmPerS) ? accel : decel;
        var maxDelta = rate * dt;
        _velocityCmPerS += Math.Clamp(desiredVelocity - _velocityCmPerS, -maxDelta, maxDelta);

        var next = _positionCm + _velocityCmPerS * dt;
        var overshot = Math.Sign(_targetCm - next) != direction;
        if (overshot || Math.Abs(_targetCm - next) <= PositionToleranceCm || maxSpeed <= 0)
        {
            _positionCm = maxSpeed <= 0 ? _positionCm : _targetCm;
            _velocityCmPerS = 0;
            _isMoving = false;
            if (_isHoming)
            {
                _referenced = true;
                _isHoming = false;
            }

            return;
        }

        _positionCm = next;
    }

    private void StepPion(double dt)
    {
        var targetRpm = _outputs.PionEnable
            ? _outputs.PionSpeed * (_outputs.PionDirectionDown ? -1 : 1)
            : 0;
        var maxDelta = PionRampRpmPerS * dt;
        _pionRpm += Math.Clamp(targetRpm - _pionRpm, -maxDelta, maxDelta);
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
