using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Threading;
using System.Windows.Forms;

namespace DeepSeekHarnessControl
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private TextBox txtPath;
        private Label lblStatus;

        public MainForm()
        {
            Text = "DeepSeek Harness 控制台";
            Width = 580;
            Height = 230;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var lblPath = new Label();
            lblPath.Text = "Harness 目录：";
            lblPath.Location = new Point(15, 18);
            lblPath.AutoSize = true;

            txtPath = new TextBox();
            txtPath.Location = new Point(110, 15);
            txtPath.Width = 330;
            txtPath.ReadOnly = false;

            var btnBrowse = new Button();
            btnBrowse.Text = "浏览…";
            btnBrowse.Location = new Point(450, 13);
            btnBrowse.Width = 90;
            btnBrowse.Click += (s, e) => BrowseFolder();

            var btnStart = new Button();
            btnStart.Text = "一键启动";
            btnStart.Location = new Point(110, 60);
            btnStart.Width = 110;
            btnStart.Height = 34;
            btnStart.Click += (s, e) => StartHarness();

            var btnStop = new Button();
            btnStop.Text = "一键停止";
            btnStop.Location = new Point(230, 60);
            btnStop.Width = 110;
            btnStop.Height = 34;
            btnStop.Click += (s, e) => StopHarness();

            var btnRestart = new Button();
            btnRestart.Text = "一键重启";
            btnRestart.Location = new Point(350, 60);
            btnRestart.Width = 110;
            btnRestart.Height = 34;
            btnRestart.Click += (s, e) => RestartHarness();

            lblStatus = new Label();
            lblStatus.Location = new Point(15, 115);
            lblStatus.Width = 530;
            lblStatus.Height = 50;
            lblStatus.ForeColor = Color.DarkSlateGray;

            Controls.Add(lblPath);
            Controls.Add(txtPath);
            Controls.Add(btnBrowse);
            Controls.Add(btnStart);
            Controls.Add(btnStop);
            Controls.Add(btnRestart);
            Controls.Add(lblStatus);

            AutoDetectPath();
        }

        private void AutoDetectPath()
        {
            string detected = FindHarnessRoot();
            if (detected != null)
            {
                txtPath.Text = detected;
                SetStatus("已自动检测到 Harness：" + detected
                    + (IsHarnessRunning() ? "（正在运行）" : "（未运行）"));
            }
            else
            {
                SetStatus("未自动检测到 Harness，请点击“浏览…”手动选择。");
            }
        }

        private string FindHarnessRoot()
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidates = new string[]
            {
                Path.Combine(user, "deepseek-harness"),
                Path.Combine(user, ".dsh", "profiles", "node_modules", "@deepseek-ai", "dsh"),
                @"D:\deepseek-harness",
                @"C:\deepseek-harness",
                Path.Combine(user, "source", "repos", "deepseek-harness"),
                Path.Combine(user, "Documents", "deepseek-harness"),
            };

            foreach (string candidate in candidates)
            {
                if (IsHarnessRoot(candidate)) return candidate;
            }
            return null;
        }

        private bool IsHarnessRoot(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                // Repo checkout root: apps/cli/src/bin.ts
                if (File.Exists(Path.Combine(path, "apps", "cli", "src", "bin.ts"))) return true;
                // Installed dsh package source: src/bin.ts
                if (File.Exists(Path.Combine(path, "src", "bin.ts"))) return true;
            }
            catch
            {
            }
            return false;
        }

        private void BrowseFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "选择 DeepSeek Harness 目录（包含 apps\\cli\\src\\bin.ts 的目录）";
                fbd.SelectedPath = txtPath.Text;
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtPath.Text = fbd.SelectedPath;
                    SetStatus("已选择：" + fbd.SelectedPath);
                }
            }
        }

        private string RepoPath()
        {
            string path = txtPath.Text.Trim();
            if (path.Length == 0) return null;
            // If user selected the dsh package dir (src/bin.ts), try to derive repo root.
            if (File.Exists(Path.Combine(path, "apps", "cli", "src", "bin.ts"))) return path;
            if (File.Exists(Path.Combine(path, "src", "bin.ts")))
            {
                // package dir is normally a junction to <repo>\apps\cli
                DirectoryInfo parentDir = Directory.GetParent(path);
                string parent = parentDir != null ? parentDir.FullName : null;
                if (parent != null && File.Exists(Path.Combine(parent, "apps", "cli", "src", "bin.ts"))) return parent;
                // Fallback: try two levels up
                DirectoryInfo grandparentDir = parent != null ? Directory.GetParent(parent) : null;
                string grandparent = grandparentDir != null ? grandparentDir.FullName : null;
                if (grandparent != null && File.Exists(Path.Combine(grandparent, "apps", "cli", "src", "bin.ts"))) return grandparent;
                return path;
            }
            return path;
        }

        private bool IsHarnessRunning()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE Name = 'node.exe'");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string cmd = Convert.ToString(mo["CommandLine"]) ?? "";
                    bool isServer = HasServerPath(cmd)
                        && cmd.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isLauncher = cmd.IndexOf("pnpm", StringComparison.OrdinalIgnoreCase) >= 0
                        && cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0
                        && cmd.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isServer || isLauncher) return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private void StartHarness()
        {
            string repo = RepoPath();
            if (repo == null)
            {
                SetStatus("请先选择 Harness 目录。");
                return;
            }
            if (IsHarnessRunning())
            {
                SetStatus("Harness 已在运行，请先停止或使用“一键重启”。");
                return;
            }
            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/k node --import tsx/esm apps/cli/src/bin.ts web";
                psi.WorkingDirectory = repo;
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Normal;
                Process.Start(psi);
                SetStatus("已启动 Harness，新窗口已打开。");
            }
            catch (Exception ex)
            {
                SetStatus("启动失败：" + ex.Message);
            }
        }

        private static bool HasServerPath(string cmd)
        {
            return cmd.IndexOf("apps\\cli\\src\\bin.ts", StringComparison.OrdinalIgnoreCase) >= 0
                || cmd.IndexOf("apps/cli/src/bin.ts", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void StopHarness()
        {
            HashSet<int> pids = new HashSet<int>();
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, Name, CommandLine FROM Win32_Process WHERE (Name = 'node.exe' OR Name = 'cmd.exe')");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string processName = Convert.ToString(mo["Name"]) ?? "";
                    string cmd = Convert.ToString(mo["CommandLine"]) ?? "";
                    bool isServer = processName.Equals("node.exe", StringComparison.OrdinalIgnoreCase)
                        && HasServerPath(cmd)
                        && cmd.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isLauncher = processName.Equals("node.exe", StringComparison.OrdinalIgnoreCase)
                        && cmd.IndexOf("pnpm", StringComparison.OrdinalIgnoreCase) >= 0
                        && cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0
                        && cmd.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isCmdWindow = processName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)
                        && ((HasServerPath(cmd)
                             && cmd.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0)
                            || (cmd.IndexOf("pnpm", StringComparison.OrdinalIgnoreCase) >= 0
                                && cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0
                                && cmd.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0));
                    if (!isServer && !isLauncher && !isCmdWindow) continue;
                    int pid = Convert.ToInt32(mo["ProcessId"]);
                    pids.Add(pid);
                }
            }
            catch
            {
            }

            if (pids.Count == 0)
            {
                SetStatus("未发现运行中的 Harness。");
                return;
            }

            int killed = 0;
            foreach (int pid in pids)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = "/PID " + pid + " /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    killed++;
                }
                catch
                {
                }
            }
            SetStatus("已停止 " + killed + " 个 Harness 进程（共检测到 " + pids.Count + " 个）。");
        }

        private void RestartHarness()
        {
            StopHarness();
            Thread.Sleep(800);
            StartHarness();
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text;
        }
    }
}
