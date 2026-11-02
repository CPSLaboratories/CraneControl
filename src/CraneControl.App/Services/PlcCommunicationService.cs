using System.Diagnostics;
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

    /// <summary>Źródło wyjść wywoływane raz w każdym cyklu tuż przed zapisem do PLC.</summary>
    public Func<PlcOutputs> OutputsProvider { get; set; } = () => PlcOutputs.Default;

    /// <summary>Stan oraz czas jego pobrania w sekundach (zegar Stopwatch, środek transakcji odczytu).</summary>
    public event Action<CraneState, double>? StateUpdated;

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
        var periodTicks = (long)(_period.TotalSeconds * Stopwatch.Frequency);
        var nextCycle = Stopwatch.GetTimestamp();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = GetClient();
                var requestStart = Stopwatch.GetTimestamp();
                var state = await client.ReadStateAsync(ct);
                var requestEnd = Stopwatch.GetTimestamp();

                // PLC próbkuje rejestr w trakcie transakcji, a nie w chwili nadejścia odpowiedzi.
                var sampleTime = (requestStart + ((requestEnd - requestStart) / 2)) / (double)Stopwatch.Frequency;
                StateUpdated?.Invoke(state, sampleTime);
                await client.WriteOutputsAsync(OutputsProvider(), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                CommunicationError?.Invoke(ex);
            }

            // Stały rytm bez nadrabiania spóźnionych cykli (seria odczytów jeden po drugim psułaby różniczkowanie).
            nextCycle += periodTicks;
            var now = Stopwatch.GetTimestamp();
            if (nextCycle <= now)
            {
                nextCycle = now;
                continue;
            }

            try
            {
                await Task.Delay(Stopwatch.GetElapsedTime(now, nextCycle), ct);
            }
            catch (OperationCanceledException)
            {
                break;
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

