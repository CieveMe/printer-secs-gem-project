using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class ERackSerialHardwareGateway : IHardwareGateway, IDisposable
{
    private readonly ERackHardwareOptions _fallbackOptions;
    private readonly ERackLocationRegistry _locations;
    private readonly ILogger<ERackSerialHardwareGateway> _logger;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, SerialPort> _serialPorts = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private bool _shutdownRequested;

    public ERackSerialHardwareGateway(
        IOptions<ERackHardwareOptions> options,
        ERackLocationRegistry locations,
        ILogger<ERackSerialHardwareGateway> logger)
    {
        _fallbackOptions = options.Value;
        _locations = locations;
        _logger = logger;
    }

    public ERackPortStatus GetPortStatus()
    {
        lock (_syncRoot)
        {
            var location = _locations.DefaultLocation;
            var isOpen = _serialPorts.TryGetValue(GetPortKey(location), out var serialPort) &&
                serialPort.IsOpen;
            var description = isOpen
                ? "COM port is open"
                : "COM port is closed";

            return new ERackPortStatus(
                _fallbackOptions.Enabled,
                location.PortName,
                location.BaudRate,
                location.KeepPortOpen,
                isOpen,
                description);
        }
    }

    public OperationResult OpenPort()
    {
        lock (_syncRoot)
        {
            if (_shutdownRequested)
            {
                return OperationResult.Fail(7, "ERack COM port is shutting down");
            }

            var openedLocations = new List<string>();
            var failedLocations = new List<string>();
            foreach (var location in _locations.Locations)
            {
                try
                {
                    EnsureSerialPortOpen(location, location.InventoryWaitTimeMilliseconds);
                    openedLocations.Add(location.LocationId);
                }
                catch (Exception ex)
                {
                    failedLocations.Add($"{location.LocationId}({location.PortName}): {ex.Message}");
                    _logger.LogError(
                        ex,
                        "Failed to open ERack COM port for location: location={LocationId}, port={PortName}, baudRate={BaudRate}",
                        location.LocationId,
                        location.PortName,
                        location.BaudRate);
                }
            }

            if (openedLocations.Count == 0)
            {
                return OperationResult.Fail(7, $"no ERack COM ports opened; failed: {string.Join("; ", failedLocations)}");
            }

            var description = failedLocations.Count == 0
                ? $"COM port opened for {openedLocations.Count} location(s): {string.Join(", ", openedLocations)}"
                : $"COM port opened for {openedLocations.Count}/{_locations.Locations.Count} location(s): {string.Join(", ", openedLocations)}; failed: {string.Join("; ", failedLocations)}";

            return OperationResult.Ok(description);
        }
    }

    public void ClosePort()
    {
        try
        {
            lock (_syncRoot)
            {
                CloseAllPortsLocked();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to close ERack COM ports cleanly");
        }
    }

    public bool IsShutdownRequested
    {
        get
        {
            lock (_syncRoot)
            {
                return _shutdownRequested;
            }
        }
    }

    public void BeginShutdown()
    {
        try
        {
            lock (_syncRoot)
            {
                _shutdownRequested = true;
                CloseAllPortsLocked();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to shut down ERack COM ports cleanly");
        }
    }

    public Task<OperationResult> WriteTagAsync(TagWriteCommand command, CancellationToken cancellationToken)
    {
        var location = _locations.Find(command.ShelfId, command.LocationId);
        if (location is null)
        {
            return Task.FromResult(OperationResult.Fail(6, $"location not configured: {command.LocationId}"));
        }

        var tag = command.Tag.Trim();
        if (tag.Length < 1 || tag.Length > 32)
        {
            return Task.FromResult(OperationResult.Fail(2, "tag length must be 1-32 characters"));
        }

        if (tag.Any(character => character > 0x7F || char.IsControl(character)))
        {
            return Task.FromResult(OperationResult.Fail(3, "tag must contain printable ASCII characters only"));
        }

        return Task.Run(() =>
        {
            try
            {
                WriteTag(location, command, tag, cancellationToken);

                _logger.LogInformation(
                    "ERack RFID write completed: shelf={ShelfId}, location={LocationId}, tag={Tag}",
                    command.ShelfId,
                    location.LocationId,
                    tag);

                return OperationResult.Ok("tag written");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ERack RFID write failed: port={PortName}, baudRate={BaudRate}, address={Address}, location={LocationId}, tag={Tag}",
                    location.PortName,
                    location.BaudRate,
                    location.DeviceAddress,
                    location.LocationId,
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
            var shelfId = string.IsNullOrWhiteSpace(query.ShelfId) ? _fallbackOptions.DefaultShelfId : query.ShelfId;
            var locations = _locations.FindAll(shelfId, query.LocationId);
            if (locations.Count == 0)
            {
                return ShelfStatusResult.Fail(shelfId, 6, $"location not configured: {query.LocationId}");
            }

            try
            {
                var statuses = new List<ShelfLocationStatus>();
                foreach (var location in locations)
                {
                    var tag = ReadInventoryTag(location, query.ReadLengthBytes, cancellationToken);
                    statuses.Add(new ShelfLocationStatus(location.LocationId, tag, !string.IsNullOrWhiteSpace(tag)));

                    _logger.LogDebug(
                        "ERack RFID inventory completed: shelf={ShelfId}, location={LocationId}, tag={Tag}, loaded={Loaded}, readLength={ReadLength}",
                        location.ShelfId,
                        location.LocationId,
                        tag,
                        !string.IsNullOrWhiteSpace(tag),
                        NormalizeReadLength(query.ReadLengthBytes));
                }

                return ShelfStatusResult.Ok(shelfId, statuses);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ERack RFID inventory failed: shelf={ShelfId}, location={LocationId}",
                    shelfId,
                    query.LocationId);

                return ShelfStatusResult.Fail(shelfId, 7, ex.Message);
            }
        }, cancellationToken);
    }

    public Task<ERackSensorStateResult> ReadSensorStateAsync(
        ERackLocation location,
        byte sensorCommand,
        int sensorPayloadIndex,
        byte checkLevel,
        int waitTimeMilliseconds,
        int waitCount,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_syncRoot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var serialPort = EnsureSerialPortOpen(location, waitTimeMilliseconds);
                    try
                    {
                        var response = SendCommandWithResponseLocked(
                            location,
                            serialPort,
                            sensorCommand,
                            Array.Empty<byte>(),
                            waitTimeMilliseconds,
                            waitCount,
                            "sensor",
                            cancellationToken);

                        if (response.Payload.Length == 1)
                        {
                            return ERackSensorStateResult.Fail(
                                location,
                                sensorCommand,
                                response.Payload[0],
                                response.Payload[0] == ERackCommand.Success
                                    ? "sensor response did not include state bytes"
                                    : $"sensor returned error code {response.Payload[0]} (0x{response.Payload[0]:X2})");
                        }

                        if (response.Payload.Length < 4)
                        {
                            return ERackSensorStateResult.Fail(
                                location,
                                sensorCommand,
                                7,
                                $"sensor response payload is too short: {response.Payload.Length}");
                        }

                        if (sensorPayloadIndex < 0 || sensorPayloadIndex >= response.Payload.Length)
                        {
                            return ERackSensorStateResult.Fail(
                                location,
                                sensorCommand,
                                7,
                                $"sensor payload index is out of range: {sensorPayloadIndex}");
                        }

                        var sensorValue = response.Payload[sensorPayloadIndex];
                        var isLoaded = sensorValue == checkLevel;

                        return ERackSensorStateResult.Ok(
                            location,
                            sensorCommand,
                            response.Payload.ToArray(),
                            isLoaded);
                    }
                    finally
                    {
                        ClosePortAfterOperationIfNeeded(location);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_shutdownRequested || cancellationToken.IsCancellationRequested)
                {
                    return ERackSensorStateResult.Fail(location, sensorCommand, 8, "ERack gateway is shutting down");
                }

                _logger.LogError(
                    ex,
                    "ERack sensor read failed: port={PortName}, baudRate={BaudRate}, address={Address}, location={LocationId}, command=0x{Command:X2}",
                    location.PortName,
                    location.BaudRate,
                    location.DeviceAddress,
                    location.LocationId,
                    sensorCommand);

                return ERackSensorStateResult.Fail(location, sensorCommand, 7, ex.Message);
            }
        }, cancellationToken);
    }

    public Task<OperationResult> SetDisplayTextAsync(
        ERackLocation location,
        string displayText,
        int waitTimeMilliseconds,
        int minWaitCount,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var payload = BuildDisplayPayload(displayText, maxBytes);
        return SendDisplayPayloadAsync(
            location,
            payload,
            waitTimeMilliseconds,
            minWaitCount,
            displayText,
            cancellationToken);
    }

    public Task<OperationResult> ClearDisplayAsync(
        ERackLocation location,
        int waitTimeMilliseconds,
        int minWaitCount,
        CancellationToken cancellationToken)
    {
        return SendDisplayPayloadAsync(
            location,
            Array.Empty<byte>(),
            waitTimeMilliseconds,
            minWaitCount,
            string.Empty,
            cancellationToken);
    }

    private Task<OperationResult> SendDisplayPayloadAsync(
        ERackLocation location,
        byte[] payload,
        int waitTimeMilliseconds,
        int minWaitCount,
        string displayText,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_syncRoot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var waitCount = Math.Max(minWaitCount, payload.Length / 20);
                    var serialPort = EnsureSerialPortOpen(location, waitTimeMilliseconds);
                    try
                    {
                        var response = SendCommandWithResponseLocked(
                            location,
                            serialPort,
                            ERackCommand.SetDisplayAll,
                            payload,
                            waitTimeMilliseconds,
                            waitCount,
                            "display",
                            cancellationToken);

                        if (response.Payload.Length != 1)
                        {
                            return OperationResult.Fail(
                                7,
                                $"display response payload length is invalid: {response.Payload.Length}");
                        }

                        if (response.Payload[0] != ERackCommand.Success)
                        {
                            return OperationResult.Fail(
                                response.Payload[0],
                                $"display returned error code {response.Payload[0]} (0x{response.Payload[0]:X2})");
                        }

                        return OperationResult.Ok("display updated");
                    }
                    finally
                    {
                        ClosePortAfterOperationIfNeeded(location);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_shutdownRequested || cancellationToken.IsCancellationRequested)
                {
                    return OperationResult.Fail(8, "ERack gateway is shutting down");
                }

                _logger.LogError(
                    ex,
                    "ERack display update failed: port={PortName}, baudRate={BaudRate}, address={Address}, location={LocationId}, text={DisplayText}",
                    location.PortName,
                    location.BaudRate,
                    location.DeviceAddress,
                    location.LocationId,
                    displayText);

                return OperationResult.Fail(7, ex.Message);
            }
        }, cancellationToken);
    }

    private string ReadInventoryTag(
        ERackLocation location,
        int readLengthBytes,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var serialPort = EnsureSerialPortOpen(location, location.InventoryWaitTimeMilliseconds);
                var tagBytes = ReadInventoryTagBytesLocked(location, serialPort, cancellationToken);
                return tagBytes.Length == 0
                    ? string.Empty
                    : Encoding.ASCII
                        .GetString(tagBytes.AsSpan(0, NormalizeReadLength(readLengthBytes)))
                        .TrimEnd('\0', ' ');
            }
            finally
            {
                ClosePortAfterOperationIfNeeded(location);
            }
        }
    }

    private byte[] ReadInventoryTagBytesLocked(
        ERackLocation location,
        SerialPort serialPort,
        CancellationToken cancellationToken)
    {
        serialPort.DiscardInBuffer();
        serialPort.DiscardOutBuffer();

        var request = ERackProtocol.BuildRequest(
            location.DeviceAddress,
            ERackCommand.InventoryWithResponse,
            new[] { location.InventoryMode });

        _logger.LogInformation(
            "Sending ERack RFID inventory request: port={PortName}, baudRate={BaudRate}, address={Address}, location={LocationId}, inventoryMode={InventoryMode}, requestHex={RequestHex}",
            location.PortName,
            location.BaudRate,
            location.DeviceAddress,
            location.LocationId,
            location.InventoryMode,
            ToHex(request));

        serialPort.Write(request, 0, request.Length);

        var response = ReadResponse(
            serialPort,
            location.DeviceAddress,
            ERackCommand.InventoryWithResponse,
            location.InventoryWaitTimeMilliseconds,
            location.InventoryWaitCount,
            "inventory",
            cancellationToken);

        _logger.LogInformation(
            "ERack RFID inventory response parsed: command=0x{Command:X2}, address={Address}, payloadLength={PayloadLength}, payloadHex={PayloadHex}",
            response.Command,
            response.Address,
            response.Payload.Length,
            ToHex(response.Payload));

        if (response.Payload.Length == 1)
        {
            throw new InvalidOperationException($"RFID reader returned error code {response.Payload[0]} (0x{response.Payload[0]:X2})");
        }

        if (response.Payload.Length < 32)
        {
            throw new InvalidOperationException($"RFID response payload is too short: {response.Payload.Length}");
        }

        return ERackTagDecoder.DecodeInventoryTagBytes(response.Payload);
    }

    private ERackFrame SendCommandWithResponseLocked(
        ERackLocation location,
        SerialPort serialPort,
        byte command,
        ReadOnlySpan<byte> payload,
        int waitTimeMilliseconds,
        int waitCount,
        string operation,
        CancellationToken cancellationToken)
    {
        serialPort.DiscardInBuffer();
        serialPort.DiscardOutBuffer();

        var request = ERackProtocol.BuildRequest(location.DeviceAddress, command, payload);

        _logger.Log(
            GetOperationLogLevel(operation),
            "Sending ERack {Operation} request: port={PortName}, baudRate={BaudRate}, address={Address}, location={LocationId}, command=0x{Command:X2}, payloadHex={PayloadHex}, requestHex={RequestHex}",
            operation,
            location.PortName,
            location.BaudRate,
            location.DeviceAddress,
            location.LocationId,
            command,
            ToHex(payload),
            ToHex(request));

        serialPort.Write(request, 0, request.Length);

        var response = ReadResponse(
            serialPort,
            location.DeviceAddress,
            command,
            waitTimeMilliseconds,
            waitCount,
            operation,
            cancellationToken);

        _logger.Log(
            GetOperationLogLevel(operation),
            "ERack {Operation} response parsed: command=0x{Command:X2}, address={Address}, payloadLength={PayloadLength}, payloadHex={PayloadHex}",
            operation,
            response.Command,
            response.Address,
            response.Payload.Length,
            ToHex(response.Payload));

        return response;
    }

    private void WriteTag(
        ERackLocation location,
        TagWriteCommand command,
        string tag,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var serialPort = EnsureSerialPortOpen(location, location.WriteTagWaitTimeMilliseconds);
                var logicalLength = ERackTagDecoder.RoundUpToBlock(tag.Length);
                var logicalBytes = new byte[logicalLength];

                if (tag.Length % 8 != 0)
                {
                    byte[] currentBytes;
                    try
                    {
                        currentBytes = ReadInventoryTagBytesLocked(location, serialPort, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "non-page-aligned tag write requires reading the existing tag first; read failed and write was not sent",
                            ex);
                    }

                    if (currentBytes.Length >= logicalLength)
                    {
                        currentBytes.AsSpan(0, logicalLength).CopyTo(logicalBytes);
                    }
                }

                Encoding.ASCII.GetBytes(tag).CopyTo(logicalBytes, 0);
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                var tagBytes = ERackTagDecoder.EncodeWriteTag(logicalBytes);
                var payload = new byte[tagBytes.Length + 1];
                payload[0] = location.WriteTagStartPage;
                tagBytes.CopyTo(payload.AsSpan(1));
                var hardwareTag = Encoding.ASCII.GetString(tagBytes);

                var request = ERackProtocol.BuildRequest(
                    location.DeviceAddress,
                    ERackCommand.WriteTagWithResponse,
                    payload);

                _logger.LogInformation(
                    "Sending ERack RFID write request: port={PortName}, baudRate={BaudRate}, address={Address}, startPage={StartPage}, shelf={ShelfId}, location={LocationId}, tag={Tag}, logicalLength={LogicalLength}, hardwareTag={HardwareTag}, payloadHex={PayloadHex}, requestHex={RequestHex}",
                    location.PortName,
                    location.BaudRate,
                    location.DeviceAddress,
                    location.WriteTagStartPage,
                    command.ShelfId,
                    location.LocationId,
                    tag,
                    logicalLength,
                    hardwareTag,
                    ToHex(payload),
                    ToHex(request));

                serialPort.Write(request, 0, request.Length);

                var response = ReadResponse(
                    serialPort,
                    location.DeviceAddress,
                    ERackCommand.WriteTagWithResponse,
                    location.WriteTagWaitTimeMilliseconds,
                    location.WriteTagWaitCount,
                    "write",
                    cancellationToken);

                _logger.LogInformation(
                    "ERack RFID write response parsed: command=0x{Command:X2}, address={Address}, payloadLength={PayloadLength}, payloadHex={PayloadHex}",
                    response.Command,
                    response.Address,
                    response.Payload.Length,
                    ToHex(response.Payload));

                if (response.Payload.Length != 1)
                {
                    throw new InvalidOperationException($"RFID write response payload length is invalid: {response.Payload.Length}");
                }

                if (response.Payload[0] != ERackCommand.Success)
                {
                    throw new InvalidOperationException($"RFID writer returned error code {response.Payload[0]} (0x{response.Payload[0]:X2})");
                }
            }
            finally
            {
                ClosePortAfterOperationIfNeeded(location);
            }
        }
    }

    private SerialPort EnsureSerialPortOpen(ERackLocation location, int readTimeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_shutdownRequested)
        {
            throw new OperationCanceledException("ERack gateway is shutting down.");
        }

        var portKey = GetPortKey(location);
        if (_serialPorts.TryGetValue(portKey, out var existingPort) && existingPort.IsOpen)
        {
            existingPort.ReadTimeout = readTimeout;
            existingPort.WriteTimeout = 1000;
            return existingPort;
        }

        ClosePortLocked(portKey);

        var serialPort = new SerialPort(location.PortName, location.BaudRate)
        {
            ReadTimeout = readTimeout,
            WriteTimeout = 1000
        };

        serialPort.Open();
        _serialPorts[portKey] = serialPort;
        _logger.LogInformation(
            "ERack COM port opened: port={PortName}, baudRate={BaudRate}, keepPortOpen={KeepPortOpen}, location={LocationId}",
            location.PortName,
            location.BaudRate,
            location.KeepPortOpen,
            location.LocationId);

        return serialPort;
    }

    private void ClosePortAfterOperationIfNeeded(ERackLocation location)
    {
        if (!location.KeepPortOpen)
        {
            ClosePortLocked(GetPortKey(location));
        }
    }

    private void CloseAllPortsLocked()
    {
        foreach (var portKey in _serialPorts.Keys.ToArray())
        {
            ClosePortLocked(portKey);
        }
    }

    private void ClosePortLocked(string portKey)
    {
        if (!_serialPorts.Remove(portKey, out var serialPort))
        {
            return;
        }

        try
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to close ERack COM port: portKey={PortKey}", portKey);
        }
        finally
        {
            try
            {
                serialPort.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose ERack COM port: portKey={PortKey}", portKey);
            }

            _logger.LogInformation("ERack COM port closed: portKey={PortKey}", portKey);
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

            _shutdownRequested = true;
            CloseAllPortsLocked();
            _disposed = true;
        }
    }

    private ERackFrame ReadResponse(
        SerialPort serialPort,
        byte address,
        byte command,
        int waitTimeMilliseconds,
        int waitCount,
        string operation,
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

                var offset = length;
                var bytesRead = serialPort.Read(buffer, offset, remaining);
                length += bytesRead;

                _logger.Log(
                    GetOperationLogLevel(operation),
                    "ERack RFID {Operation} raw read: bytesRead={BytesRead}, totalLength={TotalLength}, readHex={ReadHex}, totalHex={TotalHex}",
                    operation,
                    bytesRead,
                    length,
                    ToHex(buffer.AsSpan(offset, bytesRead)),
                    ToHex(buffer.AsSpan(0, length)));
            }
            catch (TimeoutException)
            {
            }

            if (TryFindResponse(buffer.AsSpan(0, length), address, command, out var response))
            {
                return response;
            }
        }

        throw new TimeoutException($"RFID response timeout after {waitCount} attempts; receivedLength={length}; receivedHex={ToHex(buffer.AsSpan(0, length))}");
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

    private static LogLevel GetOperationLogLevel(string operation)
    {
        return operation.Equals("sensor", StringComparison.OrdinalIgnoreCase) ||
            operation.Equals("inventory", StringComparison.OrdinalIgnoreCase)
            ? LogLevel.Debug
            : LogLevel.Information;
    }

    private static int NormalizeReadLength(int readLengthBytes)
    {
        return ERackTagDecoder.RoundUpToBlock(readLengthBytes <= 0 ? 32 : readLengthBytes);
    }

    private static byte[] BuildDisplayPayload(string displayText, int maxBytes)
    {
        var safeMaxBytes = Math.Clamp(maxBytes, 18, 512);
        var text = displayText ?? string.Empty;
        if (text.Any(character => character > 0x7F || char.IsControl(character)))
        {
            throw new InvalidOperationException("display text must contain printable ASCII characters only");
        }

        var textBytes = Encoding.ASCII.GetBytes(text);
        var maxTextBytes = safeMaxBytes - 18;
        if (textBytes.Length > maxTextBytes)
        {
            textBytes = textBytes.AsSpan(0, maxTextBytes).ToArray();
        }

        var payload = new byte[textBytes.Length + 18];
        textBytes.CopyTo(payload.AsSpan());
        payload[textBytes.Length] = 0x0D;
        payload[textBytes.Length + 1] = 0x0A;
        payload.AsSpan(textBytes.Length + 2, 16).Fill(0x7F);
        return payload;
    }

    private static string GetPortKey(ERackLocation location)
    {
        return $"{location.PortName.Trim().ToUpperInvariant()}|{location.BaudRate}";
    }

    private static string ToHex(ReadOnlySpan<byte> data)
    {
        return data.IsEmpty
            ? "<empty>"
            : string.Join(" ", data.ToArray().Select(value => value.ToString("X2")));
    }
}
