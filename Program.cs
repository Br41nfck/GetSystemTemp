using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Timer = System.Threading.Timer;

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
        static Timer logTimer = null!;

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
            logTimer = new Timer(_ => ReportSystemInfo(), null, 0, 5000);

            Application.Run();
        }

        static readonly string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        static readonly string exeDir = Path.GetDirectoryName(exePath)!;
        static readonly string logPath = Path.Combine(exeDir, "temps.log");

        public static DateTime GetMoscowTime()
        {
            const string RuNTPServer = "0.ru.pool.ntp.org"; // https://www.ntppool.org/ru/zone/ru

            byte[] ntpData = new byte[48];
            ntpData[0] = 0x1B;

            var addresses = Dns.GetHostEntry(RuNTPServer).AddressList;
            var ipEndPoint = new IPEndPoint(addresses[0], 123);

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(ipEndPoint);
            socket.Send(ntpData);
            socket.ReceiveTimeout = 3000;
            socket.Receive(ntpData);
            socket.Close();

            const byte serverReplyTime = 40;
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
            var networkDateTime = new DateTime(1900, 1, 1).AddMilliseconds((long)milliseconds);

            // Московское время — UTC+3, без учёта перехода на летнее
            return TimeZoneInfo.ConvertTimeFromUtc(networkDateTime, TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"));
        }

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
            cpuTemps.Clear();
            cpuPowers.Clear();
            gpuTemps.Clear();
            gpuPowers.Clear();
            gpuCoreFrequencies.Clear();
            gpuMemoryFrequencies.Clear();

            foreach (var hardware in c.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature &&
                            (sensor.Name.Contains("CPU Package") || sensor.Name.Contains("Tctl") || sensor.Name.Contains("Tdie")))
                        {
                            if (sensor.Value is float val)
                                cpuTemps.Add(val);
                        }

                        if (sensor.SensorType == SensorType.Power &&
                            (sensor.Name.Contains("CPU Package") || sensor.Name.Contains("Package")))
                        {
                            if (sensor.Value is float val)
                                cpuPowers.Add(val);
                        }
                    }
                }

                if (hardware.HardwareType == HardwareType.GpuAmd ||
                    hardware.HardwareType == HardwareType.GpuNvidia ||
                    hardware.HardwareType == HardwareType.GpuIntel)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature &&
                            (sensor.Name.Contains("GPU Hot Spot") || sensor.Name.Contains("GPU Core")))
                        {
                            if (sensor.Value is float val)
                                gpuTemps.Add(val);
                        }

                        if (sensor.SensorType == SensorType.Power &&
                            (sensor.Name.Contains("GPU Package") || sensor.Name.Contains("GPU Power") || sensor.Name.Contains("Total")))
                        {
                            if (sensor.Value is float val)
                                gpuPowers.Add(val);
                        }

                        if (sensor.SensorType == SensorType.Voltage &&
                            (sensor.Name.Contains("GPU Core") || sensor.Name.Contains("VDD")))
                        {
                            if (sensor.Value is float val)
                                gpuPowers.Add(val); // fallback
                        }

                        if (sensor.SensorType == SensorType.Clock)
                        {
                            float? value = sensor.Value;
                            if (value is float v)
                            {
                                if (sensor.Name.Contains("GPU Core"))
                                    gpuCoreFrequencies.Add(v);
                                else if (sensor.Name.Contains("GPU Memory"))
                                    gpuMemoryFrequencies.Add(v);
                            }
                        }
                    }
                }
            }

            // Формируем строки из полученных значений
            string cpuTempStr = cpuTemps.Count > 0 ? $"CPU Temp: {cpuTemps.Average():F1} °C" : "CPU Temp: N/A";
            string cpuPowerStr = cpuPowers.Count > 0 ? $"CPU Power: {cpuPowers.Average():F1} W" : "CPU Power: N/A";
            string gpuTempStr = gpuTemps.Count > 0 ? $"GPU Temp: {gpuTemps.Average():F1} °C" : "GPU Temp: N/A";
            string gpuPowerStr = gpuPowers.Count > 0 ? $"GPU Power: {gpuPowers.Average():F1} W" : "GPU Power: N/A";
            string gpuCoreFreqStr = gpuCoreFrequencies.Count > 0 ? $"GPU Core Freq: {gpuCoreFrequencies.Average():F0} MHz" : "GPU Core Freq: N/A";
            string gpuMemFreqStr = gpuMemoryFrequencies.Count > 0 ? $"GPU Mem Freq: {gpuMemoryFrequencies.Average():F0} MHz" : "GPU Mem Freq: N/A";

            string timestr = GetMoscowTime().ToString("dd-MM-yyyy HH:mm:ss");
            string line = $"[{timestr}] | {cpuTempStr} | {cpuPowerStr} | {gpuTempStr} | {gpuPowerStr} | {gpuCoreFreqStr} | {gpuMemFreqStr}";
            File.AppendAllText(logPath, line + Environment.NewLine);
        }

    }
}