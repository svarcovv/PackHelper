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
        private Label LabelAt(string text, int x, int y, int width, int height, Font font, Color color)
        { return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), Font = font, ForeColor = color }; }

        private void SetupButton(Button button, string text, int x, int y, int width)
        {
            button.Text = text; button.SetBounds(x, y, width, 34); button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(235, 204, 178); button.BackColor = Color.FromArgb(255, 245, 235); Controls.Add(button);
        }

        private void OpenUrl(string url)
        {
            try
            {
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != "https") return;
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Не удалось открыть ссылку"); }
        }

        private GameCandidate Selected { get { return games.SelectedItem as GameCandidate; } }

        private void SetBusy(bool value)
        {
            busy = value; games.Enabled = folder.Enabled = log.Enabled = rescan.Enabled = closed.Enabled = !value;
            progress.Visible = value; RefreshInstall();
        }

        private void RefreshInstall()
        { install.Enabled = !busy && closed.Checked && Selected != null && !Selected.NeedsCacheEvidence; }

        private void ShowCandidate()
        {
            var candidate = Selected;
            target.Text = candidate == null ? "Здесь появится путь установки." : candidate.Destination(release);
            note.Text = candidate == null ? "Не нашли сборку? Выберите папку с options.txt или перетащите её latest.log в окно." :
                candidate.NeedsCacheEvidence ? "OpSec меняет кэш. Один раз войдите на LavaBox, закройте игру и выберите свежий latest.log этой сборки." :
                candidate.Isolated ? "Изолированный кэш определён по логу. Используйте тот же аккаунт и адрес сервера, что при этой попытке входа." :
                "Выбирайте папку именно той сборки, из которой играете на LavaBox. Можно перетащить папку или лог в окно.";
            RefreshInstall();
        }

        private async Task Scan()
        {
            SetBusy(true); status.Text = "Ищем папки известных лаунчеров…";
            try
            {
                var candidates = demoFolder == null ? await Task.Run(() => GameDiscovery.Scan(release)) :
                    new List<GameCandidate> { GameDiscovery.Candidate(demoFolder, release) };
                games.Items.Clear(); foreach (var candidate in candidates) games.Items.Add(candidate);
                if (games.Items.Count == 1) games.SelectedIndex = 0;
                else games.SelectedIndex = -1;
                status.ForeColor = muted;
                status.Text = candidates.Count == 0 ? "Автоматически не нашли сборку. Выберите её папку или latest.log." :
                    candidates.Count == 1 ? "Сборка найдена. Проверьте путь и подтвердите, что игра закрыта." :
                    "Найдено сборок: " + candidates.Count + ". Выберите нужную - автоматически устанавливать во все не будем.";
            }
            catch (Exception error) { status.Text = "Поиск не завершён: " + error.Message + " Выберите папку вручную."; }
            finally { SetBusy(false); ShowCandidate(); }
        }

        private void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Выберите папку игры с options.txt, logs или resourcepacks", ShowNewFolderButton = false })
            { if (dialog.ShowDialog(this) == DialogResult.OK) ImportPath(dialog.SelectedPath); }
        }

        private void ChooseLog()
        {
            using (var dialog = new OpenFileDialog { Title = "Выберите latest.log той сборки, из которой заходили на LavaBox", Filter = "Логи Minecraft (*.log)|*.log", CheckFileExists = true })
            { if (dialog.ShowDialog(this) == DialogResult.OK) ImportPath(dialog.FileName); }
        }

        private void ImportPath(string path)
        {
            try
            {
                GameCandidate candidate;
                if (Directory.Exists(path))
                {
                    if (!PackPaths.LooksLikeGame(path)) throw new IOException("Выберите папку игры с options.txt, logs или resourcepacks.");
                    candidate = GameDiscovery.Candidate(path, release);
                }
                else
                {
                    var candidates = GameDiscovery.ParseLog(GameDiscovery.ReadLogTail(path), release);
                    candidate = candidates.LastOrDefault(c => PackPaths.LooksLikeGame(c.GameDirectory));
                    if (candidate == null)
                    {
                        string parent = Path.GetDirectoryName(Path.GetDirectoryName(path));
                        if (Path.GetFileName(Path.GetDirectoryName(path)).Equals("logs", StringComparison.OrdinalIgnoreCase) && PackPaths.LooksLikeGame(parent))
                            candidate = GameDiscovery.Candidate(parent, release);
                        else throw new IOException("В логе нет подходящего существующего пути LavaBox. Нужен лог с этого компьютера либо выберите папку игры.");
                    }
                }
                PackPaths.RejectLinks(candidate.CacheDirectory);
                int index = -1;
                for (int i = 0; i < games.Items.Count; i++)
                    if (((GameCandidate)games.Items[i]).GameDirectory.Equals(candidate.GameDirectory, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
                if (index >= 0) games.Items.RemoveAt(index);
                index = games.Items.Add(candidate); games.SelectedIndex = index;
                status.ForeColor = muted; status.Text = "Путь выбран. Подтвердите, что Minecraft закрыт, и нажмите «Установить».";
            }
            catch (Exception error) { status.ForeColor = Color.FromArgb(170, 50, 32); status.Text = error.Message; }
        }

        private async Task Install()
        {
            GameCandidate candidate = Selected; if (candidate == null || !closed.Checked) return;
            SetBusy(true); status.ForeColor = muted; status.Text = "Проверяем архив и устанавливаем серверный пак…";
            try
            {
                if (demoFolder == null)
                {
                    var running = await Task.Run(() => GameDiscovery.RunningGameDirectories());
                    if (running.Any(root => root.Equals(candidate.GameDirectory, StringComparison.OrdinalIgnoreCase)))
                        throw new IOException("Эта сборка Minecraft ещё запущена. Полностью закройте игру и повторите установку.");
                }
                InstallResult result = await Task.Run(() => PackInstall.Install(candidate, release, PackInstall.EmbeddedPack));
                status.ForeColor = Color.FromArgb(24, 118, 75);
                status.Text = result.AlreadyPresent ? "Актуальный пак уже установлен. Запустите Minecraft и зайдите на " + release.server + "." :
                    "Готово! Запустите Minecraft и зайдите на " + release.server + ". Разрешите серверный ресурспак.";
                summary.Text = "Версия " + release.packVersion + " установлена.\nВключать ZIP в меню игры не нужно.";
                install.Text = "Проверить установку";
            }
            catch (Exception error) { status.ForeColor = Color.FromArgb(170, 50, 32); status.Text = error.Message; }
            finally { SetBusy(false); }
        }
    }
}
