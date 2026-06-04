using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Validation;
using Secs4Net;

namespace PrinterSecsGem.Eq.StatusUi;

public sealed class StatusDashboardForm : Form
{
    private static readonly Font TitleFont = new(FontFamily.GenericSansSerif, 24, FontStyle.Bold);
    private static readonly Font LabelFont = new(FontFamily.GenericSansSerif, 14, FontStyle.Bold);
    private static readonly Font ValueFont = new(FontFamily.GenericSansSerif, 14, FontStyle.Regular);
    private static readonly Font ButtonFont = new(FontFamily.GenericSansSerif, 12, FontStyle.Bold);
    private static readonly Font InputFont = new(FontFamily.GenericSansSerif, 15, FontStyle.Regular);
    private static readonly Font LogFont = new("Consolas", 12, FontStyle.Regular);

    private readonly IHardwareGateway _hardwareGateway;
    private readonly ERackSerialHardwareGateway _erackGateway;
    private readonly IPrinterGateway _printerGateway;
    private readonly SecsGemOptions _secsOptions;
    private readonly PrinterOptions _printerOptions;
    private readonly ERackHardwareOptions _erackOptions;
    private readonly LocalValidationOptions _validationOptions;
    private readonly StatusUiEventBus _statusEvents;
    private readonly CancellationTokenSource _formCancellation = new();
    private readonly Queue<StatusUiEvent> _pendingStatusEvents = new();
    private string _lastSecsState = "Starting";
    private bool _isClosing;

    private readonly Label _secsStatusValue = new();
    private readonly Label _printerStatusValue = new();
    private readonly Label _comStatusValue = new();
    private readonly Label _rfidStatusValue = new();
    private readonly Label _lastPrintValue = new();
    private readonly TextBox _contentTextBox = new();
    private readonly TextBox _logTextBox = new();

    public StatusDashboardForm(
        IHardwareGateway hardwareGateway,
        ERackSerialHardwareGateway erackGateway,
        IPrinterGateway printerGateway,
        IOptions<SecsGemOptions> secsOptions,
        IOptions<PrinterOptions> printerOptions,
        IOptions<ERackHardwareOptions> erackOptions,
        IOptions<LocalValidationOptions> validationOptions,
        StatusUiEventBus statusEvents)
    {
        _hardwareGateway = hardwareGateway;
        _erackGateway = erackGateway;
        _printerGateway = printerGateway;
        _secsOptions = secsOptions.Value;
        _printerOptions = printerOptions.Value;
        _erackOptions = erackOptions.Value;
        _validationOptions = validationOptions.Value;
        _statusEvents = statusEvents;

        Text = "Printer SECS GEM Status";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1280, 820);

        Controls.Add(CreateMainLayout());
        _statusEvents.EventReceived += OnStatusEvent;
        FormClosing += (_, _) => CloseForShutdown("window closing");
        FormClosed += (_, _) =>
        {
            _statusEvents.EventReceived -= OnStatusEvent;
            _formCancellation.Dispose();
        };

        RefreshStatus();
        AppendLog("Status UI started.");
        Shown += async (_, _) =>
        {
            FlushPendingStatusEvents();
            await AutoOpenComAsync();
        };
    }

    private Control CreateMainLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = "Status Dashboard",
            Font = TitleFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(CreateStatusGrid(), 0, 1);
        root.Controls.Add(CreateActionPanel(), 0, 2);
        root.Controls.Add(CreateLogBox(), 0, 3);
        return root;
    }

    private Control CreateStatusGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(0, 8, 0, 8)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddStatusRow(grid, 0, "SECS", _secsStatusValue, "Print", _printerStatusValue);
        AddStatusRow(grid, 1, "COM", _comStatusValue, "RFID", _rfidStatusValue);
        AddStatusRow(grid, 2, "Last Print", _lastPrintValue, "Config", CreateConfigValueLabel());
        return grid;
    }

    private static void AddStatusRow(
        TableLayoutPanel grid,
        int row,
        string leftName,
        Control leftValue,
        string rightName,
        Control rightValue)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        grid.Controls.Add(CreateNameLabel(leftName), 0, row);
        grid.Controls.Add(StyleValueControl(leftValue), 1, row);
        grid.Controls.Add(CreateNameLabel(rightName), 2, row);
        grid.Controls.Add(StyleValueControl(rightValue), 3, row);
    }

    private static Label CreateNameLabel(string text)
    {
        return new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = LabelFont
        };
    }

    private static Control StyleValueControl(Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Font = ValueFont;
        if (control is Label label)
        {
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        return control;
    }

    private Label CreateConfigValueLabel()
    {
        return new Label
        {
            Text = $"Host {_secsOptions.IpAddress}:{_secsOptions.Port}, DeviceId {_secsOptions.DeviceId}"
        };
    }

    private Control CreateActionPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 8, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 620));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var contentLabel = CreateNameLabel("Content");
        _contentTextBox.Dock = DockStyle.Fill;
        _contentTextBox.Text = _validationOptions.Content;
        _contentTextBox.Font = InputFont;
        _contentTextBox.Margin = new Padding(4, 14, 18, 4);

        panel.Controls.Add(contentLabel, 0, 0);
        panel.SetRowSpan(contentLabel, 2);
        panel.Controls.Add(_contentTextBox, 1, 0);
        panel.SetRowSpan(_contentTextBox, 2);
        panel.Controls.Add(CreateButtonGrid(), 2, 0);
        panel.SetRowSpan(panel.GetControlFromPosition(2, 0)!, 2);
        return panel;
    }

    private Control CreateButtonGrid()
    {
        var buttonGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2
        };
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        buttonGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttonGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        buttonGrid.Controls.Add(CreateButton("Refresh", (_, _) => RefreshStatus()), 0, 0);
        buttonGrid.Controls.Add(CreateButton("Open COM", async (_, _) => await OpenComAsync()), 1, 0);
        buttonGrid.Controls.Add(CreateButton("Close COM", (_, _) => CloseCom()), 2, 0);
        buttonGrid.Controls.Add(CreateButton("Read RFID", async (_, _) => await ReadRfidAsync()), 0, 1);
        buttonGrid.Controls.Add(CreateButton("Write RFID", async (_, _) => await WriteRfidAsync()), 1, 1);
        buttonGrid.Controls.Add(CreateButton("Test Print", async (_, _) => await TestPrintAsync()), 2, 1);
        return buttonGrid;
    }

    private static Button CreateButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = ButtonFont,
            Margin = new Padding(6),
            MinimumSize = new Size(160, 50),
            UseVisualStyleBackColor = true
        };
        button.Click += click;
        return button;
    }

    private Control CreateLogBox()
    {
        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ReadOnly = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.Font = LogFont;
        return _logTextBox;
    }

    private async Task OpenComAsync()
    {
        await RunUiActionAsync("Open COM", () =>
        {
            var result = _erackGateway.OpenPort();
            AppendResult("Open COM", result);
            RefreshStatus();
            return Task.CompletedTask;
        });
    }

    private async Task AutoOpenComAsync()
    {
        await RunUiActionAsync("Auto Open COM", () =>
        {
            var result = _erackGateway.OpenPort();
            AppendResult("Auto Open COM", result);
            RefreshStatus();
            return Task.CompletedTask;
        });
    }

    private void CloseCom()
    {
        _erackGateway.ClosePort();
        AppendLog("COM port closed by UI.");
        RefreshStatus();
    }

    private async Task ReadRfidAsync()
    {
        await RunUiActionAsync("Read RFID", async () =>
        {
            var result = await _hardwareGateway.QueryShelfStatusAsync(
                new ShelfStatusQuery(_validationOptions.ShelfId, _validationOptions.LocationId),
                _formCancellation.Token);

            if (!result.Success)
            {
                _rfidStatusValue.Text = $"Read failed: {result.Description}";
                AppendLog($"Read RFID failed: code={result.Code}, description={result.Description}");
                return;
            }

            var location = result.Locations.FirstOrDefault();
            var tag = location?.Tag ?? string.Empty;
            _rfidStatusValue.Text = string.IsNullOrWhiteSpace(tag) ? "No tag" : tag;
            AppendLog($"Read RFID completed: tag={tag}, loaded={location?.IsLoaded}");
            RefreshStatus();
        });
    }

    private async Task WriteRfidAsync()
    {
        await RunUiActionAsync("Write RFID", async () =>
        {
            var tag = _contentTextBox.Text.Trim();
            var result = await _hardwareGateway.WriteTagAsync(
                new TagWriteCommand(_validationOptions.ShelfId, _validationOptions.LocationId, tag),
                _formCancellation.Token);

            AppendResult("Write RFID", result);
            if (result.Success)
            {
                _rfidStatusValue.Text = $"Written: {tag}";
            }

            RefreshStatus();
        });
    }

    private async Task TestPrintAsync()
    {
        await RunUiActionAsync("Test print", async () =>
        {
            var content = _contentTextBox.Text.Trim();
            var result = await _printerGateway.PrintAsync(
                new PrintCommand(_validationOptions.ShelfId, _validationOptions.PrinterId, content, _validationOptions.Copies),
                _formCancellation.Token);

            AppendResult("Test print", result);
            _lastPrintValue.Text = result.Description;
            RefreshStatus();
        });
    }

    private async Task RunUiActionAsync(string name, Func<Task> action)
    {
        try
        {
            AppendLog($"{name} started.");
            await action();
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{name} canceled.");
        }
        catch (Exception ex)
        {
            AppendLog($"{name} failed: {ex.Message}");
        }
    }

    private void AppendResult(string operation, OperationResult result)
    {
        AppendLog(
            result.Success
                ? $"{operation} completed: {result.Description}"
                : $"{operation} failed: code={result.Code}, description={result.Description}");
    }

    private void RefreshStatus()
    {
        if (_isClosing || IsDisposed)
        {
            return;
        }

        _secsStatusValue.Text = $"HSMS {_lastSecsState}, {_secsOptions.IpAddress}:{_secsOptions.Port}, DeviceId={_secsOptions.DeviceId}";
        _printerStatusValue.Text = _printerOptions.RealPrintEnabled
            ? $"Real print ON, mode={_printerOptions.Mode}"
            : $"Real print OFF, ZPL only: {_printerOptions.OutputDirectory}";

        var portStatus = _erackGateway.GetPortStatus();
        _comStatusValue.Text = _erackOptions.Enabled
            ? $"{portStatus.PortName} {(portStatus.IsOpen ? "open" : "closed")}, keepOpen={portStatus.KeepPortOpen}"
            : $"{portStatus.PortName} disabled, mock hardware";

        if (string.IsNullOrWhiteSpace(_rfidStatusValue.Text))
        {
            _rfidStatusValue.Text = "No RFID operation yet";
        }

        if (string.IsNullOrWhiteSpace(_lastPrintValue.Text))
        {
            _lastPrintValue.Text = "No print operation yet";
        }
    }

    private void AppendLog(string message)
    {
        if (_isClosing || _logTextBox.IsDisposed)
        {
            return;
        }

        _logTextBox.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
    }

    private void OnStatusEvent(object? sender, StatusUiEvent statusEvent)
    {
        if (_isClosing || IsDisposed)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            lock (_pendingStatusEvents)
            {
                _pendingStatusEvents.Enqueue(statusEvent);
            }

            return;
        }

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => ApplyStatusEvent(statusEvent)));
                return;
            }

            ApplyStatusEvent(statusEvent);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void FlushPendingStatusEvents()
    {
        while (true)
        {
            StatusUiEvent statusEvent;
            lock (_pendingStatusEvents)
            {
                if (_pendingStatusEvents.Count == 0)
                {
                    return;
                }

                statusEvent = _pendingStatusEvents.Dequeue();
            }

            ApplyStatusEvent(statusEvent);
        }
    }

    private void ApplyStatusEvent(StatusUiEvent statusEvent)
    {
        if (_isClosing || IsDisposed)
        {
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.SecsState)
        {
            _lastSecsState = statusEvent.Message;
            AppendLog($"SECS state: {statusEvent.Message}");
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.RfidStatus)
        {
            _rfidStatusValue.Text = statusEvent.Message;
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.LastPrint)
        {
            _lastPrintValue.Text = statusEvent.Message;
            RefreshStatus();
            return;
        }

        AppendLog(statusEvent.Message);
        RefreshStatus();
    }

    private void CloseForShutdown(string reason)
    {
        if (_isClosing)
        {
            return;
        }

        AppendLog($"Status UI shutting down: {reason}.");
        _isClosing = true;
        _formCancellation.Cancel();
        _erackGateway.ClosePort();
    }
}
