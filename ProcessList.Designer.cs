namespace GetSystemTemp
{
    partial class ProcessList
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            processes_lb = new ListBox();
            search_tb = new TextBox();
            find_proc_l = new Label();
            sort_l = new Label();
            proc_sort_cb = new ComboBox();
            exit_b = new Button();
            refresh_b = new Button();
            sysproc_cb = new CheckBox();
            kill_proc_b = new Button();
            monitor_proc_b = new Button();
            label1 = new Label();
            cpu_cb = new CheckBox();
            gpu_cb = new CheckBox();
            ram_cb = new CheckBox();
            SuspendLayout();
            // 
            // processes_lb
            // 
            processes_lb.FormattingEnabled = true;
            processes_lb.Location = new Point(12, 58);
            processes_lb.Name = "processes_lb";
            processes_lb.Size = new Size(328, 289);
            processes_lb.TabIndex = 0;
            // 
            // search_tb
            // 
            search_tb.Location = new Point(12, 29);
            search_tb.Name = "search_tb";
            search_tb.Size = new Size(328, 23);
            search_tb.TabIndex = 1;
            search_tb.TextChanged += Search_tb_TextChanged;
            // 
            // find_proc_l
            // 
            find_proc_l.AutoSize = true;
            find_proc_l.Location = new Point(138, 11);
            find_proc_l.Name = "find_proc_l";
            find_proc_l.Size = new Size(123, 15);
            find_proc_l.TabIndex = 2;
            find_proc_l.Text = "Поиск по процессам";
            // 
            // sort_l
            // 
            sort_l.AutoSize = true;
            sort_l.Location = new Point(438, 11);
            sort_l.Name = "sort_l";
            sort_l.Size = new Size(73, 15);
            sort_l.TabIndex = 3;
            sort_l.Text = "Сортировка";
            // 
            // proc_sort_cb
            // 
            proc_sort_cb.FormattingEnabled = true;
            proc_sort_cb.Location = new Point(369, 29);
            proc_sort_cb.Name = "proc_sort_cb";
            proc_sort_cb.Size = new Size(192, 23);
            proc_sort_cb.TabIndex = 4;
            // 
            // exit_b
            // 
            exit_b.Location = new Point(500, 324);
            exit_b.Name = "exit_b";
            exit_b.Size = new Size(75, 23);
            exit_b.TabIndex = 6;
            exit_b.Text = "Выход";
            exit_b.UseVisualStyleBackColor = true;
            exit_b.Click += Exit_b_Click;
            // 
            // refresh_b
            // 
            refresh_b.Location = new Point(346, 324);
            refresh_b.Name = "refresh_b";
            refresh_b.Size = new Size(104, 23);
            refresh_b.TabIndex = 7;
            refresh_b.Text = "Обновить";
            refresh_b.UseVisualStyleBackColor = true;
            refresh_b.Click += Refresh_b_Click;
            // 
            // sysproc_cb
            // 
            sysproc_cb.AutoSize = true;
            sysproc_cb.Location = new Point(346, 245);
            sysproc_cb.Name = "sysproc_cb";
            sysproc_cb.Size = new Size(215, 19);
            sysproc_cb.TabIndex = 8;
            sysproc_cb.Text = "Отображать системные процессы";
            sysproc_cb.UseVisualStyleBackColor = true;
            // 
            // kill_proc_b
            // 
            kill_proc_b.Location = new Point(346, 295);
            kill_proc_b.Name = "kill_proc_b";
            kill_proc_b.Size = new Size(104, 23);
            kill_proc_b.TabIndex = 9;
            kill_proc_b.Text = "Завершить";
            kill_proc_b.UseVisualStyleBackColor = true;
            kill_proc_b.Click += Kill_proc_b_Click;
            // 
            // monitor_proc_b
            // 
            monitor_proc_b.Location = new Point(346, 266);
            monitor_proc_b.Name = "monitor_proc_b";
            monitor_proc_b.Size = new Size(104, 23);
            monitor_proc_b.TabIndex = 10;
            monitor_proc_b.Text = "Мониторинг";
            monitor_proc_b.UseVisualStyleBackColor = true;
            monitor_proc_b.Click += Monitor_proc_b_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(346, 58);
            label1.Name = "label1";
            label1.Size = new Size(96, 15);
            label1.TabIndex = 11;
            label1.Text = "Что логировать:";
            // 
            // cpu_cb
            // 
            cpu_cb.AutoSize = true;
            cpu_cb.Location = new Point(346, 76);
            cpu_cb.Name = "cpu_cb";
            cpu_cb.Size = new Size(49, 19);
            cpu_cb.TabIndex = 12;
            cpu_cb.Text = "CPU";
            cpu_cb.UseVisualStyleBackColor = true;
            // 
            // gpu_cb
            // 
            gpu_cb.AutoSize = true;
            gpu_cb.Location = new Point(346, 101);
            gpu_cb.Name = "gpu_cb";
            gpu_cb.Size = new Size(49, 19);
            gpu_cb.TabIndex = 13;
            gpu_cb.Text = "GPU";
            gpu_cb.UseVisualStyleBackColor = true;
            // 
            // ram_cb
            // 
            ram_cb.AutoSize = true;
            ram_cb.Location = new Point(346, 126);
            ram_cb.Name = "ram_cb";
            ram_cb.Size = new Size(52, 19);
            ram_cb.TabIndex = 14;
            ram_cb.Text = "RAM";
            ram_cb.UseVisualStyleBackColor = true;
            // 
            // ProcessList
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(587, 352);
            Controls.Add(ram_cb);
            Controls.Add(gpu_cb);
            Controls.Add(cpu_cb);
            Controls.Add(label1);
            Controls.Add(monitor_proc_b);
            Controls.Add(kill_proc_b);
            Controls.Add(sysproc_cb);
            Controls.Add(refresh_b);
            Controls.Add(exit_b);
            Controls.Add(proc_sort_cb);
            Controls.Add(sort_l);
            Controls.Add(find_proc_l);
            Controls.Add(search_tb);
            Controls.Add(processes_lb);
            Name = "ProcessList";
            Text = "Список процессов";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListBox processes_lb;
        private TextBox search_tb;
        private Label find_proc_l;
        private Label sort_l;
        private ComboBox proc_sort_cb;
        private Button exit_b;
        private Button refresh_b;
        private CheckBox sysproc_cb;
        private Button kill_proc_b;
        private Button monitor_proc_b;
        private Label label1;
        private CheckBox cpu_cb;
        private CheckBox gpu_cb;
        private CheckBox ram_cb;
    }
}