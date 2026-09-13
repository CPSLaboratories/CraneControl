namespace CraneControl.App.Models;

/// <summary>Aktualny stan dźwignicy odczytany ze sterownika PLC.</summary>
public sealed class CraneState
{
    public double PositionXmm { get; init; }
    public double PositionYmm { get; init; }
    public bool Ready { get; init; }
    public bool Fault { get; init; }
    public bool MovingX { get; init; }
    public bool MovingY { get; init; }
    public bool LimitXMinus { get; init; }
    public bool LimitXPlus { get; init; }
    public bool LimitYMinus { get; init; }
    public bool LimitYPlus { get; init; }
    public bool AutoModeActive { get; init; }
    public bool InPosition { get; init; }
    public int FaultCode { get; init; }
}

/// <summary>Komplet wartości wysyłanych do PLC w jednym cyklu zapisu (rejestry 10-16).</summary>
public sealed record PlcOutputs
{
    public bool AutoMode { get; init; }
    public bool Stop { get; init; }
    public bool StartMove { get; init; }
    public bool Enable { get; init; } = true;
    public sbyte JogX { get; init; }
    public sbyte JogY { get; init; }
    public byte SpeedX { get; init; } = 50;
    public byte SpeedY { get; init; } = 50;
    public double TargetXmm { get; init; }
    public double TargetYmm { get; init; }

    public static PlcOutputs Default { get; } = new();
}
