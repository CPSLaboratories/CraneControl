using CraneControl.App.Models;

namespace CraneControl.App.Services;

/// <summary>
/// Cyklicznie odczytuje stan z PLC i zapisuje bieżące wyjścia (jeden "skan" na okres).
/// Odseparowuje resztę aplikacji od szczegółów protokołu Modbus.
/// </summary>
public sealed class PlcCommunicationService : IAsyncDisposable
{
    private readonly object _clientLock = new();
    private readonly TimeSpan _period;
    private IPlcClient _client;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public PlcCommunicationService(IPlcClient client, TimeSpan period)
    {
        _client = client;
        _period = period;
    }

    /// <summary>Wyjścia zapisywane w każdym cyklu; aktualizowane przez warstwę UI.</summary>
    public PlcOutputs Outputs { get; set; } = PlcOutputs.Default;

    public event Action<CraneState>? StateUpdated;

    public event Action<Exception>? CommunicationError;

    public async Task StartAsync()
    {
        await GetClient().ConnectAsync(CancellationToken.None);
        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Podmienia aktywnego klienta "na gorąco" (np. przełączenie symulacja/rzeczywisty PLC).
    /// Nowy klient musi być już połączony; poprzedni zostaje rozłączony.
    /// </summary>
    public async ValueTask SwitchClientAsync(IPlcClient newClient)
    {
        IPlcClient previous;
        lock (_clientLock)
        {
            previous = _client;
            _client = newClient;
        }

        await previous.DisposeAsync();
    }

    private IPlcClient GetClient()
    {
        lock (_clientLock)
        {
            return _client;
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_period);
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                var client = GetClient();
                var state = await client.ReadStateAsync(ct);
                StateUpdated?.Invoke(state);
                await client.WriteOutputsAsync(Outputs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                CommunicationError?.Invoke(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await GetClient().DisposeAsync();
    }
}

