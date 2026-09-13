namespace CraneControl.App.Models;

/// <summary>
/// Mapa rejestrów Modbus TCP (Holding Registers) używana do komunikacji ze sterownikiem PLC.
/// Pełny opis znajduje się w README.md w katalogu głównym repozytorium.
/// </summary>
public static class PlcRegisterMap
{
    // Odczyt: PLC -> aplikacja (Read Holding Registers, FC03)
    public const ushort PositionX = 0;   // mm * 10 (rozdzielczość 0.1 mm)
    public const ushort PositionY = 1;   // mm * 10
    public const ushort StatusWord = 2;  // patrz StatusBits
    public const ushort FaultCode = 3;   // 0 = brak błędu

    public const ushort ReadBlockStart = PositionX;
    public const ushort ReadBlockLength = 4;

    // Zapis: aplikacja -> PLC (Write Multiple Registers, FC16)
    public const ushort CommandWord = 10; // patrz CommandBits
    public const ushort JogX = 11;        // signed, -100..100 (kierunek i % prędkości ręcznej)
    public const ushort JogY = 12;        // signed, -100..100
    public const ushort SpeedX = 13;      // 0..100 % (zadana prędkość z suwaka)
    public const ushort SpeedY = 14;      // 0..100 %
    public const ushort TargetX = 15;     // mm * 10 (cel w trybie automatycznym)
    public const ushort TargetY = 16;     // mm * 10

    public const ushort WriteBlockStart = CommandWord;
    public const ushort WriteBlockLength = 7;

    /// <summary>Rozdzielczość pozycji: wartość rejestru = mm * PositionScale.</summary>
    public const double PositionScale = 10.0;
}

/// <summary>Znaczenie bitów rejestru StatusWord (adres 2, odczyt).</summary>
public static class StatusBits
{
    public const int Ready = 0;          // napędy gotowe
    public const int Fault = 1;          // awaria aktywna
    public const int MovingX = 2;        // trwa ruch w osi X
    public const int MovingY = 3;        // trwa ruch w osi Y
    public const int LimitXMinus = 4;    // krańcówka X-
    public const int LimitXPlus = 5;     // krańcówka X+
    public const int LimitYMinus = 6;    // krańcówka Y-
    public const int LimitYPlus = 7;     // krańcówka Y+
    public const int AutoModeActive = 8; // PLC potwierdza tryb automatyczny
    public const int InPosition = 9;     // pozycja docelowa osiągnięta (tryb auto)
}

/// <summary>Znaczenie bitów rejestru CommandWord (adres 10, zapis).</summary>
public static class CommandBits
{
    public const int AutoMode = 0;   // 0 = ręczny, 1 = automatyczny
    public const int Stop = 1;       // 1 = zatrzymanie ruchu (stop)
    public const int StartMove = 2;  // zbocze narastające = start ruchu do TargetX/TargetY
    public const int Enable = 3;     // zezwolenie na pracę napędów
}
