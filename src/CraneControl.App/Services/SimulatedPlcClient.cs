using System.Diagnostics;
using CraneControl.App.Models;

namespace CraneControl.App.Services;

/// <summary>
/// Symulator fizyki dźwignicy. Implementuje ten sam kontrakt co realny klient Modbus.
/// </summary>
public sealed class SimulatedPlcClient : IPlcClient
{
    private const double MaxSpeedMmPerS = 250;
    private const double AccelerationMmPerS2 = 350;
    private const double MoveThresholdMmPerS = 1.0;
    private const double AutoPositionToleranceMm = 0.15;

    private readonly double _travelXmm;
    private readonly double _travelYmm;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private double _positionX = 200;
    private double _positionY = 150;
    private double _velocityX;
    private double _velocityY;
    private TimeSpan _lastUpdate;
    private PlcOutputs _outputs = PlcOutputs.Default;
    private bool _hasAutoTarget;

    public SimulatedPlcClient(double travelXmm, double travelYmm)
    {
        _travelXmm = travelXmm;
        _travelYmm = travelYmm;
    }

    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken ct)
    {
        IsConnected = true;
        _lastUpdate = _stopwatch.Elapsed;
        return Task.CompletedTask;
    }

    /// <summary>Symulator odczytuje ostatnio ustawione wyjścia zamiast prawdziwego zapisu do PLC.</summary>
    public Task WriteOutputsAsync(PlcOutputs outputs, CancellationToken ct)
    {
        _outputs = outputs;
        if (!outputs.AutoMode)
        {
            _hasAutoTarget = false;
        }
        else if (outputs.StartMove)
        {
            _hasAutoTarget = true;
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
            Step(dt);
        }

        var state = new CraneState
        {
            PositionXmm = _positionX,
            PositionYmm = _positionY,
            Ready = true,
            Fault = false,
            MovingX = Math.Abs(_velocityX) > MoveThresholdMmPerS,
            MovingY = Math.Abs(_velocityY) > MoveThresholdMmPerS,
            LimitXMinus = _positionX <= 0,
            LimitXPlus = _positionX >= _travelXmm,
            LimitYMinus = _positionY <= 0,
            LimitYPlus = _positionY >= _travelYmm,
            AutoModeActive = _outputs.AutoMode,
            InPosition = _outputs.AutoMode && _hasAutoTarget
                && Math.Abs(_positionX - _outputs.TargetXmm) <= AutoPositionToleranceMm
                && Math.Abs(_positionY - _outputs.TargetYmm) <= AutoPositionToleranceMm,
            FaultCode = 0,
        };

        return Task.FromResult(state);
    }

    private void Step(double dt)
    {
        if (_outputs.Stop)
        {
            _velocityX = RampToward(_velocityX, 0, dt);
            _velocityY = RampToward(_velocityY, 0, dt);

            _positionX = Math.Clamp(_positionX + _velocityX * dt, 0, _travelXmm);
            _positionY = Math.Clamp(_positionY + _velocityY * dt, 0, _travelYmm);
            return;
        }

        if (_outputs.AutoMode)
        {
            if (!_hasAutoTarget)
            {
                _velocityX = RampToward(_velocityX, 0, dt);
                _velocityY = RampToward(_velocityY, 0, dt);
                return;
            }

            (_positionX, _velocityX) = StepAutoAxis(_positionX, _outputs.TargetXmm, _velocityX, _outputs.SpeedX, dt, _travelXmm);
            (_positionY, _velocityY) = StepAutoAxis(_positionY, _outputs.TargetYmm, _velocityY, _outputs.SpeedY, dt, _travelYmm);
            return;
        }

        var targetVelX = _outputs.JogX / 100.0 * (_outputs.SpeedX / 100.0) * MaxSpeedMmPerS;
        var targetVelY = _outputs.JogY / 100.0 * (_outputs.SpeedY / 100.0) * MaxSpeedMmPerS;
        _velocityX = RampToward(_velocityX, targetVelX, dt);
        _velocityY = RampToward(_velocityY, targetVelY, dt);

        var nextX = _positionX + _velocityX * dt;
        var nextY = _positionY + _velocityY * dt;

        if (nextX <= 0 || nextX >= _travelXmm)
        {
            _velocityX = 0;
        }

        if (nextY <= 0 || nextY >= _travelYmm)
        {
            _velocityY = 0;
        }

        _positionX = Math.Clamp(nextX, 0, _travelXmm);
        _positionY = Math.Clamp(nextY, 0, _travelYmm);
    }

    /// <summary>
    /// Dojeżdża do celu: gdy jest już bardzo blisko albo krok symulacji przeskoczyłby
    /// przez cel, pozycja jest "dopinana" do wartości docelowej zamiast zbliżać się do niej asymptotycznie.
    /// </summary>
    private static (double Position, double Velocity) StepAutoAxis(
        double position, double target, double velocity, byte speedPercent, double dt, double travel)
    {
        var distanceToTarget = target - position;
        if (Math.Abs(distanceToTarget) <= AutoPositionToleranceMm && Math.Abs(velocity) < 2)
        {
            return (Math.Clamp(target, 0, travel), 0);
        }

        var maxSpeed = MaxSpeedMmPerS * (speedPercent / 100.0);
        var brakingDistance = velocity * velocity / (2 * AccelerationMmPerS2);

        double desiredVelocity;
        if (Math.Abs(distanceToTarget) <= brakingDistance)
        {
            desiredVelocity = Math.Sign(distanceToTarget) * Math.Sqrt(2 * AccelerationMmPerS2 * Math.Abs(distanceToTarget));
        }
        else
        {
            desiredVelocity = Math.Sign(distanceToTarget) * maxSpeed;
        }

        var newVelocity = RampToward(velocity, Math.Clamp(desiredVelocity, -maxSpeed, maxSpeed), dt);
        var newPosition = position + newVelocity * dt;

        var overshot = Math.Sign(target - position) != Math.Sign(target - newPosition);
        if (overshot || Math.Abs(target - newPosition) <= AutoPositionToleranceMm)
        {
            return (Math.Clamp(target, 0, travel), 0);
        }

        return (Math.Clamp(newPosition, 0, travel), newVelocity);
    }

    private static double RampToward(double current, double target, double dt)
    {
        var maxDelta = AccelerationMmPerS2 * dt;
        var delta = Math.Clamp(target - current, -maxDelta, maxDelta);
        return current + delta;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
