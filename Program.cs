using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GetSystemTemp;

static partial class Program
{
    static readonly Computer c = new()
    {
        IsGpuEnabled = true,
        IsCpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true,
        IsStorageEnabled = true,
    };

    static readonly List<float> cpuTemps = [];
    static readonly List<float> cpuPowers = [];
    static readonly List<float> cpuFrequencies = [];
    static readonly List<(string name, float temp)> gpuTempsWithNames = [];
    static readonly List<(string name, float power)> gpuPowersWithNames = [];

    static bool legendWritten = false;
    static readonly List<(string id, string fullName, bool isIntegrated)> gpuLegend = [];

    static NotifyIcon trayIcon = null!;
    static System.Threading.Timer logTimer = null!;

    // ����� �����������
    static LoggingMode currentLoggingMode = LoggingMode.File;

    // ���� ��� ������������ ��������� �������
    static bool consoleAllocated = false;

    [STAThread]
    static void Main(string[] args)
    {
        // ��������� ��������� ��������� ������
        ParseCommandLineArgs(args);

        c.Open();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        trayIcon = new NotifyIcon
        {
            Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico")),
            Visible = true,
            Text = "AutoTest �����������"
        };

        trayIcon.ShowBalloonTip(3000,
                                "AutoTest",
                                "���������� ���������� �������",
                                ToolTipIcon.Info);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("������� ���", null, (_, _) => OpenLog());
        contextMenu.Items.Add("����� �����������", null, (sender, e) => { }); // ������ ����������
        contextMenu.Items.Add("�����", null, (_, _) => Exit());

        trayIcon.ContextMenuStrip = contextMenu;

        // ������� ������� ��� ������� �����������
        CreateLoggingModeMenu();

        // ���� ������ ����� � ��������, ��������� �������
        if (currentLoggingMode == LoggingMode.Console || currentLoggingMode == LoggingMode.Both)
        {
            OpenConsole();
        }

        // ������ �����������
        logTimer = new System.Threading.Timer(_ => ReportSystemInfo(), null, 0, 5000);

        Application.Run();
    }

    static void ParseCommandLineArgs(string[] args)
    {
        if (args.Length > 0)
        {
            string mode = args[0].ToLower();
            currentLoggingMode = mode switch
            {
                "console" => LoggingMode.Console,
                "file" => LoggingMode.File,
                "both" => LoggingMode.Both,
                _ => LoggingMode.File
            };
        }
    }

    static void CreateLoggingModeMenu()
    {
        var contextMenu = trayIcon.ContextMenuStrip;
        if (contextMenu == null) return;

        // ������� ����� "����� �����������"
        var loggingMenuItem = contextMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text == "����� �����������");

        if (loggingMenuItem == null) return;

        // ������� �������
        var fileItem = new ToolStripMenuItem("� ����")
        {
            Checked = currentLoggingMode == LoggingMode.File
        };
        fileItem.Click += (_, _) => SetLoggingMode(LoggingMode.File);

        var consoleItem = new ToolStripMenuItem("� �������")
        {
            Checked = currentLoggingMode == LoggingMode.Console
        };
        consoleItem.Click += (_, _) => SetLoggingMode(LoggingMode.Console);

        var bothItem = new ToolStripMenuItem("� ������� � ����")
        {
            Checked = currentLoggingMode == LoggingMode.Both
        };
        bothItem.Click += (_, _) => SetLoggingMode(LoggingMode.Both);

        loggingMenuItem.DropDownItems.Add(fileItem);
        loggingMenuItem.DropDownItems.Add(consoleItem);
        loggingMenuItem.DropDownItems.Add(bothItem);
    }

    static void SetLoggingMode(LoggingMode mode)
    {
        LoggingMode oldMode = currentLoggingMode;
        currentLoggingMode = mode;

        // ��������� ������� � ����
        UpdateLoggingMenuChecks();

        // ���� ������������� �� ����� � ��������, ��������� �������
        if ((mode == LoggingMode.Console || mode == LoggingMode.Both) && (oldMode == LoggingMode.File)) OpenConsole();

        // ���� ������������� �� ����� "� ����", ��������� �������
        else if (mode == LoggingMode.File && (oldMode == LoggingMode.Console || oldMode == LoggingMode.Both)) CloseConsole();

        trayIcon.ShowBalloonTip(2000, "AutoTest", $"����� ����������� ������� ��: {GetModeDescription(mode)}", ToolTipIcon.Info);
    }

    static void OpenConsole()
    {
        try
        {
            // �������� ������� �������
            if (!AttachConsole(-1)) // -1 �������� ������������ �������
            {
                // ���� �� ������� ��������������, ������� ����� �������
                if (AllocConsole())
                {
                    consoleAllocated = true;
                    // ������� ����, ����� ������� ������������������
                    Thread.Sleep(100);
                }
            }
            else
            {
                consoleAllocated = true;
            }
        }
        catch (Exception ex)
        {
            // � ������ ������ ���������� �����������
            trayIcon.ShowBalloonTip(3000, "AutoTest", $"������ �������� �������: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    static void CloseConsole()
    {
        try
        {
            if (consoleAllocated)
            {
                FreeConsole();
                consoleAllocated = false;
            }
        }
        catch (Exception ex)
        {
            trayIcon.ShowBalloonTip(3000, "AutoTest", $"������ �������� �������: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    static void UpdateLoggingMenuChecks()
    {
        ContextMenuStrip? contextMenu = trayIcon.ContextMenuStrip;
        if (contextMenu == null) return;

        ToolStripMenuItem? loggingMenuItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "����� �����������");

        if (loggingMenuItem?.DropDownItems != null)
        {
            for (int i = 0; i < loggingMenuItem.DropDownItems.Count; i++)
            {
                if (loggingMenuItem.DropDownItems[i] is ToolStripMenuItem menuItem) menuItem.Checked = false;
            }

            int selectedIndex = currentLoggingMode switch
            {
                LoggingMode.File => 0,
                LoggingMode.Console => 1,
                LoggingMode.Both => 2,
                _ => 0
            };

            if (selectedIndex < loggingMenuItem.DropDownItems.Count && loggingMenuItem.DropDownItems[selectedIndex] is ToolStripMenuItem selectedMenuItem)
            {
                selectedMenuItem.Checked = true;
            }
        }
    }

    static string GetModeDescription(LoggingMode mode)
    {
        return mode switch
        {
            LoggingMode.File => "� ����",
            LoggingMode.Console => "� �������",
            LoggingMode.Both => "� ������� � ����",
            _ => "� ����"
        };
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
    static readonly string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
    static readonly string exeDir = Path.GetDirectoryName(exePath)!;
    static readonly string logPath = Path.Combine(exeDir, "temps.log");

    static void OpenLog()
    {
        try
        {
            if (!File.Exists(logPath)) File.WriteAllText(logPath, "");

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{logPath}\"",
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"������ ��� �������� ����: {ex.Message}");
        }
    }

    static void Exit()
    {
        trayIcon.ShowBalloonTip(3000, "AutoTest", "���������� ���������� ����������", ToolTipIcon.Info);
        trayIcon.Visible = false;
        logTimer.Dispose();

        // ��������� ������� ��� ������, ���� ��� ���� �������
        if (consoleAllocated)
        {
            FreeConsole();
            consoleAllocated = false;
        }

        Application.Exit();
    }

    static void ReportSystemInfo()
    {
        try
        {
            cpuTemps.Clear();
            cpuPowers.Clear();
            cpuFrequencies.Clear();
            gpuTempsWithNames.Clear();
            gpuPowersWithNames.Clear();

            if (!legendWritten) gpuLegend.Clear();

            int gpuIndex = 0;
            bool isAMDProcessor = false;

            foreach (var hardware in c.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    // ���������, �������� �� ��������� AMD
                    if (hardware.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                    {
                        isAMDProcessor = true;
                    }

                    foreach (var sensor in hardware.Sensors)
                    {
                        try
                        {
                            if (sensor.Value is not float val || float.IsNaN(val))
                                continue;

                            switch (sensor.SensorType)
                            {
                                case SensorType.Temperature:
                                    if (sensor.Name.Contains("CPU Package") || sensor.Name.Contains("Tctl") || sensor.Name.Contains("Tdie"))
                                        cpuTemps.Add(val);
                                    break;

                                case SensorType.Power:
                                    if (sensor.Name.Contains("CPU Package") || sensor.Name.Contains("Package"))
                                        cpuPowers.Add(val);
                                    break;

                                case SensorType.Clock:
                                    var clockSensors = hardware.Sensors.Where(s => s.SensorType == SensorType.Clock).ToList();
                                    List<float> coreClocks = [.. clockSensors.Where(s => s.Name.Contains("Core") && !s.Name.Contains("Bus")).Select(s => s.Value).Where(v => v.HasValue).Select(v => v!.Value)];
                                    if (coreClocks.Count != 0)
                                    {
                                        float averageClock = coreClocks.Average();
                                        cpuFrequencies.Add(averageClock);
                                    }
                                    break;
                            }
                        }
                        catch { }
                    }
                }
                else if (hardware.HardwareType == HardwareType.GpuNvidia ||
                         hardware.HardwareType == HardwareType.GpuAmd ||
                         hardware.HardwareType == HardwareType.GpuIntel)
                {
                    if (!legendWritten)
                    {
                        bool isIntegrated = hardware.HardwareType == HardwareType.GpuIntel;
                        string gpuId = $"GPU#{gpuIndex}";
                        gpuLegend.Add((gpuId, hardware.Name, isIntegrated));
                    }

                    string currentGpuId = $"GPU#{gpuIndex}";

                    foreach (var sensor in hardware.Sensors)
                    {
                        try
                        {
                            if (sensor.Value is not float val || float.IsNaN(val))
                                continue;

                            switch (sensor.SensorType)
                            {
                                case SensorType.Temperature:
                                    if (sensor.Name.Contains("GPU Hot Spot") || sensor.Name.Contains("GPU Core"))
                                        gpuTempsWithNames.Add((currentGpuId, val));
                                    break;

                                case SensorType.Power:
                                    if (sensor.Name.Contains("GPU Package") || sensor.Name.Contains("GPU Power") || sensor.Name.Contains("Total"))
                                        gpuPowersWithNames.Add((currentGpuId, val));
                                    break;
                            }
                        }
                        catch { }
                    }

                    gpuIndex++;
                }
            }

            string timestr = DateTime.Now.ToString("HH:mm:ss");
            List<string> logLines = [];

            if (!legendWritten && gpuLegend.Count != 0)
            {
                List<string> legendEntries = [];
                foreach (var (id, fullName, isIntegrated) in gpuLegend)
                {
                    string description = isIntegrated ? "����������" : fullName;
                    legendEntries.Add($"{id} ({description}) - {fullName}");
                }
                logLines.Add("�����������: ");
                logLines.Add(string.Join(Environment.NewLine, legendEntries));
                logLines.Add("\n");
                legendWritten = true;
            }

            // ������������� ��� AMD �����������
            float cpuTempValue = cpuTemps.Count != 0 ? cpuTemps.Average() : float.NaN;
            float cpuFreqValue = cpuFrequencies.Count != 0 ? cpuFrequencies.Average() : float.NaN;

            if (isAMDProcessor)
            {
                if (!float.IsNaN(cpuTempValue))
                    cpuTempValue += 5;

                if (!float.IsNaN(cpuFreqValue))
                    cpuFreqValue += 2050;
            }

            string cpuTemp = FormatTempValue(cpuTempValue);
            string cpuPower = FormatPowerValue(cpuPowers.Count != 0 ? cpuPowers.Average() : float.NaN);
            string cpuFreq = FormatFreqValue(cpuFreqValue);

            string cpuLine = $"[{timestr}] CPU    | Temp: {cpuTemp,-7} | CPU Power: {cpuPower,-9} | CPU Freq (avg): {cpuFreq}";
            logLines.Add(cpuLine);

            for (int i = 0; i < gpuLegend.Count; i++)
            {
                var (id, fullName, isIntegrated) = gpuLegend[i];
                var temps = gpuTempsWithNames.Where(x => x.name == id).Select(x => x.temp);
                var powers = gpuPowersWithNames.Where(x => x.name == id).Select(x => x.power);

                string gpuTemp = FormatTempValue(temps.Any() ? temps.Average() : float.NaN);
                string gpuPower = FormatPowerValue(powers.Any() ? powers.Average() : float.NaN);

                string gpuLine = $"[{timestr}] {id,-6} | Temp: {gpuTemp,-7} | Power: {gpuPower,-8}";

                logLines.Add(gpuLine);
            }

            logLines.Add("\n");

            string logEntry = string.Join(Environment.NewLine, logLines);

            // �������� � ����������� �� ���������� ������
            LogOutput(logEntry);
        }
        catch (Exception ex)
        {
            string errorLine = $"[{DateTime.Now}] ������ � ReportSystemInfo: {ex}\n";
            LogOutput(errorLine, isError: true);
        }
    }

    static void LogOutput(string message, bool isError = false)
    {
        switch (currentLoggingMode)
        {
            case LoggingMode.Console:
                if (consoleAllocated)
                {
                    if (!isError)
                        Console.Write(message);
                    else
                        Console.Error.Write(message);
                }
                else
                {
                    // ���� ������� �� �������, ������� � ���� ��� �������� �������
                    try
                    {
                        File.AppendAllText(logPath, message);
                    }
                    catch { }
                }
                break;

            case LoggingMode.File:
                try
                {
                    File.AppendAllText(logPath, message);
                }
                catch (Exception ex)
                {
                    // � ������ ������ ������ � ����, ������� � ������� ���� ��� ��������
                    if (consoleAllocated)
                    {
                        Console.Error.WriteLine($"������ ������ � ����: {ex.Message}");
                    }
                }
                break;

            case LoggingMode.Both:
                // ������� � ������� ���� ��������
                if (consoleAllocated)
                {
                    if (!isError)
                        Console.Write(message);
                    else
                        Console.Error.Write(message);
                }

                // ���������� � ����
                try
                {
                    File.AppendAllText(logPath, message);
                }
                catch (Exception ex)
                {
                    if (consoleAllocated)
                    {
                        Console.Error.WriteLine($"������ ������ � ����: {ex.Message}");
                    }
                }
                break;
        }
    }

    static string FormatTempValue(float value)
    {
        if (float.IsNaN(value))
            return "N/A �C";
        return $"{value:F1}".Replace(".", ",") + " �C";
    }

    static string FormatPowerValue(float value)
    {
        if (float.IsNaN(value))
            return "N/A W";
        return $"{value:F1}".Replace(".", ",") + " W";
    }

    static string FormatFreqValue(float value)
    {
        if (float.IsNaN(value))
            return "N/A MHz";
        return $"{value:F0} MHz";
    }

    // ����������� ������� Win32 API ��� ������ � ��������
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int dwProcessId);
}

// ������������ ��� ������� �����������
enum LoggingMode
{
    File,    // ������ � ����
    Console, // ������ � �������
    Both     // � ������� � ����
}