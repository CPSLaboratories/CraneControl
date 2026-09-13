using System.Net.Sockets;
using CraneControl.App.Models;
using NModbus;

namespace CraneControl.App.Services;

/// <summary>Rzeczywiste połączenie Modbus TCP/IP ze sterownikiem PLC (biblioteka NModbus).</summary>
public sealed class ModbusPlcClient : IPlcClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly byte _slaveId;
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;

    public ModbusPlcClient(string host, int port, byte slaveId = 1)
    {
        _host = host;
        _port = port;
        _slaveId = slaveId;
    }

    public bool IsConnected => _tcpClient?.Connected == true;

    public async Task ConnectAsync(CancellationToken ct)
    {
        _tcpClient?.Dispose();
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_host, _port, ct);
        _master = new ModbusFactory().CreateMaster(_tcpClient);
    }

    public async Task<CraneState> ReadStateAsync(CancellationToken ct)
    {
        if (_master is null)
        {
            throw new InvalidOperationException("Klient Modbus nie jest połączony.");
        }

        var regs = await _master.ReadHoldingRegistersAsync(
            _slaveId, PlcRegisterMap.ReadBlockStart, PlcRegisterMap.ReadBlockLength);

        var status = regs[PlcRegisterMap.StatusWord - PlcRegisterMap.ReadBlockStart];

        return new CraneState
        {
            PositionXmm = regs[PlcRegisterMap.PositionX - PlcRegisterMap.ReadBlockStart] / PlcRegisterMap.PositionScale,
            PositionYmm = regs[PlcRegisterMap.PositionY - PlcRegisterMap.ReadBlockStart] / PlcRegisterMap.PositionScale,
            Ready = IsBitSet(status, StatusBits.Ready),
            Fault = IsBitSet(status, StatusBits.Fault),
            MovingX = IsBitSet(status, StatusBits.MovingX),
            MovingY = IsBitSet(status, StatusBits.MovingY),
            LimitXMinus = IsBitSet(status, StatusBits.LimitXMinus),
            LimitXPlus = IsBitSet(status, StatusBits.LimitXPlus),
            LimitYMinus = IsBitSet(status, StatusBits.LimitYMinus),
            LimitYPlus = IsBitSet(status, StatusBits.LimitYPlus),
            AutoModeActive = IsBitSet(status, StatusBits.AutoModeActive),
            InPosition = IsBitSet(status, StatusBits.InPosition),
            FaultCode = regs[PlcRegisterMap.FaultCode - PlcRegisterMap.ReadBlockStart],
        };
    }

    public async Task WriteOutputsAsync(PlcOutputs outputs, CancellationToken ct)
    {
        if (_master is null)
        {
            throw new InvalidOperationException("Klient Modbus nie jest połączony.");
        }

        ushort command = 0;
        SetBit(ref command, CommandBits.AutoMode, outputs.AutoMode);
        SetBit(ref command, CommandBits.Stop, outputs.Stop);
        SetBit(ref command, CommandBits.StartMove, outputs.StartMove);
        SetBit(ref command, CommandBits.Enable, outputs.Enable);

        var values = new ushort[PlcRegisterMap.WriteBlockLength];
        values[PlcRegisterMap.CommandWord - PlcRegisterMap.WriteBlockStart] = command;
        values[PlcRegisterMap.JogX - PlcRegisterMap.WriteBlockStart] = unchecked((ushort)outputs.JogX);
        values[PlcRegisterMap.JogY - PlcRegisterMap.WriteBlockStart] = unchecked((ushort)outputs.JogY);
        values[PlcRegisterMap.SpeedX - PlcRegisterMap.WriteBlockStart] = outputs.SpeedX;
        values[PlcRegisterMap.SpeedY - PlcRegisterMap.WriteBlockStart] = outputs.SpeedY;
        values[PlcRegisterMap.TargetX - PlcRegisterMap.WriteBlockStart] = (ushort)Math.Round(outputs.TargetXmm * PlcRegisterMap.PositionScale);
        values[PlcRegisterMap.TargetY - PlcRegisterMap.WriteBlockStart] = (ushort)Math.Round(outputs.TargetYmm * PlcRegisterMap.PositionScale);

        await _master.WriteMultipleRegistersAsync(_slaveId, PlcRegisterMap.WriteBlockStart, values);
    }

    private static bool IsBitSet(ushort value, int bit) => (value & (1 << bit)) != 0;

    private static void SetBit(ref ushort value, int bit, bool set)
    {
        if (set)
        {
            value |= (ushort)(1 << bit);
        }
    }

    public ValueTask DisposeAsync()
    {
        _tcpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
