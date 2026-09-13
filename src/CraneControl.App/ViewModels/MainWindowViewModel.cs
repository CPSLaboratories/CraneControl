using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraneControl.App.Models;
using CraneControl.App.Services;

namespace CraneControl.App.ViewModels;

/// <summary>Główny model widoku - spina komunikację z PLC, wizualizację i wykresy obu osi.</summary>
public partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly PlcCommunicationService _plc;
    private readonly AxisMotionTracker _trackerX = new();
    private readonly AxisMotionTracker _trackerY = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private bool _wasMovingX;
    private bool _wasMovingY;
    private sbyte _jogX;
    private sbyte _jogY;
    private bool _pulseStartMove;
    private double _targetXmm;
    private double _targetYmm;
    private IPlcClient? _connectedRealClient;

    [ObservableProperty]
    private double speedX = 50;

    [ObservableProperty]
    private double speedY = 50;

    [ObservableProperty]
    private bool isAutoMode;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string connectionStatusIcon = "✕";

    [ObservableProperty]
    private string statusMessage = "Łączenie...";

    [ObservableProperty]
    private bool isAutoModeConfirmedByPlc;

    [ObservableProperty]
    private string plcHost = "192.168.0.61";

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
        var initialClient = new SimulatedPlcClient(Visualization.TravelXmm, Visualization.TravelYmm);
        _plc = new PlcCommunicationService(initialClient, TimeSpan.FromMilliseconds(100));
        _plc.StateUpdated += OnStateUpdated;
        _plc.CommunicationError += OnCommunicationError;
    }

    public CraneVisualizationViewModel Visualization { get; } = new();

    public AxisChartGroupViewModel ChartsX { get; } = new("X", 0xFF3DA5FF, 0xFFFFB020, 0xFF34D399);

    public AxisChartGroupViewModel ChartsY { get; } = new("Y", 0xFFB37FEB, 0xFFFF6B6B, 0xFF34D399);

    public string CurrentModeLabel => IsAutoMode ? "Sterowanie automatyczne aktywne" : "Sterowanie ręczne aktywne";

    public async Task StartAsync()
    {
        try
        {
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
                var simulated = new SimulatedPlcClient(Visualization.TravelXmm, Visualization.TravelYmm);
                await simulated.ConnectAsync(CancellationToken.None);
                await _plc.SwitchClientAsync(simulated);
                ConnectionMessage = "Aktywne źródło danych: symulacja.";
            }
        }
        catch (Exception ex)
        {
            ConnectionMessage = $"Błąd przełączania źródła danych: {ex.Message}";
        }
    }

    partial void OnSpeedXChanged(double value) => PushOutputs();

    partial void OnSpeedYChanged(double value) => PushOutputs();

    partial void OnIsAutoModeChanged(bool value)
    {
        Visualization.IsAutoMode = value;
        Visualization.HasTarget = false;
        _targetXmm = Visualization.PositionXmm;
        _targetYmm = Visualization.PositionYmm;
        _jogX = 0;
        _jogY = 0;
        OnPropertyChanged(nameof(CurrentModeLabel));
        PushOutputs();
    }

    /// <summary>Obsługa strzałek w trybie ręcznym (nacisk = jazda, puszczenie = stop).</summary>
    public void OnArrowKeyDown(Key key)
    {
        if (IsAutoMode)
        {
            return;
        }

        switch (key)
        {
            case Key.Left: _jogX = -100; break;
            case Key.Right: _jogX = 100; break;
            case Key.Up: _jogY = -100; break;
            case Key.Down: _jogY = 100; break;
            default: return;
        }

        PushOutputs();
    }

    public void OnArrowKeyUp(Key key)
    {
        if (IsAutoMode)
        {
            return;
        }

        switch (key)
        {
            case Key.Left or Key.Right: _jogX = 0; break;
            case Key.Up or Key.Down: _jogY = 0; break;
            default: return;
        }

        PushOutputs();
    }

    /// <summary>Kliknięcie na wizualizacji w trybie automatycznym zadaje nową pozycję docelową.</summary>
    public void OnVisualizationClicked(double normX, double normY)
    {
        if (!IsAutoMode)
        {
            return;
        }

        var (xmm, ymm) = Visualization.NormalizedClickToMillimeters(normX, normY);
        _targetXmm = xmm;
        _targetYmm = ymm;
        Visualization.TargetXmm = xmm;
        Visualization.TargetYmm = ymm;
        Visualization.HasTarget = true;
        _pulseStartMove = true;
        PushOutputs();
    }

    private void PushOutputs()
    {
        _plc.Outputs = new PlcOutputs
        {
            AutoMode = IsAutoMode,
            Enable = true,
            JogX = IsAutoMode ? (sbyte)0 : _jogX,
            JogY = IsAutoMode ? (sbyte)0 : _jogY,
            SpeedX = (byte)SpeedX,
            SpeedY = (byte)SpeedY,
            TargetXmm = _targetXmm,
            TargetYmm = _targetYmm,
            StartMove = _pulseStartMove,
        };
        _pulseStartMove = false;
    }

    private void OnStateUpdated(CraneState state) => Dispatcher.UIThread.Post(() => ApplyState(state));

    private void ApplyState(CraneState state)
    {
        var now = DateTime.UtcNow;
        var elapsed = _clock.Elapsed.TotalSeconds;

        Visualization.PositionXmm = state.PositionXmm;
        Visualization.PositionYmm = state.PositionYmm;
        _trackerX.Update(state.PositionXmm, now);
        _trackerY.Update(state.PositionYmm, now);

        UpdateAxisChart(ChartsX, _trackerX, elapsed, state.MovingX, IsAutoMode, ref _wasMovingX);
        UpdateAxisChart(ChartsY, _trackerY, elapsed, state.MovingY, IsAutoMode, ref _wasMovingY);

        IsAutoModeConfirmedByPlc = state.AutoModeActive;
        IsConnected = IsRealPlcAvailable;
        ConnectionStatusIcon = IsConnected ? "✓" : "✕";
        StatusMessage = IsConnected ? "Połączenie aktywne" : "Brak połączenia";
    }

    private static void UpdateAxisChart(
        AxisChartGroupViewModel charts, AxisMotionTracker tracker, double elapsed, bool isMoving, bool isAutoMode, ref bool wasMoving)
    {
        // Tryb ręczny: bufor kołowy 15 s (licznik drogi rośnie stale). Tryb automatyczny:
        // licznik i wykresy startują od zera przy każdym nowym ruchu i zamrażają się po jego końcu.
        if (!isAutoMode)
        {
            charts.AddSample(elapsed, tracker.VelocityMmPerS, tracker.AccelerationMmPerS2, tracker.DistanceTraveledMm, trimToWindow: true);
        }
        else
        {
            if (isMoving && !wasMoving)
            {
                tracker.Reset();
                charts.ClearAndRestart();
            }

            if (isMoving)
            {
                charts.AddSample(elapsed, tracker.VelocityMmPerS, tracker.AccelerationMmPerS2, tracker.DistanceTraveledMm, trimToWindow: false);
            }
        }

        wasMoving = isMoving;
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
