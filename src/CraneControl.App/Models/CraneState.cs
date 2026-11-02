namespace CraneControl.App.Models;

/// <summary>Aktualny stan dźwignicy odczytany ze sterownika PLC.</summary>
public sealed class CraneState
{
    public double PositionXcm { get; init; }
    public bool AxisXEnabled { get; init; }
    public bool AxisXReferenced { get; init; }

    /// <summary>Prędkość obrotowa silnika osi Y; dodatnia = podnoszenie, ujemna = opuszczanie.</summary>
    public double SpeedYRpm { get; init; }
}

/// <summary>Komplet wartości wysyłanych do PLC w jednym cyklu zapisu (rejestry 10-23), w wartościach surowych.</summary>
public sealed record PlcOutputs
{
    public bool PozEnable { get; init; }
    public ushort PozMode { get; init; }
    public bool PozJog1 { get; init; }
    public bool PozJog2 { get; init; }
    public bool PozAckError { get; init; }
    public bool PozExecute { get; init; }
    public ushort PozPosition { get; init; }
    public ushort PozVelocity { get; init; }
    public ushort OverAcc { get; init; }
    public ushort OverDec { get; init; }
    public bool PionEnable { get; init; }
    public ushort PionSpeed { get; init; }
    public bool PionDirectionDown { get; init; }
    public bool PionAckError { get; init; }

    public static PlcOutputs Default { get; } = new();
}
