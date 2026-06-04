using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class ERackSerialHardwareGateway : IHardwareGateway, IDisposable
{
    private readonly ERackHardwareOptions _options;
    private readonly ILogger<ERackSerialHardwareGateway> _logger;
    private readonly object _syncRoot = new();
    private SerialPort? _serialPort;
    private bool _disposed;

    public ERackSerialHardwareGateway(
        IOptions<ERackHardwareOptions> options,
        ILogger<ERackSerialHardwareGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public ERackPortStatus GetPortStatus()
    {
        lock (_syncRoot)
        {
            var isOpen = _serialPort?.IsOpen == true;
            var description = isOpen
                ? "COM port is open"
                : "COM port is closed";

            return new ERackPortStatus(
                _options.Enabled,
                _options.PortName,
                _options.BaudRate,
                _options.KeepPortOpen,
                isOpen,
                description);
        }
    }

    public OperationResult OpenPort()
    {
        lock (_syncRoot)
        {
            try
            {
                EnsureSerialPortOpen(_options.InventoryWaitTimeMilliseconds);
                return OperationResult.Ok("COM port opened");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to open ERack COM port: port={PortName}, baudRate={BaudRate}",
                    _options.PortName,
                    _options.BaudRate);

                return OperationResult.Fail(7, ex.Message);
            }
        }
    }

    public void ClosePort()
    {
        try
        {
            lock (_syncRoot)
            {
                ClosePortLocked();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to close ERack COM port cleanly: port={PortName}",
                _options.PortName);
        }
    }

    public Task<OperationResult> WriteTagAsync(TagWriteCommand command, CancellationToken cancellationToken)
    {
        var tag = command.Tag.Trim();
        if (tag.Length % 8 != 0 || tag.Length < 8 || tag.Length > 32)
        {
            return Task.FromResult(OperationResult.Fail(2, "tag length must be 8/16/24/32 characters"));
        }

        if (tag.Any(character => character > 0x7F || char.IsControl(character)))
        {
            return Task.FromResult(OperationResult.Fail(3, "tag must contain printable ASCII characters only"));
        }

        return Task.Run(() =>
        {
            try
            {
                WriteTag(command, tag, cancellationToken);

                _logger.LogInformation(
                    "ERack RFID write completed: shelf={ShelfId}, location={LocationId}, tag={Tag}",
                    command.ShelfId,
                    command.LocationId,
                    tag);

                return OperationResult.Ok("tag written");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ERack RFID write failed: port={PortName}, baudRate={BaudRate}, address={Address}, tag={Tag}",
                    _options.PortName,
                    _options.BaudRate,
                    _options.DeviceAddress,
                    tag);

                return OperationResult.Fail(7, ex.Message);
            }
        }, cancellationToken);
    }

    public Task<ShelfStatusResult> QueryShelfStatusAsync(ShelfStatusQuery query, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var tag = ReadInventoryTag(cancellationToken);
                var shelfId = string.IsNullOrWhiteSpace(query.ShelfId) ? _options.DefaultShelfId : query.ShelfId;
                var locationId = query.LocationId.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? _options.DefaultLocationId
                    : query.LocationId;

                _logger.LogInformation(
                    "ERack RFID inventory completed: shelf={ShelfId}, location={LocationId}, tag={Tag}, loaded={Loaded}",
                    shelfId,
                    locationId,
                    tag,
                    !string.IsNullOrWhiteSpace(tag));

                return ShelfStatusResult.Ok(
                    shelfId,
                    new[] { new ShelfLocationStatus(locationId, tag, !string.IsNullOrWhiteSpace(tag)) });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ERack RFID inventory failed: port={PortName}, baudRate={BaudRate}, address={Address}",
                    _options.PortName,
                    _options.BaudRate,
                    _options.DeviceAddress);

                return ShelfStatusResult.Fail(
                    string.IsNullOrWhiteSpace(query.ShelfId) ? _options.DefaultShelfId : query.ShelfId,
                    7,
                    ex.Message);
            }
        }, cancellationToken);
    }

    private string ReadInventoryTag(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var serialPort = EnsureSerialPortOpen(_options.InventoryWaitTimeMilliseconds);
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                var request = ERackProtocol.BuildRequest(
                    _options.DeviceAddress,
                    ERackCommand.InventoryWithResponse,
                    new[] { _options.InventoryMode });

                _logger.LogInformation(
                    "Sending ERack RFID inventory request: port={PortName}, baudRate={BaudRate}, address={Address}, inventoryMode={InventoryMode}",
                    _options.PortName,
                    _options.BaudRate,
                    _options.DeviceAddress,
                    _options.InventoryMode);

                serialPort.Write(request, 0, request.Length);

                var response = ReadResponse(
                    serialPort,
                    _options.DeviceAddress,
                    ERackCommand.InventoryWithResponse,
                    _options.InventoryWaitTimeMilliseconds,
                    _options.InventoryWaitCount,
                    cancellationToken);

                if (response.Payload.Length == 1)
                {
                    throw new InvalidOperationException($"RFID reader returned error code {response.Payload[0]}");
                }

                if (response.Payload.Length < 32)
                {
                    throw new InvalidOperationException($"RFID response payload is too short: {response.Payload.Length}");
                }

                return ERackTagDecoder.DecodeInventoryTag(response.Payload);
            }
            finally
            {
                ClosePortAfterOperationIfNeeded();
            }
        }
    }

    private void WriteTag(TagWriteCommand command, string tag, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var serialPort = EnsureSerialPortOpen(_options.WriteTagWaitTimeMilliseconds);
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                var tagBytes = ERackTagDecoder.EncodeWriteTag(tag);
                var payload = new byte[tagBytes.Length + 1];
                payload[0] = _options.WriteTagStartPage;
                tagBytes.CopyTo(payload.AsSpan(1));
                var hardwareTag = Encoding.ASCII.GetString(tagBytes);

                var request = ERackProtocol.BuildRequest(
                    _options.DeviceAddress,
                    ERackCommand.WriteTagWithResponse,
                    payload);

                _logger.LogInformation(
                    "Sending ERack RFID write request: port={PortName}, baudRate={BaudRate}, address={Address}, startPage={StartPage}, shelf={ShelfId}, location={LocationId}, tag={Tag}, hardwareTag={HardwareTag}",
                    _options.PortName,
                    _options.BaudRate,
                    _options.DeviceAddress,
                    _options.WriteTagStartPage,
                    command.ShelfId,
                    command.LocationId,
                    tag,
                    hardwareTag);

                serialPort.Write(request, 0, request.Length);

                var response = ReadResponse(
                    serialPort,
                    _options.DeviceAddress,
                    ERackCommand.WriteTagWithResponse,
                    _options.WriteTagWaitTimeMilliseconds,
                    _options.WriteTagWaitCount,
                    cancellationToken);

                if (response.Payload.Length != 1)
                {
                    throw new InvalidOperationException($"RFID write response payload length is invalid: {response.Payload.Length}");
                }

                if (response.Payload[0] != ERackCommand.Success)
                {
                    throw new InvalidOperationException($"RFID writer returned error code {response.Payload[0]}");
                }
            }
            finally
            {
                ClosePortAfterOperationIfNeeded();
            }
        }
    }

    private SerialPort EnsureSerialPortOpen(int readTimeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_serialPort is { IsOpen: true })
        {
            _serialPort.ReadTimeout = readTimeout;
            _serialPort.WriteTimeout = 1000;
            return _serialPort;
        }

        ClosePortLocked();

        _serialPort = new SerialPort(_options.PortName, _options.BaudRate)
        {
            ReadTimeout = readTimeout,
            WriteTimeout = 1000
        };

        _serialPort.Open();
        _logger.LogInformation(
            "ERack COM port opened: port={PortName}, baudRate={BaudRate}, keepPortOpen={KeepPortOpen}",
            _options.PortName,
            _options.BaudRate,
            _options.KeepPortOpen);

        return _serialPort;
    }

    private void ClosePortAfterOperationIfNeeded()
    {
        if (!_options.KeepPortOpen)
        {
            ClosePortLocked();
        }
    }

    private void ClosePortLocked()
    {
        var serialPort = _serialPort;
        if (serialPort is null)
        {
            return;
        }

        _serialPort = null;

        try
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to close ERack COM port: port={PortName}",
                _options.PortName);
        }
        finally
        {
            try
            {
                serialPort.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to dispose ERack COM port: port={PortName}",
                    _options.PortName);
            }

            _logger.LogInformation("ERack COM port closed: port={PortName}", _options.PortName);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            ClosePortLocked();
            _disposed = true;
        }
    }

    private static ERackFrame ReadResponse(
        SerialPort serialPort,
        byte address,
        byte command,
        int waitTimeMilliseconds,
        int waitCount,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var length = 0;

        Thread.Sleep(waitTimeMilliseconds);
        for (var attempt = 0; attempt < waitCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var remaining = buffer.Length - length;
                if (remaining <= 0)
                {
                    throw new InvalidOperationException("RFID response buffer is full");
                }

                length += serialPort.Read(buffer, length, remaining);
            }
            catch (TimeoutException)
            {
            }

            if (TryFindResponse(buffer.AsSpan(0, length), address, command, out var response))
            {
                return response;
            }
        }

        throw new TimeoutException($"RFID response timeout after {waitCount} attempts");
    }

    private static bool TryFindResponse(
        ReadOnlySpan<byte> buffer,
        byte address,
        byte command,
        out ERackFrame response)
    {
        for (var index = 0; index < buffer.Length; index++)
        {
            if (!ERackProtocol.TryParseResponse(buffer[index..], out response))
            {
                continue;
            }

            if (response.Command == command && response.Address == address)
            {
                return true;
            }
        }

        response = new ERackFrame(0, 0, Array.Empty<byte>());
        return false;
    }
}
