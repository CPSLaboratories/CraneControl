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

        ushort Reg(ushort address) => regs[address - PlcRegisterMap.ReadBlockStart];

        return new CraneState
        {
            PositionXcm = PlcScaling.RawToPositionCm(Reg(PlcRegisterMap.PozPositionAct)),
            AxisXEnabled = Reg(PlcRegisterMap.PozAxisEnabled) != 0,
            AxisXReferenced = Reg(PlcRegisterMap.PozAxisRef) != 0,
            SpeedYRpm = PlcScaling.RawToPionSpeedOutRpm(unchecked((short)Reg(PlcRegisterMap.PionSpeedOut))),
        };
    }

    public async Task WriteOutputsAsync(PlcOutputs outputs, CancellationToken ct)
    {
        if (_master is null)
        {
            throw new InvalidOperationException("Klient Modbus nie jest połączony.");
        }

        var values = new ushort[PlcRegisterMap.WriteBlockLength];

        void Set(ushort address, ushort value) => values[address - PlcRegisterMap.WriteBlockStart] = value;
        static ushort Flag(bool value) => value ? (ushort)1 : (ushort)0;

        Set(PlcRegisterMap.PozEN, Flag(outputs.PozEnable));
        Set(PlcRegisterMap.PozMode, outputs.PozMode);
        Set(PlcRegisterMap.PozJog1, Flag(outputs.PozJog1));
        Set(PlcRegisterMap.PozJog2, Flag(outputs.PozJog2));
        Set(PlcRegisterMap.PozAckError, Flag(outputs.PozAckError));
        Set(PlcRegisterMap.PozExecute, Flag(outputs.PozExecute));
        Set(PlcRegisterMap.PozPositionSet, outputs.PozPosition);
        Set(PlcRegisterMap.PozVelocity, outputs.PozVelocity);
        Set(PlcRegisterMap.OverAcc, outputs.OverAcc);
        Set(PlcRegisterMap.OverDec, outputs.OverDec);
        Set(PlcRegisterMap.PionEN, Flag(outputs.PionEnable));
        Set(PlcRegisterMap.PionSpeedSet, outputs.PionSpeed);
        Set(PlcRegisterMap.PionDirection, Flag(outputs.PionDirectionDown));
        Set(PlcRegisterMap.PionAckError, Flag(outputs.PionAckError));

        await _master.WriteMultipleRegistersAsync(_slaveId, PlcRegisterMap.WriteBlockStart, values);
    }

    public ValueTask DisposeAsync()
    {
        _tcpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
