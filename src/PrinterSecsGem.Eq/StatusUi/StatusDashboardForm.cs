using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq;
using PrinterSecsGem.Eq.ErackNetwork;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Validation;
using Secs4Net;

namespace PrinterSecsGem.Eq.StatusUi;

public sealed class StatusDashboardForm : Form
{
    private static readonly Font TitleFont = new(FontFamily.GenericSansSerif, 14, FontStyle.Bold);
    private static readonly Font LabelFont = new(FontFamily.GenericSansSerif, 8.25f, FontStyle.Bold);
    private static readonly Font ValueFont = new(FontFamily.GenericSansSerif, 8.25f, FontStyle.Regular);
    private static readonly Font ButtonFont = new(FontFamily.GenericSansSerif, 8f, FontStyle.Bold);
    private static readonly Font InputFont = new(FontFamily.GenericSansSerif, 8.25f, FontStyle.Regular);
    private static readonly Font LogFont = new("Consolas", 8.25f, FontStyle.Regular);
    private static readonly Color WindowBackColor = Color.FromArgb(247, 248, 250);
    private static readonly Color SurfaceBackColor = Color.White;
    private static readonly Color UiBorderColor = Color.FromArgb(207, 214, 224);
    private static readonly Color TextColor = Color.FromArgb(24, 32, 44);
    private static readonly Color MutedTextColor = Color.FromArgb(73, 84, 101);
    private static readonly Color ButtonHoverColor = Color.FromArgb(240, 245, 252);
    private const int StatusRowHeight = 31;
    private const int StatusTitleHeight = 24;

    private readonly IHardwareGateway _hardwareGateway;
    private readonly ERackSerialHardwareGateway _erackGateway;
    private readonly IPrinterGateway _printerGateway;
    private readonly ZebraCommandLinePrinterGateway _zebraGateway;
    private readonly AppConfigWriter _configWriter;
    private readonly SecsGemOptions _secsOptions;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly PrinterOptions _printerOptions;
    private readonly ERackHardwareOptions _erackOptions;
    private readonly ERackSensorDisplayOptions _sensorDisplayOptions;
    private readonly ERackServerOptions _erackServerOptions;
    private readonly ERackClientOptions _erackClientOptions;
    private readonly ERackSimulationOptions _simulationOptions;
    private readonly LocalValidationOptions _validationOptions;
    private readonly StatusUiEventBus _statusEvents;
    private readonly StatusUiText _uiText;
    private readonly CancellationTokenSource _formCancellation = new();
    private readonly Queue<StatusUiEvent> _pendingStatusEvents = new();
    private string _lastSecsState = "Starting";
    private string _erackServerStatus = string.Empty;
    private string _erackUnitClientStatus = string.Empty;
    private string _erackRoutesStatus = "0 online";
    private string _simulationStatus = string.Empty;
    private string _displayStatus = string.Empty;
    private bool _isClosing;

    private readonly Label _configPathValue = CreateStatusValueLabel();
    private readonly Label _secsStatusValue = CreateStatusValueLabel();
    private readonly Label _secsRoleValue = CreateStatusValueLabel();
    private readonly Label _erackServerStatusValue = CreateStatusValueLabel();
    private readonly Label _erackUnitClientStatusValue = CreateStatusValueLabel();
    private readonly Label _erackRoutesStatusValue = CreateStatusValueLabel();
    private readonly Label _simulationStatusValue = CreateStatusValueLabel();
    private readonly Label _printerStatusValue = CreateStatusValueLabel();
    private readonly Label _comStatusValue = CreateStatusValueLabel();
    private readonly Label _rfidStatusValue = CreateStatusValueLabel();
    private readonly Label _displayStatusValue = CreateStatusValueLabel();
    private readonly Label _lastPrintValue = CreateStatusValueLabel();
    private readonly Label _recentSecsValue = CreateStatusValueLabel();
    private readonly TextBox _contentTextBox = new();
    private readonly ComboBox _printerComboBox = new();
    private readonly TextBox _logTextBox = new();

    public StatusDashboardForm(
        IHardwareGateway hardwareGateway,
        ERackSerialHardwareGateway erackGateway,
        IPrinterGateway printerGateway,
        ZebraCommandLinePrinterGateway zebraGateway,
        AppConfigWriter configWriter,
        IOptions<SecsGemOptions> secsOptions,
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<PrinterOptions> printerOptions,
        IOptions<ERackHardwareOptions> erackOptions,
        IOptions<ERackSensorDisplayOptions> sensorDisplayOptions,
        IOptions<ERackServerOptions> erackServerOptions,
        IOptions<ERackClientOptions> erackClientOptions,
        IOptions<ERackSimulationOptions> simulationOptions,
        IOptions<LocalValidationOptions> validationOptions,
        IOptions<StatusUiOptions> statusUiOptions,
        StatusUiEventBus statusEvents)
    {
        _hardwareGateway = hardwareGateway;
        _erackGateway = erackGateway;
        _printerGateway = printerGateway;
        _zebraGateway = zebraGateway;
        _configWriter = configWriter;
        _secsOptions = secsOptions.Value;
        _runtimeOptions = runtimeOptions.Value;
        _printerOptions = printerOptions.Value;
        _erackOptions = erackOptions.Value;
        _sensorDisplayOptions = sensorDisplayOptions.Value;
        _erackServerOptions = erackServerOptions.Value;
        _erackClientOptions = erackClientOptions.Value;
        _simulationOptions = simulationOptions.Value;
        _validationOptions = validationOptions.Value;
        _statusEvents = statusEvents;
        _uiText = new StatusUiText(statusUiOptions.Value);

        Text = $"{_uiText["WindowTitle"]} {ApplicationInfo.DisplayVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 700);
        Size = new Size(1180, 860);
        BackColor = WindowBackColor;
        _configPathValue.Text = Path.Combine(AppContext.BaseDirectory, "App.config");
        _recentSecsValue.Text = _uiText["NoSecsOperation"];
        InitializeRuntimeStatusText();

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

    private void InitializeRuntimeStatusText()
    {
        _erackServerStatus = _runtimeOptions.IsServerEnabled && _erackServerOptions.Enabled
            ? $"Starting {_erackServerOptions.ListenIp}:{_erackServerOptions.Port}"
            : "Disabled";
        _erackUnitClientStatus = _runtimeOptions.IsUnitEnabled && (_erackClientOptions.Enabled || _runtimeOptions.IsServerEnabled)
            ? $"Starting unit={NormalizeText(_erackClientOptions.UnitId, Environment.MachineName)}, shelf={NormalizeText(_erackClientOptions.ShelfId, "SHELF001")}"
            : "Disabled";
        _simulationStatus = _runtimeOptions.IsUnitEnabled && _simulationOptions.Enabled
            ? $"Enabled shelf={NormalizeText(_simulationOptions.ShelfId, "SHELF001")}, location={NormalizeText(_simulationOptions.LocationId, "LOC001")}"
            : "Disabled";
        _erackRoutesStatus = _runtimeOptions.IsServerEnabled
            ? "0 online"
            : "Routes are maintained by ERACK Server";
        _displayStatus = BuildInitialDisplayStatusText();
    }

    private Control CreateMainLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10),
            BackColor = WindowBackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 212));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = $"{_uiText["DashboardTitle"]} {ApplicationInfo.DisplayVersion}",
            Font = TitleFont,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 0, 4)
        };

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(CreateStatusGrid(), 0, 1);
        root.Controls.Add(CreateStatusPanel(
            _uiText["LocalDeviceGroup"],
            (_uiText["Print"], _printerStatusValue),
            (_uiText["Com"], _comStatusValue),
            (_uiText["Display"], _displayStatusValue)), 0, 2);
        root.Controls.Add(CreateActionPanel(), 0, 3);
        root.Controls.Add(CreateLogBox(), 0, 4);
        return root;
    }

    private Control CreateStatusGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var leftStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        leftStack.Controls.Add(CreateStatusPanel(
            _uiText["SecsGroup"],
            (_uiText["SecsState"], _secsStatusValue),
            (_uiText["SecsRole"], _secsRoleValue)), 0, 0);

        leftStack.Controls.Add(CreateStatusPanel(
            _uiText["ClientGroup"],
            (_uiText["ClientState"], _erackUnitClientStatusValue),
            (_uiText["Rfid"], _rfidStatusValue)), 0, 1);

        grid.Controls.Add(leftStack, 0, 0);

        grid.Controls.Add(CreateStatusPanel(
            _uiText["ServerGroup"],
            (_uiText["ServerState"], _erackServerStatusValue),
            (_uiText["Routes"], _erackRoutesStatusValue)), 1, 0);

        return grid;
    }

    private Control CreateSummaryPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(4, 2, 4, 2)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        panel.Controls.Add(CreateNameLabel(_uiText["Config"]), 0, 0);
        panel.Controls.Add(StyleValueControl(CreateConfigValueLabel()), 1, 0);
        panel.Controls.Add(CreateNameLabel(_uiText["ConfigPath"]), 0, 1);
        panel.Controls.Add(StyleValueControl(_configPathValue), 1, 1);
        return panel;
    }

    private Control CreateStatusSections()
    {
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0, 3, 0, 3)
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        stack.Controls.Add(CreateStatusBand(
            _uiText["SecsGroup"],
            (_uiText["SecsState"], _secsStatusValue),
            (_uiText["SecsRole"], _secsRoleValue)), 0, 0);

        stack.Controls.Add(CreateStatusBand(
            _uiText["ServerGroup"],
            (_uiText["ServerState"], _erackServerStatusValue),
            (_uiText["Routes"], _erackRoutesStatusValue)), 0, 1);

        stack.Controls.Add(CreateStatusBand(
            _uiText["ClientGroup"],
            (_uiText["ClientState"], _erackUnitClientStatusValue),
            (_uiText["Rfid"], _rfidStatusValue),
            (_uiText["LastPrint"], _lastPrintValue)), 0, 2);

        stack.Controls.Add(CreateStatusBand(
            _uiText["LocalDeviceGroup"],
            (_uiText["Print"], _printerStatusValue),
            (_uiText["Com"], _comStatusValue),
            (_uiText["Display"], _displayStatusValue)), 0, 3);

        stack.Controls.Add(CreateStatusBand(
            _uiText["SimulationGroup"],
            (_uiText["SimulationState"], _simulationStatusValue),
            (_uiText["RecentSecs"], _recentSecsValue)), 0, 4);
        return stack;
    }

    private Control CreateStatusBand(string title, params (string Name, Control Value)[] rows)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(4, 2, 4, 2),
            Padding = new Padding(8, 2, 8, 2)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = LabelFont,
            AutoEllipsis = true
        };
        panel.Controls.Add(titleLabel, 0, 0);

        var valuesGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows.Length
        };
        valuesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        valuesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var row = 0; row < rows.Length; row++)
        {
            valuesGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows.Length));
            valuesGrid.Controls.Add(CreateNameLabel(rows[row].Name), 0, row);
            valuesGrid.Controls.Add(StyleValueControl(rows[row].Value), 1, row);
        }

        panel.Controls.Add(valuesGrid, 1, 0);
        return panel;
    }

    private Control CreateStatusPanel(string title, params (string Name, Control Value)[] rows)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(4),
            Padding = new Padding(0),
            BackColor = WindowBackColor
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusTitleHeight));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = LabelFont,
            ForeColor = TextColor,
            Margin = new Padding(0),
            Padding = new Padding(2, 0, 0, 0),
            UseMnemonic = false
        };

        var border = new BorderedPanel
        {
            Dock = DockStyle.Fill,
            BorderColor = UiBorderColor,
            BackColor = SurfaceBackColor,
            Margin = new Padding(0),
            Padding = new Padding(8, 5, 8, 5)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = rows.Length,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = SurfaceBackColor
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var row = 0; row < rows.Length; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusRowHeight));
            grid.Controls.Add(CreateNameLabel(rows[row].Name), 0, row);
            grid.Controls.Add(StyleValueControl(rows[row].Value), 1, row);
        }

        border.Controls.Add(grid);
        panel.Controls.Add(titleLabel, 0, 0);
        panel.Controls.Add(border, 0, 1);
        return panel;
    }

    private Control CreateStatusGroup(string title, params (string Name, Control Value)[] rows)
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = LabelFont,
            Padding = new Padding(10, 14, 10, 6),
            Margin = new Padding(4)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows.Length + 1,
            Padding = new Padding(0, 2, 0, 0)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var row = 0; row < rows.Length; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusRowHeight));
            grid.Controls.Add(CreateNameLabel(rows[row].Name), 0, row);
            grid.Controls.Add(StyleValueControl(rows[row].Value), 1, row);
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        group.Controls.Add(grid);
        return group;
    }

    private static void AddStatusRow(
        TableLayoutPanel grid,
        int row,
        string leftName,
        Control leftValue,
        string rightName,
        Control rightValue)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
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
            Font = LabelFont,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoEllipsis = true,
            Padding = new Padding(0, 0, 4, 0),
            UseMnemonic = false
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
            label.AutoEllipsis = false;
            label.UseMnemonic = false;
            label.Margin = new Padding(0);
            label.MinimumSize = new Size(0, 30);
            label.BackColor = SurfaceBackColor;
            label.ForeColor = TextColor;
        }
        else if (control is TextBox textBox)
        {
            textBox.Multiline = true;
            textBox.ReadOnly = true;
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = SurfaceBackColor;
            textBox.ForeColor = TextColor;
            textBox.ScrollBars = ScrollBars.None;
            textBox.WordWrap = true;
            textBox.Margin = new Padding(0, 2, 0, 0);
            textBox.MinimumSize = new Size(0, 30);
        }

        return control;
    }

    private Label CreateConfigValueLabel()
    {
        var label = CreateStatusValueLabel();
        label.Text = $"{_uiText["Mode"]} {_runtimeOptions.NormalizedMode}; {CompactSecsRoleText()}; {_uiText["DeviceId"]} {_secsOptions.DeviceId}";
        return label;
    }

    private static Label CreateStatusValueLabel()
    {
        return new Label
        {
            AutoSize = false,
            BackColor = SurfaceBackColor,
            Font = ValueFont,
            ForeColor = TextColor,
            Margin = new Padding(0),
            Padding = new Padding(0),
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
    }

    private Control CreateActionPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(4, 6, 4, 6),
            BackColor = WindowBackColor
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

        var printerLabel = CreateNameLabel(_uiText["Printer"]);
        _printerComboBox.Dock = DockStyle.Fill;
        _printerComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _printerComboBox.Font = InputFont;
        _printerComboBox.BackColor = SurfaceBackColor;
        _printerComboBox.ForeColor = TextColor;
        _printerComboBox.FlatStyle = FlatStyle.System;
        _printerComboBox.Margin = new Padding(4, 6, 10, 4);
        if (!string.IsNullOrWhiteSpace(_printerOptions.ZebraPrinterAddress))
        {
            _printerComboBox.Items.Add(_printerOptions.ZebraPrinterAddress);
            _printerComboBox.Text = _printerOptions.ZebraPrinterAddress;
        }

        panel.Controls.Add(printerLabel, 0, 0);
        panel.Controls.Add(_printerComboBox, 1, 0);
        panel.Controls.Add(CreatePrinterButtonGrid(), 2, 0);

        var contentLabel = CreateNameLabel(_uiText["Content"]);
        _contentTextBox.Dock = DockStyle.Fill;
        _contentTextBox.Text = _validationOptions.Content;
        _contentTextBox.Font = InputFont;
        _contentTextBox.BackColor = SurfaceBackColor;
        _contentTextBox.ForeColor = TextColor;
        _contentTextBox.BorderStyle = BorderStyle.FixedSingle;
        _contentTextBox.Margin = new Padding(4, 6, 10, 4);

        panel.Controls.Add(contentLabel, 0, 1);
        panel.Controls.Add(_contentTextBox, 1, 1);
        panel.SetColumnSpan(_contentTextBox, 2);
        panel.Controls.Add(CreateButtonGrid(), 0, 2);
        panel.SetColumnSpan(panel.GetControlFromPosition(0, 2)!, 3);
        return panel;
    }

    private Control CreatePrinterButtonGrid()
    {
        var buttonGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = WindowBackColor
        };
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonGrid.Controls.Add(CreateButton(_uiText["Discover"], async (_, _) => await DiscoverPrintersAsync()), 0, 0);
        buttonGrid.Controls.Add(CreateButton(_uiText["Save"], (_, _) => SavePrinterSelection()), 1, 0);
        return buttonGrid;
    }

    private Control CreateButtonGrid()
    {
        var buttonGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = WindowBackColor
        };
        for (var index = 0; index < 6; index++)
        {
            buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666f));
        }

        buttonGrid.Controls.Add(CreateButton(_uiText["Refresh"], (_, _) => RefreshStatus()), 0, 0);
        buttonGrid.Controls.Add(CreateButton(_uiText["OpenCom"], async (_, _) => await OpenComAsync()), 1, 0);
        buttonGrid.Controls.Add(CreateButton(_uiText["CloseCom"], (_, _) => CloseCom()), 2, 0);
        buttonGrid.Controls.Add(CreateButton(_uiText["ReadRfid"], async (_, _) => await ReadRfidAsync()), 3, 0);
        buttonGrid.Controls.Add(CreateButton(_uiText["WriteRfid"], async (_, _) => await WriteRfidAsync()), 4, 0);
        buttonGrid.Controls.Add(CreateButton(_uiText["TestPrint"], async (_, _) => await TestPrintAsync()), 5, 0);
        return buttonGrid;
    }

    private static Button CreateButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = ButtonFont,
            Margin = new Padding(2),
            MinimumSize = new Size(78, 30),
            Padding = new Padding(0),
            BackColor = SurfaceBackColor,
            ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = UiBorderColor;
        button.FlatAppearance.MouseOverBackColor = ButtonHoverColor;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 236, 249);
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
        _logTextBox.BackColor = SurfaceBackColor;
        _logTextBox.ForeColor = TextColor;
        _logTextBox.BorderStyle = BorderStyle.FixedSingle;
        _logTextBox.Margin = new Padding(4, 4, 4, 0);
        return _logTextBox;
    }

    private async Task DiscoverPrintersAsync()
    {
        await RunUiActionAsync("Discover printers", async () =>
        {
            var printers = await _zebraGateway.DiscoverUsbPrintersAsync(_formCancellation.Token);
            _printerComboBox.Items.Clear();
            foreach (var printer in printers)
            {
                _printerComboBox.Items.Add(printer);
            }

            if (printers.Count > 0)
            {
                _printerComboBox.Text = printers[0];
                AppendLog($"Discovered {printers.Count} USB Zebra printer(s).");
            }
            else
            {
                AppendLog("No USB Zebra printer discovered.");
            }
        });
    }

    private void SavePrinterSelection()
    {
        var printerAddress = _printerComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(printerAddress))
        {
            AppendLog("Save printer skipped: printer address is empty.");
            return;
        }

        try
        {
            _configWriter.SetAppSetting("Printer:ZebraPrinterAddress", printerAddress);
            _printerOptions.ZebraPrinterAddress = printerAddress;
            _zebraGateway.SetPrinterAddress(printerAddress);
            AppendLog($"Default printer saved: {printerAddress}");
            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppendLog($"Save printer failed: {ex.Message}");
        }
    }

    private async Task OpenComAsync()
    {
        await RunUiActionAsync("Open COM", () =>
        {
            if (!CanOpenRealCom())
            {
                AppendLog(GetComOpenSkippedMessage());
                RefreshStatus();
                return Task.CompletedTask;
            }

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
            if (!CanOpenRealCom())
            {
                AppendLog(GetComOpenSkippedMessage());
                RefreshStatus();
                return Task.CompletedTask;
            }

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
            if (!_runtimeOptions.IsUnitEnabled)
            {
                AppendLog("Read RFID skipped: Runtime:Mode=Server does not enable local ERACK unit hardware.");
                return;
            }

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
            if (!_runtimeOptions.IsUnitEnabled)
            {
                AppendLog("Write RFID skipped: Runtime:Mode=Server does not enable local ERACK unit hardware.");
                return;
            }

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

        _secsStatusValue.Text = $"HSMS {_uiText.Status(_lastSecsState)}; {_secsOptions.IpAddress}:{_secsOptions.Port}; ID {_secsOptions.DeviceId}";
        _secsRoleValue.Text = BuildSecsRoleText();
        _erackServerStatusValue.Text = _uiText.Status(_erackServerStatus);
        _erackUnitClientStatusValue.Text = _uiText.Status(CompactUnitClientStatus(_erackUnitClientStatus));
        _erackRoutesStatusValue.Text = _uiText.Status(CompactRoutesStatus(_erackRoutesStatus));
        _simulationStatusValue.Text = _uiText.Status(_simulationStatus);
        _displayStatusValue.Text = CompactDisplayStatus(_uiText.Status(_displayStatus));

        var printerAddress = string.IsNullOrWhiteSpace(_printerOptions.ZebraPrinterAddress)
            ? _uiText["AutoUsb"]
            : _printerOptions.ZebraPrinterAddress;
        _printerStatusValue.Text = _printerOptions.RealPrintEnabled
            ? $"{CompactRealPrintOnText()}; {_printerOptions.Mode}; {CompactPrinterAddress(printerAddress)}"
            : $"{_uiText["RealPrintOff"]}: {CompactPath(_printerOptions.OutputDirectory)}";

        var portStatus = _erackGateway.GetPortStatus();
        _comStatusValue.Text = !_runtimeOptions.IsUnitEnabled
            ? _uiText["UnitDisabled"]
            : _erackOptions.Enabled
            ? $"{portStatus.PortName} {(portStatus.IsOpen ? "open" : "closed")}, keepOpen={portStatus.KeepPortOpen}"
            : $"{portStatus.PortName} disabled, {_uiText["MockHardware"]}";

        if (string.IsNullOrWhiteSpace(_rfidStatusValue.Text))
        {
            _rfidStatusValue.Text = _uiText["NoRfidOperation"];
        }

        if (string.IsNullOrWhiteSpace(_displayStatusValue.Text))
        {
            _displayStatusValue.Text = _uiText["NoDisplayOperation"];
        }

        if (string.IsNullOrWhiteSpace(_lastPrintValue.Text))
        {
            _lastPrintValue.Text = _uiText["NoPrintOperation"];
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

        if (statusEvent.Category == StatusUiEventCategories.SecsLog)
        {
            _recentSecsValue.Text = _uiText.Status(statusEvent.Message);
            AppendLog(statusEvent.Message);
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.RfidStatus)
        {
            _rfidStatusValue.Text = _uiText.Status(statusEvent.Message);
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.DisplayStatus)
        {
            _displayStatus = statusEvent.Message;
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.LastPrint)
        {
            _lastPrintValue.Text = _uiText.Status(statusEvent.Message);
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.ERackServerStatus)
        {
            _erackServerStatus = statusEvent.Message;
            AppendLog($"ERACK Server: {statusEvent.Message}");
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.ERackUnitClientStatus)
        {
            _erackUnitClientStatus = statusEvent.Message;
            AppendLog($"Unit Client: {statusEvent.Message}");
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.ERackRoutesStatus)
        {
            _erackRoutesStatus = statusEvent.Message;
            AppendLog($"Routes: {statusEvent.Message}");
            RefreshStatus();
            return;
        }

        if (statusEvent.Category == StatusUiEventCategories.SimulationStatus)
        {
            _simulationStatus = statusEvent.Message;
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

    private bool CanOpenRealCom()
    {
        return _runtimeOptions.IsUnitEnabled && _erackOptions.Enabled;
    }

    private string GetComOpenSkippedMessage()
    {
        if (!_runtimeOptions.IsUnitEnabled)
        {
            return "COM open skipped: Runtime:Mode=Server does not enable local ERACK unit hardware.";
        }

        return "COM open skipped: ERackHardware:Enabled=false, mock hardware is active.";
    }

    private string BuildSecsRoleText()
    {
        return _secsOptions.IsActive
            ? $"主动 Host {_secsOptions.IpAddress}:{_secsOptions.Port}"
            : $"被动监听 {_secsOptions.IpAddress}:{_secsOptions.Port}";
    }

    private string CompactSecsRoleText()
    {
        return _secsOptions.IsActive
            ? $"主动 {_secsOptions.IpAddress}:{_secsOptions.Port}"
            : $"被动 {_secsOptions.IpAddress}:{_secsOptions.Port}";
    }

    private string CompactRealPrintOnText()
    {
        return _uiText["RealPrintOn"].Replace("开启", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static string CompactUnitClientStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        var text = status.Trim();
        if (text.StartsWith("Registered unit=", StringComparison.OrdinalIgnoreCase))
        {
            var unit = ExtractBetween(text, "unit=", ",");
            var shelf = ExtractBetween(text, "shelf=", ",");
            var locations = ExtractAfter(text, "locations=");
            return $"Registered {unit} / {shelf} / loc={locations}";
        }

        if (text.StartsWith("Connected ", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = ExtractBetween(text, "Connected ", ",");
            var shelf = ExtractAfter(text, "shelf=");
            return $"Connected {endpoint}; shelf={shelf}";
        }

        return text.Length <= 54 ? text : $"{text[..51]}...";
    }

    private static string CompactRoutesStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        var text = status.Trim();
        var lastIndex = text.IndexOf("; last=", StringComparison.OrdinalIgnoreCase);
        if (lastIndex >= 0)
        {
            text = text[..lastIndex];
        }

        return text.Length <= 54 ? text : $"{text[..51]}...";
    }

    private static string ExtractBetween(string text, string start, string end)
    {
        var startIndex = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += start.Length;
        var endIndex = text.IndexOf(end, startIndex, StringComparison.OrdinalIgnoreCase);
        return endIndex < 0 ? text[startIndex..].Trim() : text[startIndex..endIndex].Trim();
    }

    private static string ExtractAfter(string text, string start)
    {
        var startIndex = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        return text[(startIndex + start.Length)..].Trim();
    }

    private static string CompactDisplayStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        status = status
            .Replace("Display disabled: ERackSensorDisplay:Enabled=false", "屏幕禁用: SensorDisplay=false", StringComparison.OrdinalIgnoreCase)
            .Replace("Display disabled: ERackHardware:Enabled=false", "屏幕禁用: 硬件未启用", StringComparison.OrdinalIgnoreCase)
            .Replace("Display enabled: RFID polling presence mode", "屏幕启用: RFID轮询", StringComparison.OrdinalIgnoreCase)
            .Replace("Display enabled: waiting for sensor state", "屏幕启用: 等待传感器", StringComparison.OrdinalIgnoreCase)
            .Replace("Mock display: real screen not connected", "模拟屏幕: 未接真实屏", StringComparison.OrdinalIgnoreCase);

        return status
            .Replace("屏幕显示已禁用：ERackSensorDisplay:Enabled=false", "屏幕禁用；SensorDisplay=false", StringComparison.OrdinalIgnoreCase)
            .Replace("屏幕显示已启用：等待传感器状态", "屏幕启用；等待传感器", StringComparison.OrdinalIgnoreCase)
            .Replace("模拟屏幕：未连接真实屏", "模拟屏幕；未接真实屏", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string CompactPrinterAddress(string printerAddress)
    {
        if (string.IsNullOrWhiteSpace(printerAddress))
        {
            return string.Empty;
        }

        var text = printerAddress.Trim();
        if (text.StartsWith("usb#", StringComparison.OrdinalIgnoreCase))
        {
            return "USB 已指定";
        }

        return text.Length <= 40 ? text : $"{text[..37]}...";
    }

    private static string CompactPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var text = path.Trim();
        return text.Length <= 44 ? text : $"...{text[^41..]}";
    }

    private string BuildInitialDisplayStatusText()
    {
        if (!_runtimeOptions.IsUnitEnabled)
        {
            return "Display skipped: Runtime:Mode does not enable Unit";
        }

        if (!_erackOptions.Enabled)
        {
            return _simulationOptions.Enabled
                ? "Mock display: real screen not connected"
                : "Display disabled: ERackHardware:Enabled=false";
        }

        if (!_sensorDisplayOptions.Enabled)
        {
            return "Display disabled: ERackSensorDisplay:Enabled=false";
        }

        return _sensorDisplayOptions.IsRfidPollingMode
            ? "Display enabled: RFID polling presence mode"
            : "Display enabled: waiting for sensor state";
    }

    private sealed class BorderedPanel : Panel
    {
        public Color BorderColor { get; set; } = UiBorderColor;

        public BorderedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            {
                return;
            }

            using var pen = new Pen(BorderColor);
            e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }
}
