using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LavaBox.PackHelper
{
    public sealed partial class MainForm : Form
    {
        private readonly PackRelease release;
        private readonly string demoFolder;
        private readonly ComboBox games = new ComboBox();
        private readonly TextBox target = new TextBox();
        private readonly Label status = new Label();
        private readonly Label note = new Label();
        private readonly CheckBox closed = new CheckBox();
        private readonly Button install = new Button();
        private readonly Button folder = new Button();
        private readonly Button log = new Button();
        private readonly Button rescan = new Button();
        private readonly Label summary = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Color muted = Color.FromArgb(91, 101, 117);
        private readonly Color accent = Color.FromArgb(216, 104, 38);
        private bool busy;

        public MainForm(PackRelease release, string demoFolder)
        {
            this.release = release; this.demoFolder = demoFolder;
            Text = "PackHelper - " + release.packVersion;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ClientSize = new Size(820, 684); MinimumSize = Size; MaximumSize = Size;
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; BackColor = Color.FromArgb(255, 250, 245);
            Font = new Font("Segoe UI", 10F); AutoScaleMode = AutoScaleMode.Dpi;

            var header = new Panel { BackColor = Color.FromArgb(255, 235, 215), Dock = DockStyle.Top, Height = 132 };
            header.Controls.Add(LabelAt("Установка ресурспака", 28, 25, 750, 43, new Font("Segoe UI", 23F, FontStyle.Bold), Color.FromArgb(105, 57, 30)));
            header.Controls.Add(LabelAt("LavaResurs " + release.packVersion + "   /   " + (release.size / 1048576.0).ToString("0.0") + " МБ внутри программы", 31, 83, 750, 24, Font, Color.FromArgb(131, 87, 57)));
            Controls.Add(header);

            Controls.Add(LabelAt("Закройте Minecraft", 30, 150, 750, 28, new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor));
            closed.Text = "Я полностью закрыл игру. Лаунчер можно оставить открытым.";
            closed.SetBounds(32, 184, 754, 28); closed.CheckedChanged += delegate { RefreshInstall(); }; Controls.Add(closed);

            Controls.Add(LabelAt("Выберите свою сборку", 30, 226, 750, 28, new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor));
            games.DropDownStyle = ComboBoxStyle.DropDownList; games.SetBounds(32, 263, 754, 31);
            games.SelectedIndexChanged += delegate { ShowCandidate(); }; Controls.Add(games);
            SetupButton(folder, "Выбрать папку…", 32, 307, 180); folder.Click += delegate { ChooseFolder(); };
            SetupButton(log, "Определить по latest.log…", 224, 307, 270); log.Click += delegate { ChooseLog(); };
            SetupButton(rescan, "Найти снова", 506, 307, 160); rescan.Click += async delegate { await Scan(); };
            note.SetBounds(32, 351, 754, 43); note.ForeColor = muted; note.Font = new Font("Segoe UI", 9F); Controls.Add(note);

            Controls.Add(LabelAt("Установите ресурспак", 30, 406, 750, 28, new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor));
            target.ReadOnly = true; target.Multiline = true; target.BorderStyle = BorderStyle.FixedSingle;
            target.SetBounds(32, 443, 754, 55); target.BackColor = Color.White; target.Font = new Font("Segoe UI", 9F); Controls.Add(target);
            install.Text = "Установить ресурспак"; install.SetBounds(32, 516, 350, 48);
            install.BackColor = accent; install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat;
            install.FlatAppearance.BorderSize = 0; install.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            install.Click += async delegate { await Install(); }; Controls.Add(install);
            summary.Text = "Пак уже в программе.\nПосле установки зайдите на сервер.";
            summary.SetBounds(404, 520, 382, 48); summary.ForeColor = muted; summary.Font = new Font("Segoe UI", 9F); Controls.Add(summary);
            progress.SetBounds(32, 577, 754, 5); progress.Style = ProgressBarStyle.Marquee; progress.Visible = false; Controls.Add(progress);
            status.SetBounds(32, 591, 754, 45); status.ForeColor = muted; status.Font = new Font("Segoe UI", 10F); Controls.Add(status);
            var source = new LinkLabel { Text = "Открытый код на GitHub", LinkColor = muted, AutoSize = true, Location = new Point(32, 652) };
            source.LinkClicked += delegate { OpenUrl(release.sourceUrl); }; Controls.Add(source);
            var help = new LinkLabel { Text = "Помощь в Discord", LinkColor = muted, AutoSize = true, Location = new Point(272, 652) };
            help.LinkClicked += delegate { OpenUrl(release.discordUrl); }; Controls.Add(help);
            Controls.Add(LabelAt(demoFolder == null ? "PackHelper 1.0" : "Пример установки", 485, 649, 302, 25, new Font("Segoe UI", 9F), muted));
            AllowDrop = true; DragEnter += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += delegate(object sender, DragEventArgs e) { if (busy) return; var paths = e.Data.GetData(DataFormats.FileDrop) as string[]; if (paths != null && paths.Length == 1) ImportPath(paths[0]); };
            Shown += async delegate { await Scan(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
        }

    }
}
