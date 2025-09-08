using System.Diagnostics;

namespace GetSystemTemp
{
    public partial class ProcessList : Form
    {
        public ProcessList()
        {
            InitializeComponent();
            SortCbInit();
            ActiveProcList();

            sysproc_cb.CheckedChanged += Sysproc_cb_CheckedChanged;
            search_tb.TextChanged += Search_tb_TextChanged;
            proc_sort_cb.SelectedIndexChanged += Proc_sort_cb_SelectedIndexChanged;
        }

        private void SortCbInit()
        {
            proc_sort_cb.Items.Clear();
            proc_sort_cb.Items.AddRange(["По PID", "По названию", "По длительности (CPU Time)", "По дате запуска"]);
            proc_sort_cb.SelectedIndex = 1; // по умолчанию - сортировка по имени
        }

        public void ActiveProcList()
        {
            processes_lb.Items.Clear();
            string filter = search_tb.Text.Trim().ToLower();
            var allProc = Process.GetProcesses();

            // фильтрация
            var filtered = allProc.Where(proc =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        if (!proc.ProcessName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) &&
                            !proc.Id.ToString().Contains(filter))
                        {
                            return false;
                        }
                    }

                    if (!sysproc_cb.Checked && proc.SessionId == 0)
                        return false;

                    return true;
                }
                catch
                {
                    return false;
                }
            });

            // сортировка
            IEnumerable<Process> sorted = (proc_sort_cb.SelectedItem?.ToString()) switch
            {
                "По PID" => filtered.OrderBy(p => p.Id),
                "По названию" => filtered.OrderBy(p => p.ProcessName),
                "По длительности (CPU Time)" => filtered.OrderByDescending(p =>
                                    {
                                        try { return p.TotalProcessorTime; }
                                        catch { return TimeSpan.Zero; }
                                    }),
                "По дате запуска" => filtered.OrderBy(p =>
                    {
                        try { return p.StartTime; }
                        catch { return DateTime.MinValue; }
                    }),
                _ => filtered.OrderBy(p => p.ProcessName),
            };

            // вывод
            foreach (var proc in sorted)
            {
                try
                {
                    string procInfo = proc.SessionId == 0 ? $"(PID: {proc.Id}) [SYSTEM] {proc.ProcessName}" : $"(PID: {proc.Id}) {proc.ProcessName}";
                    processes_lb.Items.Add(procInfo);
                }
                catch { }
            }
        }

        private void Sysproc_cb_CheckedChanged(object? sender, EventArgs e) => ActiveProcList();
        private void Search_tb_TextChanged(object? sender, EventArgs e) => ActiveProcList();
        private void Refresh_b_Click(object sender, EventArgs e) => ActiveProcList();
        private void Proc_sort_cb_SelectedIndexChanged(object? sender, EventArgs e) => ActiveProcList();

        private void Kill_proc_b_Click(object sender, EventArgs e)
        {
            if (processes_lb.SelectedItem == null) return;

            string? selected = processes_lb.SelectedItem.ToString();

            try
            {
                int pidStart = selected!.IndexOf("(PID: ") + 6;
                int pidEnd = selected.IndexOf(')', pidStart);
                string pidStr = selected[pidStart..pidEnd];

                if (int.TryParse(pidStr, out int pid))
                {
                    Process proc = Process.GetProcessById(pid);
                    proc.Kill();
                    proc.WaitForExit();
                    ActiveProcList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при завершении процесса:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Exit_b_Click(object sender, EventArgs e) => Application.Exit();

        private void Monitor_proc_b_Click(object sender, EventArgs e)
        {
            if (processes_lb.SelectedItem == null)
            {
                MessageBox.Show("Выберите процесс для мониторинга.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selected = processes_lb.SelectedItem.ToString()!;

            // Имя процесса
            string processName;
            int pidEnd = selected.IndexOf(')');
            if (pidEnd >= 0) processName = selected[(pidEnd + 2)..];
            else processName = selected;

            // Параметры мониторинга (чекбоксы)
            bool logCpu = cpu_cb.Checked;
            bool logGpu = gpu_cb.Checked;
            bool logRam = ram_cb.Checked;

            try
            {
                MonitoringManager.StartMonitoring(processName, logCpu, logGpu, logRam);
                MessageBox.Show($"Мониторинг процесса '{processName}' запущен.\nФайл мониторинга: {MonitoringManager.TargetLogFile}", "Мониторинг запущен", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить мониторинг: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
