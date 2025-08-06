using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GetSystemTemp
{
    static class Program
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
        static readonly List<float> gpuTemps = [];
        static readonly List<float> gpuPowers = [];
        static readonly List<float> gpuCoreFrequencies = [];
        static readonly List<float> gpuMemoryFrequencies = [];


        static NotifyIcon trayIcon = null!;
        static System.Threading.Timer logTimer = null!;

        [STAThread]
        static void Main()
        {
            c.Open();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            trayIcon = new NotifyIcon
            {
                Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico")),
                Visible = true,
                Text = "AutoTest Температуры"
            };

            trayIcon.ShowBalloonTip(3000,                               // длительность в мс
                                    "AutoTest",                         // заголовок
                                    "Мониторинг температур запущен",    // текст
                                    ToolTipIcon.Info);                  // иконка


            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Открыть лог", null, (_, _) => OpenLog());
            contextMenu.Items.Add("Выход", null, (_, _) => Exit());

            trayIcon.ContextMenuStrip = contextMenu;

            // Таймер логирования
            logTimer = new System.Threading.Timer(_ => ReportSystemInfo(), null, 0, 5000);

            Application.Run();
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "<Pending>")]
        static readonly string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        static readonly string exeDir = Path.GetDirectoryName(exePath)!;
        static readonly string logPath = Path.Combine(exeDir, "temps.log");


        private static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                          ((x & 0x0000ff00) << 8) +
                          ((x & 0x00ff0000) >> 8) +
                          ((x & 0xff000000) >> 24));
        }


        static void OpenLog()
        {
            try
            {
                if (!File.Exists(logPath))
                    File.WriteAllText(logPath, "");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{logPath}\"",
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии лога: {ex.Message}");
            }
        }

        static void Exit()
        {
            // Показ уведомления о завершении
            trayIcon.ShowBalloonTip(
                        3000,
                        "AutoTest",
                        "Мониторинг температур остановлен",
                        ToolTipIcon.Info);
            trayIcon.Visible = false;
            logTimer.Dispose();
            Application.Exit();
        }


        static void ReportSystemInfo()
        {
            try
            {
                cpuTemps.Clear();
                cpuPowers.Clear();
                gpuTemps.Clear();
                gpuPowers.Clear();
                gpuCoreFrequencies.Clear();
                gpuMemoryFrequencies.Clear();

                foreach (var hardware in c.Hardware)
                {
                    hardware.Update();

                    foreach (var sensor in hardware.Sensors)
                    {
                        try
                        {
                            if (sensor.Value is not float val || float.IsNaN(val))
                                continue;

                            switch (sensor.SensorType)
                            {
                                case SensorType.Temperature:
                                    if (hardware.HardwareType == HardwareType.Cpu && (sensor.Name.Contains("CPU Package") || sensor.Name.Contains("Tctl") || sensor.Name.Contains("Tdie")))
                                        cpuTemps.Add(val);
                                    if ((hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuIntel) &&
                                        (sensor.Name.Contains("GPU Hot Spot") || sensor.Name.Contains("GPU Core")))
                                        gpuTemps.Add(val);
                                    break;

                                case SensorType.Power:
                                    if (hardware.HardwareType == HardwareType.Cpu && (sensor.Name.Contains("CPU Package") || sensor.Name.Contains("Package")))
                                        cpuPowers.Add(val);
                                    if ((hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuIntel) &&
                                        (sensor.Name.Contains("GPU Package") || sensor.Name.Contains("GPU Power") || sensor.Name.Contains("Total")))
                                        gpuPowers.Add(val);
                                    break;

                                case SensorType.Voltage:
                                    if ((hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuIntel) &&
                                        (sensor.Name.Contains("GPU Core") || sensor.Name.Contains("VDD")))
                                        gpuPowers.Add(val);
                                    break;

                                case SensorType.Clock:
                                    if (sensor.Name.Contains("GPU Core"))
                                        gpuCoreFrequencies.Add(val);
                                    else if (sensor.Name.Contains("GPU Memory"))
                                        gpuMemoryFrequencies.Add(val);
                                    break;
                            }
                        }
                        catch { }
                    }
                }

                string timestr = DateTime.Now.ToString("HH:mm:ss");
                string line = $"[{timestr}] | " +
                              $"CPU Temp: {(cpuTemps.Any() ? cpuTemps.Average().ToString("F1") : "N/A")} °C | " +
                              $"CPU Power: {(cpuPowers.Any() ? cpuPowers.Average().ToString("F1") : "N/A")} W | " +
                              $"GPU Temp: {(gpuTemps.Any() ? gpuTemps.Average().ToString("F1") : "N/A")} °C | " +
                              $"GPU Power: {(gpuPowers.Any() ? gpuPowers.Average().ToString("F1") : "N/A")} W | " +
                              $"GPU Core Freq: {(gpuCoreFrequencies.Any() ? gpuCoreFrequencies.Average().ToString("F0") : "N/A")} MHz | " +
                              $"GPU Mem Freq: {(gpuMemoryFrequencies.Any() ? gpuMemoryFrequencies.Average().ToString("F0") : "N/A")} MHz";

                File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                File.AppendAllText("error.log", $"[{DateTime.Now}] Ошибка в ReportSystemInfo: {ex}\n");
            }
        }


    }
}