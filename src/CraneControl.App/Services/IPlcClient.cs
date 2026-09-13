using CraneControl.App.Models;

namespace CraneControl.App.Services;

/// <summary>Abstrakcja komunikacji ze sterownikiem dźwignicy (realny PLC lub symulator).</summary>
public interface IPlcClient : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct);

    Task<CraneState> ReadStateAsync(CancellationToken ct);

    Task WriteOutputsAsync(PlcOutputs outputs, CancellationToken ct);
}
