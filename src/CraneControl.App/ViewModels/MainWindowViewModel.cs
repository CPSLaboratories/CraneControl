using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraneControl.App.Models;
using CraneControl.App.Services;

namespace CraneControl.App.ViewModels;

/// <summary>Główny model widoku - spina komunikację z PLC oraz zakładki osi X i Y.</summary>
public partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly TimeSpan CyclePeriod = TimeSpan.FromMilliseconds(10);

    private readonly PlcCommunicationService _plc;
    private readonly PlcCommandSequencer _commands = new(CyclePeriod);
    private IPlcClient? _connectedRealClient;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string connectionStatusIcon = "✕";

    [ObservableProperty]
    private string statusMessage = "Łączenie...";

    [ObservableProperty]
    private string plcHost = "192.168.0.15";

    [ObservableProperty]
    private int plcPort = 502;

    [ObservableProperty]
    private bool isConnectingToPlc;

    [ObservableProperty]
    private bool isRealPlcAvailable;

    [ObservableProperty]
    private bool isRealPlcMode;

    [ObservableProperty]
    private string connectionMessage = "Tryb symulacji - podaj adres sterownika i połącz, aby przełączyć na rzeczywisty PLC.";

    public MainWindowViewModel()
    {
        AxisX = new AxisXViewModel(_commands);
        AxisY = new AxisYViewModel(_commands);

        _plc = new PlcCommunicationService(new SimulatedPlcClient(), CyclePeriod)
        {
            OutputsProvider = _commands.Next,
        };
        _plc.StateUpdated += OnStateUpdated;
        _plc.CommunicationError += OnCommunicationError;
    }

    public AxisXViewModel AxisX { get; }

    public AxisYViewModel AxisY { get; }

    public async Task StartAsync()
    {
        try
        {
            _commands.RestartPozEnable();
            await _plc.StartAsync();
            IsConnected = IsRealPlcAvailable;
            ConnectionStatusIcon = IsConnected ? "✓" : "✕";
            StatusMessage = IsConnected ? "Połączenie aktywne" : "Brak połączenia";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatusIcon = "✕";
            StatusMessage = "Brak połączenia";
            ConnectionMessage = $"Błąd połączenia: {ex.Message}";
        }
    }

    /// <summary>Próbuje nawiązać połączenie Modbus TCP z podanym adresem, nie zmieniając jeszcze aktywnego trybu.</summary>
    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsConnectingToPlc = true;
        IsRealPlcAvailable = false;
        ConnectionMessage = $"Łączenie z {PlcHost}:{PlcPort}...";

        try
        {
            var client = new ModbusPlcClient(PlcHost, PlcPort);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(timeout.Token);

            if (_connectedRealClient is not null)
            {
                await _connectedRealClient.DisposeAsync();
            }

            _connectedRealClient = client;
            IsRealPlcAvailable = true;
            if (IsRealPlcMode)
            {
                await SwitchActiveClientAsync(true);
            }
            else
            {
                ConnectionMessage = $"Połączono z {PlcHost}:{PlcPort}. Możesz przełączyć tryb źródła danych na rzeczywisty PLC.";
            }
        }
        catch (Exception ex)
        {
            IsRealPlcAvailable = false;
            IsConnected = false;
            ConnectionStatusIcon = "✕";
            StatusMessage = "Brak połączenia";
            ConnectionMessage = $"Nie udało się połączyć z {PlcHost}:{PlcPort}: {ex.Message}";
        }
        finally
        {
            IsConnectingToPlc = false;
        }
    }

    partial void OnIsRealPlcModeChanged(bool value)
    {
        if (value && !IsRealPlcAvailable)
        {
            IsConnected = false;
            ConnectionStatusIcon = "✕";
            StatusMessage = "Brak połączenia";
            ConnectionMessage = "Tryb PLC wybrany. Połącz się ze sterownikiem, aby rozpocząć wymianę danych.";
            return;
        }

        IsConnected = IsRealPlcAvailable;
        ConnectionStatusIcon = IsConnected ? "✓" : "✕";
        StatusMessage = IsConnected ? "Połączenie aktywne" : "Brak połączenia";
        _ = SwitchActiveClientAsync(value);
    }

    private async Task SwitchActiveClientAsync(bool useReal)
    {
        try
        {
            if (useReal && _connectedRealClient is not null)
            {
                await _plc.SwitchClientAsync(_connectedRealClient);
                ConnectionMessage = $"Aktywne źródło danych: rzeczywisty PLC ({PlcHost}:{PlcPort}).";
            }
            else
            {
                var simulated = new SimulatedPlcClient();
                await simulated.ConnectAsync(CancellationToken.None);
                await _plc.SwitchClientAsync(simulated);
                ConnectionMessage = "Aktywne źródło danych: symulacja.";
            }

            _commands.RestartPozEnable();
            AxisY.ContinuousRecording = useReal && _connectedRealClient is not null;
        }
        catch (Exception ex)
        {
            ConnectionMessage = $"Błąd przełączania źródła danych: {ex.Message}";
        }
    }

    partial void OnSelectedTabIndexChanged(int value) => ReleaseAllKeys();

    /// <summary>Zwraca true, gdy klawisz został obsłużony jako sterowanie aktywnej zakładki.</summary>
    public bool OnKeyDown(Key key)
    {
        switch (SelectedTabIndex, key)
        {
            case (0, Key.Left):
                AxisX.OnJogKeyDown(right: false);
                return true;
            case (0, Key.Right):
                AxisX.OnJogKeyDown(right: true);
                return true;
            case (1, Key.Up):
                AxisY.SetUpHeld(true);
                return true;
            case (1, Key.Down):
                AxisY.SetDownHeld(true);
                return true;
            default:
                return false;
        }
    }

    public void OnKeyUp(Key key)
    {
        switch (key)
        {
            case Key.Left: AxisX.OnJogKeyUp(right: false); break;
            case Key.Right: AxisX.OnJogKeyUp(right: true); break;
            case Key.Up: AxisY.SetUpHeld(false); break;
            case Key.Down: AxisY.SetDownHeld(false); break;
        }
    }

    /// <summary>Zwalnia wszystkie klawisze (zmiana zakładki, utrata fokusu okna) - oś Y natychmiast się zatrzymuje.</summary>
    public void ReleaseAllKeys()
    {
        AxisX.ReleaseKeys();
        AxisY.ReleaseKeys();
    }

    // Czas pochodzi z chwili odczytu w pętli komunikacji (nie z kolejki UI), żeby jitter dispatchera nie zniekształcał prędkości.
    private void OnStateUpdated(CraneState state, double sampleTime)
    {
        _commands.ReportPosition(PlcScaling.PositionToRaw(state.PositionXcm));
        Dispatcher.UIThread.Post(() => ApplyState(state, sampleTime));
    }

    private void ApplyState(CraneState state, double elapsed)
    {
        AxisX.ApplyState(state, elapsed);
        AxisY.ApplyState(state, elapsed);

        IsConnected = IsRealPlcAvailable;
        ConnectionStatusIcon = IsConnected ? "✓" : "✕";
        StatusMessage = IsConnected ? "Połączenie aktywne" : "Brak połączenia";
    }

    private void OnCommunicationError(Exception ex) => Dispatcher.UIThread.Post(() =>
    {
        IsConnected = false;
        ConnectionStatusIcon = "✕";
        IsRealPlcAvailable = false;
        StatusMessage = "Brak połączenia";
        ConnectionMessage = $"Błąd komunikacji: {ex.Message}";
    });

    public async ValueTask DisposeAsync()
    {
        await _plc.DisposeAsync();
        if (_connectedRealClient is not null)
        {
            await _connectedRealClient.DisposeAsync();
        }
    }
}
