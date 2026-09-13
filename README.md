# Mapa rejestrów Modbus TCP/IP dla sterownika PLC dźwignicy

Aplikacja (master) łączy się ze sterownikiem PLC (slave) protokołem **Modbus TCP/IP**
(port domyślny 502, jednostka/slave ID = 1) i korzysta wyłącznie z **Holding Registers**
(odczyt: FC03, zapis: FC16 – zapis wielu rejestrów naraz).

Wszystkie stałe znajdują się też w kodzie: [PlcRegisterMap.cs](src/CraneControl.App/Models/PlcRegisterMap.cs).

## 1. Rejestry odczytywane przez aplikację (PLC → aplikacja)

Blok ciągły, adresy 0–3 (`ReadHoldingRegisters(start=0, count=4)`).

|------------------------------------------------------------------------------------------------------------------------|
| Adres HR | Nazwa           | Typ / skala                     | Opis                                                    |
|---------:|-----------------|---------------------------------|---------------------------------------------------------|
| 0        | `PositionX`     | UInt16, mm × 10 (0.1 mm)        | Aktualna pozycja haka w osi X                           |
| 1        | `PositionY`     | UInt16, mm × 10 (0.1 mm)        | Aktualna pozycja haka w osi Y                           |
| 2        | `StatusWord`    | UInt16, bitfield (patrz niżej)  | Słowo stanu / bity statusowe                            |
| 3        | `FaultCode`     | UInt16                          | Kod błędu (0 = brak błędu)                              |
|------------------------------------------------------------------------------------------------------------------------|

### Bity rejestru `StatusWord` (adres 2)

|-------------------------------------------------------------------------------|
| Bit | Nazwa            | Opis                                                 |
|----:|------------------|------------------------------------------------------|
| 0   | `Ready`          | Napędy gotowe do pracy                               |
| 1   | `Fault`          | Aktywna awaria (szczegóły w `FaultCode`)             |
| 2   | `MovingX`        | Trwa ruch w osi X                                    |
| 3   | `MovingY`        | Trwa ruch w osi Y                                    |
| 4   | `LimitXMinus`    | Krańcówka osi X w kierunku minus                     |
| 5   | `LimitXPlus`     | Krańcówka osi X w kierunku plus                      |
| 6   | `LimitYMinus`    | Krańcówka osi Y w kierunku minus                     |
| 7   | `LimitYPlus`     | Krańcówka osi Y w kierunku plus                      |
| 8   | `AutoModeActive` | PLC potwierdza aktywny tryb automatyczny             |
| 9   | `InPosition`     | Pozycja docelowa osiągnięta (tryb automatyczny)      |
|-------------------------------------------------------------------------------|

## 2. Rejestry zapisywane przez aplikację (aplikacja → PLC)

Blok ciągły, adresy 10–16 (`WriteMultipleRegisters(start=10, count=7)`), zapisywany
cyklicznie co ok. 100 ms (jeden "skan").

|-----------------------------------------------------------------------------------------------------------------------------|
| Adres HR | Nazwa         | Typ / skala                       | Opis                                                         |
|---------:|---------------|-----------------------------------|--------------------------------------------------------------|
| 10       | `CommandWord` | UInt16, bitfield (patrz niżej)    | Słowo rozkazowe                                              |
| 11       | `JogX`        | Int16, -100..100                  | Ręczny rozkaz jazdy w osi X (kierunek i % prędkości)         |
| 12       | `JogY`        | Int16, -100..100                  | Ręczny rozkaz jazdy w osi Y                                  |
| 13       | `SpeedX`      | UInt16, 0..100 (%)                | Zadana prędkość osi X (z suwaka)                             |
| 14       | `SpeedY`      | UInt16, 0..100 (%)                | Zadana prędkość osi Y (z suwaka)                             |
| 15       | `TargetX`     | UInt16, mm × 10                   | Pozycja docelowa X (tryb automatyczny)                       |
| 16       | `TargetY`     | UInt16, mm × 10                   | Pozycja docelowa Y (tryb automatyczny)                       |
|-----------------------------------------------------------------------------------------------------------------------------|

### Bity rejestru `CommandWord` (adres 10)

|---------------------------------------------------------------------------------------------|
| Bit | Nazwa        | Opis                                                                   |
|----:|--------------|------------------------------------------------------------------------|
| 0   | `AutoMode`   | 0 = tryb ręczny, 1 = tryb automatyczny                                 |
| 1   | `Stop`       | 1 = natychmiastowe zatrzymanie ruchu (stop)                            |
| 2   | `StartMove`  | Zbocze narastające = start ruchu do `TargetX`/`TargetY` (tryb auto)    |
| 3   | `Enable`     | Zezwolenie na pracę napędów (musi być 1, aby dźwignica mogła jechać)   |
|---------------------------------------------------------------------------------------------|

## 3. Pamiętać podczas zmian w oprogramowaniu PLC

- Rozdzielczość pozycji i celu to **0.1 mm** - wartość rejestru = mm × 10.
- `JogX`/`JogY` to liczby ze znakiem (Int16): wartość ujemna = ruch w stronę "minus",
  dodatnia = ruch w stronę "plus", 0 = stop. Wartość bezwzględna (0–100) to wyskalowany
  procent zadanej prędkości `SpeedX`/`SpeedY`.
- W trybie automatycznym `JogX`/`JogY` są ignorowane (aplikacja zawsze wysyła 0) -
  ruchem steruje `TargetX`/`TargetY` + zbocze na bicie `StartMove`.
- Bit `MovingX`/`MovingY` powinien być ustawiony przez cały czas trwania ruchu (aplikacja
  używa go m.in. do włączania/wyłączania rejestrowania wykresów w trybie automatycznym).
- `InPosition` powinien się ustawić dopiero, gdy oba napędy (X i Y) osiągną zadaną pozycję
  w trybie automatycznym.
- Zalecane ograniczenia bezpieczeństwa (krańcówki, blokady) powinny być realizowane po
  stronie PLC niezależnie od aplikacji - aplikacja jedynie odczytuje ich stan (bity 4–7).
