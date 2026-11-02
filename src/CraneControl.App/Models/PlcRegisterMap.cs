namespace CraneControl.App.Models;

/// <summary>
/// Mapa rejestrów Modbus TCP (Holding Registers) używana do komunikacji ze sterownikiem PLC.
/// Pełny opis znajduje się w README.md w katalogu głównym repozytorium.
/// Wszystkie flagi (Bool) to osobne rejestry z wartością 0/1.
/// </summary>
public static class PlcRegisterMap
{
    // Odczyt: PLC -> aplikacja (FC03)
    public const ushort PozPositionAct = 0;   // 0..8000 (0.01 cm)
    public const ushort PozAxisEnabled = 1;   // 0/1
    public const ushort PozAxisRef = 2;       // 0/1 (oś zbazowana)
    public const ushort PionSpeedOut = 3;     // Int16, 0.1 obr/min, dodatnia = podnoszenie

    public const ushort ReadBlockStart = PozPositionAct;
    public const ushort ReadBlockLength = 4;

    // Zapis: aplikacja -> PLC (FC16)
    public const ushort PozEN = 10;
    public const ushort PozMode = 11;
    public const ushort PozJog1 = 12;         // jazda w lewo
    public const ushort PozJog2 = 13;         // jazda w prawo
    public const ushort PozAckError = 14;
    public const ushort PozExecute = 15;
    public const ushort PozPositionSet = 16;  // 0..8000 (0.01 cm)
    public const ushort PozVelocity = 17;     // 0..1200
    public const ushort OverAcc = 18;         // 0..100
    public const ushort OverDec = 19;         // 0..100
    public const ushort PionEN = 20;
    public const ushort PionSpeedSet = 21;    // 0..150 obr/min
    public const ushort PionDirection = 22;   // 0 = góra, 1 = dół
    public const ushort PionAckError = 23;

    public const ushort WriteBlockStart = PozEN;
    public const ushort WriteBlockLength = 14;
}

/// <summary>Kody PozMode używane przez aplikację.</summary>
public static class PozModes
{
    public const ushort Absolute = 2;
    public const ushort Homing = 4;
    public const ushort JogStep = 8; // PLC wykonuje skok 10 cm na zbocze PozJog1/PozJog2
}

/// <summary>Przeliczenia jednostek inżynierskich na wartości rejestrów i z powrotem.</summary>
public static class PlcScaling
{
    public const double MaxPositionCm = 80.0;
    public const double MaxVelocityMps = 2.0;
    public const double MaxAccelMps2 = 20.0;
    public const int MaxPionSpeedRpm = 150;

    private const double PositionRawPerCm = 100.0;   // 8000 / 80 cm
    private const double VelocityRawPerMps = 600.0;  // 1200 / 2 m/s
    private const double AccelRawPerMps2 = 5.0;      // 100 / 20 m/s²
    private const double PionSpeedOutRawPerRpm = 10.0;

    public static ushort PositionToRaw(double cm) =>
        (ushort)Math.Round(Math.Clamp(cm, 0, MaxPositionCm) * PositionRawPerCm);

    public static double RawToPositionCm(ushort raw) => raw / PositionRawPerCm;

    public static ushort VelocityToRaw(double mps) =>
        (ushort)Math.Round(Math.Clamp(mps, 0, MaxVelocityMps) * VelocityRawPerMps);

    public static double RawToVelocityMps(ushort raw) => raw / VelocityRawPerMps;

    public static ushort AccelToRaw(double mps2) =>
        (ushort)Math.Round(Math.Clamp(mps2, 0, MaxAccelMps2) * AccelRawPerMps2);

    public static double RawToAccelMps2(ushort raw) => raw / AccelRawPerMps2;

    public static double RawToPionSpeedOutRpm(short raw) => raw / PionSpeedOutRawPerRpm;

    public static short PionSpeedOutRpmToRaw(double rpm) => (short)Math.Round(rpm * PionSpeedOutRawPerRpm);
}
