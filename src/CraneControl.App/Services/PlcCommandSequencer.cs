using System.Diagnostics;
using CraneControl.App.Models;

namespace CraneControl.App.Services;

/// <summary>
/// Buduje wyjścia dla PLC w każdym cyklu zapisu. Odpowiada za sekwencje wymagające zbocza narastającego
/// (PozExecute, PozJog1/2, PozAckError, PionAckError): sygnał jest najpierw 0, dopiero potem 1.
/// Metody publiczne są bezpieczne wątkowo (UI wywołuje je, pętla komunikacji woła <see cref="Next"/>).
/// </summary>
public sealed class PlcCommandSequencer
{
    private const int MaxQueuedCommands = 4;
    private static readonly TimeSpan PulseHoldTime = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan HomingStationaryTime = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HomingNoMotionTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HomingMaxTime = TimeSpan.FromSeconds(120);

    private enum MotionKind { MoveAbsolute, Home, JogLeft, JogRight }

    private readonly record struct MotionCommand(MotionKind Kind, ushort Mode, ushort PositionRaw);

    private readonly object _lock = new();
    private readonly Queue<MotionCommand> _queue = new();
    private readonly int _holdCycles;

    private MotionCommand? _current;
    private int _phase;
    private ushort _mode = PozModes.Absolute;
    private ushort _positionRaw;
    private ushort _velocityRaw;
    private ushort _accelRaw;
    private ushort _decelRaw;
    private int _ackXCyclesLeft;
    private int _ackYCyclesLeft;
    private int _enableLowCyclesLeft;
    private bool _pozEnableForcedOff;
    private bool _pionEnabled;
    private bool _pionDown;
    private ushort _pionSpeed;
    private ushort? _lastPositionRaw;
    private long _positionChangedAt;
    private long _homingStartedAt;
    private bool _homingMoved;

    public PlcCommandSequencer(TimeSpan cyclePeriod)
    {
        _holdCycles = Math.Max(2, (int)Math.Ceiling(PulseHoldTime / cyclePeriod));
        _enableLowCyclesLeft = _holdCycles;
    }

    /// <summary>
    /// Wywoływane po każdym połączeniu: PozEN spada do 0 na czas impulsu, a potem rośnie do 1 (zbocze narastające).
    /// Oczekujące polecenia ruchu są porzucane.
    /// </summary>
    public void RestartPozEnable()
    {
        lock (_lock)
        {
            _queue.Clear();
            _current = null;
            _enableLowCyclesLeft = _holdCycles;
        }
    }

    /// <summary>Ręczne OFF: PozEN trzymane na 0, polecenia ruchu są odrzucane. Po zdjęciu wymuszenia PozEN rośnie do 1 po min. jednym impulsie niskim.</summary>
    public void SetPozEnableForcedOff(bool forcedOff)
    {
        lock (_lock)
        {
            _pozEnableForcedOff = forcedOff;
            _queue.Clear();
            _current = null;
            _enableLowCyclesLeft = _holdCycles;
        }
    }

    public void SetPozMotionParameters(double velocityMps, double accelMps2, double decelMps2)
    {
        lock (_lock)
        {
            _velocityRaw = PlcScaling.VelocityToRaw(velocityMps);
            _accelRaw = PlcScaling.AccelToRaw(accelMps2);
            _decelRaw = PlcScaling.AccelToRaw(decelMps2);
        }
    }

    public void MoveAbsolute(double positionCm) =>
        Enqueue(new MotionCommand(MotionKind.MoveAbsolute, PozModes.Absolute, PlcScaling.PositionToRaw(positionCm)));

    public void Home() => Enqueue(new MotionCommand(MotionKind.Home, PozModes.Homing, 0));

    public void JogStep(bool right) =>
        Enqueue(new MotionCommand(right ? MotionKind.JogRight : MotionKind.JogLeft, PozModes.JogStep, 0));

    public void AckPozError()
    {
        lock (_lock)
        {
            if (_ackXCyclesLeft == 0)
            {
                _ackXCyclesLeft = _holdCycles;
            }
        }
    }

    public void AckPionError()
    {
        lock (_lock)
        {
            if (_ackYCyclesLeft == 0)
            {
                _ackYCyclesLeft = _holdCycles;
            }
        }
    }

    /// <summary>Jazda w osi Y: PionEN = enabled, kierunek 0 = góra, 1 = dół. Zmiana obowiązuje od najbliższego cyklu.</summary>
    public void SetPionDrive(bool enabled, bool down)
    {
        lock (_lock)
        {
            _pionEnabled = enabled;
            _pionDown = down;
        }
    }

    public void SetPionSpeed(double rpm)
    {
        lock (_lock)
        {
            _pionSpeed = (ushort)Math.Round(Math.Clamp(rpm, 0, PlcScaling.MaxPionSpeedRpm));
        }
    }

    /// <summary>Raportuje odczytaną pozycję osi X (rejestr surowy); służy do ustalenia końca bazowania.</summary>
    public void ReportPosition(ushort positionRaw)
    {
        lock (_lock)
        {
            if (_lastPositionRaw == positionRaw)
            {
                return;
            }

            _lastPositionRaw = positionRaw;
            _positionChangedAt = Stopwatch.GetTimestamp();
            if (_current is { Kind: MotionKind.Home } && _phase >= 2)
            {
                _homingMoved = true;
            }
        }
    }

    /// <summary>Zwraca wyjścia do zapisu w bieżącym cyklu i przesuwa sekwencje o jeden krok.</summary>
    public PlcOutputs Next()
    {
        lock (_lock)
        {
            var enableLow = _pozEnableForcedOff || _enableLowCyclesLeft > 0;
            if (_enableLowCyclesLeft > 0)
            {
                _enableLowCyclesLeft--;
            }

            if (!enableLow && _current is null && _queue.TryDequeue(out var next))
            {
                _current = next;
                _phase = 0;
                _homingMoved = false;
                _homingStartedAt = Stopwatch.GetTimestamp();
                _positionChangedAt = _homingStartedAt;
            }

            var kind = MotionKind.MoveAbsolute;
            var trigger = false;
            if (_current is { } command)
            {
                kind = command.Kind;
                _mode = command.Mode;
                if (command.Kind == MotionKind.MoveAbsolute)
                {
                    _positionRaw = command.PositionRaw;
                }

                // Faza 0: tryb i pozycja ustawione przy wyzerowanych wyzwalaczach; zbocze dopiero od fazy 1.
                trigger = _phase >= 1;
                _phase++;
                if (_phase > _holdCycles && (command.Kind != MotionKind.Home || IsHomingFinished()))
                {
                    _current = null;
                }
            }

            var ackX = _ackXCyclesLeft > 0;
            var ackY = _ackYCyclesLeft > 0;
            if (ackX)
            {
                _ackXCyclesLeft--;
            }

            if (ackY)
            {
                _ackYCyclesLeft--;
            }

            return new PlcOutputs
            {
                PozEnable = !enableLow,
                PozMode = _mode,
                PozExecute = trigger && kind is MotionKind.MoveAbsolute or MotionKind.Home,
                PozJog1 = trigger && kind == MotionKind.JogLeft,
                PozJog2 = trigger && kind == MotionKind.JogRight,
                PozAckError = ackX,
                PozPosition = _positionRaw,
                PozVelocity = _velocityRaw,
                OverAcc = _accelRaw,
                OverDec = _decelRaw,
                PionEnable = _pionEnabled,
                PionSpeed = _pionSpeed,
                PionDirectionDown = _pionDown,
                PionAckError = ackY,
            };
        }
    }

    // PozExecute przy bazowaniu trwa, dopóki oś się porusza; bez ruchu zwalniany po limicie czasu.
    private bool IsHomingFinished()
    {
        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_homingStartedAt, now) >= HomingMaxTime)
        {
            return true;
        }

        return _homingMoved
            ? Stopwatch.GetElapsedTime(_positionChangedAt, now) >= HomingStationaryTime
            : Stopwatch.GetElapsedTime(_homingStartedAt, now) >= HomingNoMotionTimeout;
    }

    private void Enqueue(MotionCommand command)
    {
        lock (_lock)
        {
            if (_queue.Count < MaxQueuedCommands && !_pozEnableForcedOff)
            {
                _queue.Enqueue(command);
            }
        }
    }
}
