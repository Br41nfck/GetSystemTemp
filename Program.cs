// Утилита для логирования температуры и потребляемой мощности процессора и видеокарты в консоль или файл
// Utility for logging the temperature and power consumption of the processor and video card to the console or file

// Запускать с правами администратора! | Run as Admin!

// Libraries
using LibreHardwareMonitor.Hardware; // Check: https://www.nuget.org/packages/LibreHardwareMonitorLib/0.9.5-pre384
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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

    // Режим логирования | Logging mode
    static LoggingMode currentLoggingMode = LoggingMode.File;

    // Флаг для отслеживания состояния консоли | Flag for monitoring console state
    static bool consoleAllocated = false;

    // Массивы процессов для логирования | Arrays of processes for logging
    static readonly string[] CpuProcesses = ["<process_name_1>", "<process_name_2>", "<process_name_3>", "<process_name_4>", "<process_name_5>", "<process_name_6>"];
    static readonly string[] GpuProcesses = ["<process_name_1>", "<process_name_2>", "<process_name_3>", "<process_name_4>"];
    static readonly string[] RamProcesses = ["<process_name_4>", "<process_name_6>"];


    [STAThread]
    static void Main()
    {
        c.Open();

        // Асинхронный вызов ShowFanRpms
        ShowFanRpmsAsync(c).GetAwaiter().GetResult();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        trayIcon = new NotifyIcon
        {
            Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico")),
            Visible = true,
            Text = "AutoTest Температуры"
        };
        trayIcon.ShowBalloonTip(3000, "AutoTest", "Мониторинг температур запущен", ToolTipIcon.Info);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Открыть лог", null, (_, _) => OpenLog());
        contextMenu.Items.Add("Режим логирования", null, (sender, e) => { });
        contextMenu.Items.Add("Выход", null, (_, _) => Exit());
        trayIcon.ContextMenuStrip = contextMenu;

        CreateLoggingModeMenu();

        if (currentLoggingMode == LoggingMode.Console || currentLoggingMode == LoggingMode.Both) OpenConsole();

        logTimer = new System.Threading.Timer(_ => ReportSystemInfo(), null, 0, 5000);
        Application.Run();
    }

    static async Task ShowFanRpmsAsync(Computer c)
    {
        await Task.Run(() =>
        {
            StringBuilder sb = new();

            void UpdateHardware(IHardware hardware)
            {
                hardware.Update();

                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value > 0)
                    {
                        sb.AppendLine($"{sensor.Name}: {sensor.Value:F0} RPM");
                    }
                }

                foreach (var sub in hardware.SubHardware)
                {
                    UpdateHardware(sub);
                }
            }

            foreach (var hardware in c.Hardware)
            {
                UpdateHardware(hardware);
            }

            if (sb.Length > 0)
            {
                try
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now}] Fan RPMs:\n{sb}\n");

                    // Form On Top = fot
                    using Form fot = new();
                    fot.TopMost = true;

                    // Скрываем окно от пользователя
                    fot.StartPosition = FormStartPosition.Manual;
                    fot.Location = new Point(-2000, -2000);

                    fot.Show();
                    fot.Focus();
                    fot.BringToFront();

                    MessageBox.Show(sb.ToString(), "Вентиляторы");

                }
                catch { }
            }
            else MessageBox.Show("Датчики оборотов вентиляторов не найдены!", "Вентиляторы");

        });
    }

    static bool IsProcessRunning(params string[] processNames)
    {
        var processes = Process.GetProcesses();
        return processes.Any(p =>
        {
            try
            {
                return processNames.Any(name => p.ProcessName.Contains(name, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        });
    }

    static void CreateLoggingModeMenu()
    {
        var contextMenu = trayIcon.ContextMenuStrip;
        if (contextMenu == null) return;

        // Находим пункт "Режим логирования" | Found "Logging mode"
        var loggingMenuItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Режим логирования");
        // EN: .FirstOrDefault(item => item.Text == "Logging mode");

        if (loggingMenuItem == null) return;

        // Создаем подменю | Create submenu
        // EN: var fileItem = new ToolStripMenuItem("To File")
        var fileItem = new ToolStripMenuItem("В файл")
        {
            Checked = currentLoggingMode == LoggingMode.File
        };
        fileItem.Click += (_, _) => SetLoggingMode(LoggingMode.File);
        // EN: var consoleItem = new ToolStripMenuItem("To Console")
        var consoleItem = new ToolStripMenuItem("В консоль")
        {
            Checked = currentLoggingMode == LoggingMode.Console
        };
        consoleItem.Click += (_, _) => SetLoggingMode(LoggingMode.Console);
        // EN:  var bothItem = new ToolStripMenuItem("Both")
        var bothItem = new ToolStripMenuItem("В консоль и файл")
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

        // Обновляем галочки в меню | Update checks in logging menu
        UpdateLoggingMenuChecks();

        // Если переключаемся на режим с консолью, открываем консоль | Else switch to "In Console", open the console
        if ((mode == LoggingMode.Console || mode == LoggingMode.Both) && (oldMode == LoggingMode.File)) OpenConsole();

        // Если переключаемся на режим "В файл", закрываем консоль | Else switch to "In file", close the console
        else if (mode == LoggingMode.File && (oldMode == LoggingMode.Console || oldMode == LoggingMode.Both)) CloseConsole();
        // EN: trayIcon.ShowBalloonTip(2000, "AutoTest", $"Logging mode changed: {GetModeDescription(mode)}", ToolTipIcon.Info);
        trayIcon.ShowBalloonTip(2000, "AutoTest", $"Режим логирования изменен: {GetModeDescription(mode)}", ToolTipIcon.Info);
    }

    static void OpenConsole()
    {
        try
        {
            // Пытаемся открыть консоль | Try open the console
            if (!AttachConsole(-1)) // -1 означает родительский процесс | -1 means parent process
            {
                // Если не удалось присоединиться, создаем новую консоль | If cannot attach to console, create new one
                if (AllocConsole())
                {
                    consoleAllocated = true;
                    // Немного ждем, чтобы консоль инициализировалась | A little wait for initialization
                    Thread.Sleep(100);
                }
            }
            else consoleAllocated = true;

        }
        catch (Exception ex)
        {
            // В случае ошибки показываем уведомление | Show notification in tray
            // EN: trayIcon.ShowBalloonTip(3000, "AutoTest", $"Error opening console: {ex.Message}", ToolTipIcon.Warning);
            trayIcon.ShowBalloonTip(3000, "AutoTest", $"Ошибка открытия консоли: {ex.Message}", ToolTipIcon.Warning);
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
            // EN: trayIcon.ShowBalloonTip(3000, "AutoTest", $"Error closing console: {ex.Message}", ToolTipIcon.Warning);
            trayIcon.ShowBalloonTip(3000, "AutoTest", $"Ошибка закрытия консоли: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    static void UpdateLoggingMenuChecks()
    {
        ContextMenuStrip? contextMenu = trayIcon.ContextMenuStrip;
        if (contextMenu == null) return;
        // EN: ToolStripMenuItem? loggingMenuItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Logging mode");
        ToolStripMenuItem? loggingMenuItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Режим логирования");

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
            LoggingMode.File => "В файл", // "To File"
            LoggingMode.Console => "В консоль", // "To console"
            LoggingMode.Both => "В консоль и файл", // "Both"
            _ => "В файл" // "To File"
        };
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
    static readonly string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
    static readonly string exeDir = Path.GetDirectoryName(exePath)!;
    static readonly string logPath = Path.Combine(exeDir, "temps.log"); // Файл для сохранения логов | Save Log File

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
            // EN: MessageBox.Show($"Error opening log: {ex.Message}");
            MessageBox.Show($"Ошибка при открытии лога: {ex.Message}");
        }
    }

    static void Exit()
    {
        // EN: trayIcon.ShowBalloonTip(3000, "AutoTest", "Temperature monitoring has been stopped", ToolTipIcon.Info);
        trayIcon.ShowBalloonTip(3000, "AutoTest", "Мониторинг температур остановлен", ToolTipIcon.Info);
        trayIcon.Visible = false;
        logTimer.Dispose();

        // Закрываем консоль при выходе, если она была открыта | Close the console on exit if it was open
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

            int gpuIndex = 1;
            bool isAMDProcessor = false;
            float? memoryUsed = null;
            float? memoryAvailable = null;

            foreach (var hardware in c.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    // Проверяем, является ли процессор AMD | Check if the processor is AMD
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
                                    List<ISensor> clockSensors = [.. hardware.Sensors.Where(s => s.SensorType == SensorType.Clock)];
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

                else if (hardware.HardwareType == HardwareType.Memory)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        try
                        {
                            if (sensor.Value is not float val || float.IsNaN(val)) continue;

                            if (sensor.SensorType == SensorType.Data)
                            {
                                string name = sensor.Name.ToLower();
                                if (name.Contains("used")) memoryUsed = val;
                                else if (name.Contains("available")) memoryAvailable = val;
                            }

                        }
                        catch { }

                    }
                }
                else if (hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel)
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
                                        gpuTempsWithNames.Add((currentGpuId, val + (float)6.7)); // Корректировка погрешностей для GPU | Correction of errors for GPU
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

            string timestr = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");
            List<string> logLines = [];

            if (!legendWritten && gpuLegend.Count != 0)
            {
                List<string> legendEntries = [];
                foreach (var (id, fullName, isIntegrated) in gpuLegend)
                {
                    // EN: string description = isIntegrated ? "intergated" : fullName;
                    string description = isIntegrated ? "встроенная" : fullName;
                    legendEntries.Add($"{id} ({description}) - {fullName}");
                }
                // EN: logLines.Add("Designations: ");
                logLines.Add("Обозначения: ");
                logLines.Add(string.Join(Environment.NewLine, legendEntries));
                logLines.Add("\n");
                legendWritten = true;
            }


            float cpuTempValue = cpuTemps.Count != 0 ? cpuTemps.Average() : float.NaN;
            float cpuFreqValue = cpuFrequencies.Count != 0 ? cpuFrequencies.Average() : float.NaN;


            // Корректировка погрешностей для процессоров AMD | Correction of errors for AMD processors
            if (isAMDProcessor)
            {
                // Температура | Temperature
                // if (!float.IsNaN(cpuTempValue)) cpuTempValue += 6;
                // Частота | Frequency
                if (!float.IsNaN(cpuFreqValue)) cpuFreqValue += 1350;
            }

            if (IsProcessRunning(CpuProcesses))
            {
                int cpuIndex = 1;
                foreach (var hw in c.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
                {
                    hw.Update();

                    float temp = float.NaN;
                    float power = float.NaN;
                    float freq = float.NaN;

                    // Temperature (priority: Package -> Tctl -> Tdie)
                    var tempSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Package"))
                                  ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Tctl"))
                                  ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Tdie"));
                    if (tempSensor?.Value is float tVal && !float.IsNaN(tVal))
                        temp = tVal;

                    // Power (priority: CPU Package -> Package)
                    var powerSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("CPU Package"))
                                   ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("Package"));
                    if (powerSensor?.Value is float pVal && !float.IsNaN(pVal))
                        power = pVal;

                    // Clock (average of all Core sensors)
                    var coreClocks = hw.Sensors
                        .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core") && !s.Name.Contains("Bus"))
                        .Select(s => s.Value)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .ToList();
                    if (coreClocks.Count > 0)
                        freq = coreClocks.Average();

                    string cpuLine = $"[{timestr}] CPU#{cpuIndex} | Temp: {FormatTempValue(temp),-7} | Power: {FormatPowerValue(power),-9} | Avg Freq: {FormatFreqValue(freq)}";
                    logLines.Add(cpuLine);

                    cpuIndex++;
                }
            }

            if (IsProcessRunning(RamProcesses))
            {
                if (IsProcessRunning(RamProcesses))
                {
                    if (memoryUsed.HasValue && memoryAvailable.HasValue)
                    {
                        float totalMemory = memoryUsed.Value + memoryAvailable.Value;
                        float usedPercent = (memoryUsed.Value / totalMemory) * 100f;
                        float freePercent = 100f - usedPercent;
                        string ramLine = $"[{timestr}] RAM   | Used: {FormatRamValue(memoryUsed.Value)} ({usedPercent:F0}%) | Free: {FormatRamValue(memoryAvailable.Value)} ({freePercent:F0}%) | Total: {FormatRamValue(totalMemory)}";
                        logLines.Add(ramLine);
                    }
                }
            }

            if (IsProcessRunning(GpuProcesses))
            {
                foreach (var hw in c.Hardware.Where(h =>
                    h.HardwareType == HardwareType.GpuNvidia ||
                    h.HardwareType == HardwareType.GpuAmd ||
                    h.HardwareType == HardwareType.GpuIntel))
                {
                    hw.Update();

                    float temp = float.NaN;
                    float power = float.NaN;

                    // Temperature (priority: Hot Spot -> Core)
                    var tempSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Hot Spot"))
                                  ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Core"));
                    if (tempSensor?.Value is float tVal && !float.IsNaN(tVal))
                        temp = tVal;

                    // Power (priority: Package -> GPU Power -> Total)
                    var powerSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("Package"))
                                   ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("GPU Power"))
                                   ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("Total"));
                    if (powerSensor?.Value is float pVal && !float.IsNaN(pVal))
                        power = pVal;

                    string gpuLine = $"[{timestr}] GPU#{gpuIndex} | Temp: {FormatTempValue(temp),-7} | Power: {FormatPowerValue(power),-9}";
                    logLines.Add(gpuLine);

                    gpuIndex++;
                }
            }

            logLines.Add("\n");

            string logEntry = string.Join(Environment.NewLine, logLines);

            // Логируем в зависимости от выбранного режима | Logging depending on the selected mode
            LogOutput(logEntry);
        }
        catch (Exception ex)
        {
            // EN: string errorLine = $"[{DateTime.Now}] Error in ReportSystemInfo: {ex}\n";
            string errorLine = $"[{DateTime.Now}] Ошибка в ReportSystemInfo: {ex}\n";
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
                    // Если консоль не открыта, выводим в файл как запасной вариант | If the console is not open, output to a file as a fallback
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
                    // В случае ошибки записи в файл, выводим в консоль если она доступна | In case of an error writing to the file, output it to the console if it is available
                    if (consoleAllocated)
                    {
                        // EN: Console.Error.WriteLine($"Error writing to file: {ex.Message}");
                        Console.Error.WriteLine($"Ошибка записи в файл: {ex.Message}");
                    }
                }
                break;

            case LoggingMode.Both:
                // Выводим в консоль если доступна | Output it to the console if it is available
                if (consoleAllocated)
                {
                    if (!isError)
                        Console.Write(message);
                    else
                        Console.Error.Write(message);
                }

                // Записываем в файл | Write to file
                try
                {
                    File.AppendAllText(logPath, message);
                }
                catch (Exception ex)
                {
                    if (consoleAllocated)
                    {
                        // EN: Console.Error.WriteLine($"Error writing to file: {ex.Message}");
                        Console.Error.WriteLine($"Ошибка записи в файл: {ex.Message}");
                    }
                }
                break;
        }
    }

    static string FormatTempValue(float value)
    {
        if (float.IsNaN(value))
            return "N/A °C";
        return $"{value:F1}".Replace(".", ",") + " °C";
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

    static string FormatRamValue(float value)
    {
        if (float.IsNaN(value))
            return "N/A MB";
        return $"{value * 1024:F0} MB"; // GB to MB
    }

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

// Перечисление для режимов логирования | Enumeration for logging modes
enum LoggingMode
{
    File,    // Только в файл | File Only
    Console, // Только в консоль | Console Only
    Both     // В консоль и файл | Both
}
