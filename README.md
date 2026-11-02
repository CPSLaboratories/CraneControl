# CraneControl

## Kompilacja i release

Zwykła kompilacja developerska:

```powershell
dotnet build src/CraneControl.App/CraneControl.App.csproj
```

Publikacja release jako self-contained Native AOT dla Windows x64:

```powershell
dotnet publish src/CraneControl.App/CraneControl.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts/release/win-x64
```

Gotowy artefakt znajduje się w `artifacts/release/win-x64`. Plik `CraneControl.App.exe`
jest natywną aplikacją AOT; trzy biblioteki DLL obok niego są natywnymi zależnościami
renderera Avalonia/Skia i muszą pozostać w tym samym katalogu.

Do uruchomienia release wystarczy dwukrotnie kliknąć `CraneControl.App.exe`.

# Mapa rejestrów Modbus TCP/IP dla sterownika PLC dźwignicy

Aplikacja (master) łączy się ze sterownikiem PLC (slave) protokołem **Modbus TCP/IP**
(port domyślny 502, jednostka/slave ID = 1) i korzysta wyłącznie z **Holding Registers**
(odczyt: FC03, zapis: FC16 – zapis wielu rejestrów naraz). Adresy są 0-based (adres protokołu).

- Każda zmienna to **jeden rejestr 16-bitowy**. Zmienne `Bool` to rejestry z wartością `0`/`1`.
- Zmienne `DInt` z tabeli zmiennych są tu przesyłane jako jeden rejestr (zakres nie przekracza 16 bitów).
- Cykl wymiany: **10 ms** (odczyt bloku, potem zapis bloku).
- Stałe są też w kodzie: [PlcRegisterMap.cs](src/CraneControl.App/Models/PlcRegisterMap.cs).

## 1. Rejestry odczytywane przez aplikację (PLC → aplikacja)

Blok ciągły: `ReadHoldingRegisters(start=0, count=4)`.

| Adres HR | Nazwa            | Zmienna PLC                         | Typ rejestru           | Zakres / skala                                     | Opis |
|---------:|------------------|-------------------------------------|------------------------|----------------------------------------------------|------|
| 0        | `PozPositionAct` | `PozPosition` (odczyt, DInt 0-8000) | UInt16                 | 0..8000 = 0..80,00 cm (**cm = rejestr / 100**)     | Aktualna pozycja osi X |
| 1        | `PozAxisEnabled` | `PozAxisEnabled`                    | Bool 0/1               | 0/1                                                | Napęd osi X aktywny |
| 2        | `PozAxisRef`     | `PozAxisRef`                        | Bool 0/1               | 0/1                                                | Oś X zbazowana (lampka "Zbazowana") |
| 3        | `PionSpeedOut`   | `PionPredkoscOut` (Real)            | **Int16 (ze znakiem)** | obr/min × 10 (**obr/min = rejestr / 10**)          | Rzeczywista prędkość silnika osi Y; **dodatnia = podnoszenie, ujemna = opuszczanie** |

W PLC: `HR3 := REAL_TO_INT(PionPredkoscOut * 10.0)` (wartość ze znakiem, 0,1 obr/min na jednostkę).

## 2. Rejestry zapisywane przez aplikację (aplikacja → PLC)

Blok ciągły: `WriteMultipleRegisters(start=10, count=14)`, zapisywany co cykl (10 ms).

| Adres HR | Nazwa            | Zmienna PLC                         | Typ rejestru | Zakres / skala                                    | Opis |
|---------:|------------------|-------------------------------------|--------------|---------------------------------------------------|------|
| 10       | `PozEN`          | `PozEN`                             | Bool 0/1     | 0/1                                               | Załączenie osi X (1 na stałe; **po każdym połączeniu najpierw 0 na ok. 300 ms, potem 1 – zbocze narastające**) |
| 11       | `PozMode`        | `PozMode`                           | UInt16       | 2 = pozycjonowanie, 4 = bazowanie, 8 = skok (jog) | Tryb pracy osi X |
| 12       | `PozJog1`        | `PozJog1`                           | Bool 0/1     | 0/1                                               | Skok w lewo (zbocze narastające, `PozMode` = 8) |
| 13       | `PozJog2`        | `PozJog2`                           | Bool 0/1     | 0/1                                               | Skok w prawo (zbocze narastające, `PozMode` = 8) |
| 14       | `PozAckError`    | `PozAckError`                       | Bool 0/1     | 0/1                                               | Kasowanie błędów osi X (zbocze narastające) |
| 15       | `PozExecute`     | `PozExecute`                        | Bool 0/1     | 0/1                                               | Wykonanie ruchu / bazowania (zbocze narastające) |
| 16       | `PozPositionSet` | `PozPosition` (zapis, DInt 0-8000)  | UInt16       | 0..8000 = 0..80,00 cm (**rejestr = cm × 100**)    | Pozycja zadana osi X |
| 17       | `PozVelocity`    | `PozVelocity`                       | UInt16       | 0..1200 = 0..2 m/s (**rejestr = m/s × 600**)      | Prędkość ruchu osi X |
| 18       | `OverAcc`        | `OverAcc`                           | UInt16       | 0..100 = 0..20 m/s² (**rejestr = m/s² × 5**)      | Przyspieszenie rozpędzania |
| 19       | `OverDec`        | `OverDec`                           | UInt16       | 0..100 = 0..20 m/s² (**rejestr = m/s² × 5**)      | Przyspieszenie hamowania |
| 20       | `PionEN`         | `PionEn`                            | Bool 0/1     | 0/1                                               | Załączenie ruchu osi Y; **1 tylko dopóki wciśnięta strzałka / przycisk** |
| 21       | `PionSpeedSet`   | `PionPredkosc`                      | UInt16       | 0..150 obr/min (bez przeliczania, liczba całkowita) | Prędkość zadana osi Y |
| 22       | `PionDirection`  | `PionKierunek`                      | Bool 0/1     | 0 = góra (podnoszenie), 1 = dół (opuszczanie)     | Kierunek osi Y |
| 23       | `PionAckError`   | `PionAckError`                      | Bool 0/1     | 0/1                                               | Kasowanie błędów osi Y (zbocze narastające) |

> Uwaga: w opisie zadania "Ack Błędy" osi X miało generować zbocze w `PozPosition` – zinterpretowano to jako
> `PozAckError` (zbocze narastające w rejestrze 14).

## 3. Sekwencje realizowane przez aplikację (wymagania dla PLC)

Wszystkie sygnały wyzwalane zboczem (`PozExecute`, `PozJog1`, `PozJog2`, `PozAckError`, `PionAckError`) są
najpierw `0`, potem `1` na ok. 300 ms (30 cykli), po czym wracają do `0`.
Polecenia ruchu osi X (jazda, bazowanie, skok) są kolejkowane (maks. 4) i wykonywane po kolei.

| Akcja                  | Rejestry |
|------------------------|----------|
| Jazda do pozycji       | `PozEN`=1, `PozMode`=2, `PozPositionSet`=cel (cykl z `PozExecute`=0), potem `PozExecute`=1 przez ok. 300 ms |
| Skok ±10 cm (strzałki) | `PozEN`=1, `PozMode`=8 (cykl z `PozJog1/2`=0), potem `PozJog1` (lewo) lub `PozJog2` (prawo) = 1 przez ok. 300 ms |
| Bazowanie              | `PozEN`=1, `PozMode`=4 (cykl z `PozExecute`=0), potem `PozExecute`=1 **do końca ruchu** osi (pozycja przestaje się zmieniać na 0,5 s; bez ruchu zwolnienie po 2 s, limit 120 s) |
| Kasowanie błędów X / Y | `PozAckError` / `PionAckError` = 1 przez ok. 300 ms (poprzednio 0) |
| Oś Y w górę            | `PionEN`=1, `PionKierunek`=0 (dopóki wciśnięta strzałka ↑); puszczenie: `PionEN`=0 w najbliższym cyklu |
| Oś Y w dół             | `PionEN`=1, `PionKierunek`=1 (dopóki wciśnięta strzałka ↓); puszczenie: `PionEN`=0 w najbliższym cyklu |

Dodatkowe wymagania po stronie PLC:

- Tryb `PozMode` = 8 musi wykonywać **skok o 10 cm** (1000 jednostek) na każde zbocze `PozJog1` (w lewo) / `PozJog2` (w prawo).
  Aplikacja nie wysyła długości skoku.
- Pozycja 0 = lewa skrajna pozycja osi X, 8000 = prawa (80,00 cm).
- Aplikacja uznaje ruch osi X za trwający, dopóki `PozPositionAct` się zmienia; wykresy startują wraz z pierwszą zmianą
  pozycji i zatrzymują się po ok. 0,3 s bez zmiany.
- Zalecane: watchdog po stronie PLC zerujący `PionEN` przy zaniku komunikacji oraz krańcówki/blokady niezależne od aplikacji.

## 4. Wzory używane przez aplikację (oś Y)

`n` = `PionSpeedOut` [obr/min], `r` = 0,03 m (ramię), `m` = masa obciążenia [kg], `M₀` = offset momentu [Nm], `g` = 9,81 m/s².

| Wielkość            | Wzór |
|---------------------|------|
| Prędkość kątowa     | `ω = n · π / 30` [rad/s] |
| Prędkość liniowa    | `v = ω · r` [m/s] |
| Moment statyczny    | `M = m · g · r + M₀` [Nm] |
| Moc mechaniczna     | `P = M · ω` [W] (ujemna przy opuszczaniu) |
| Energia potencjalna | `Ep = m · g · h`, `h = ∫ v dt` liczone od początku danego ruchu [J] |
