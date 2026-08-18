using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private static readonly Color Ink = Color.FromArgb(0x19, 0x19, 0x19);
        private static readonly Color Paper = Color.FromArgb(0xF2, 0xF2, 0xF0);
        private static readonly Color Signal = Color.FromArgb(0xFF, 0xFA, 0x00);
        private static readonly Color Online = Color.FromArgb(0x00, 0xFF, 0xA2);
        private static readonly Color Error = Color.FromArgb(0xD4, 0x3A, 0x2F);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x6B, 0x6B);
        private static readonly Color Rail = Color.FromArgb(0x19, 0x19, 0x19);
        private static readonly Color Stage = Color.FromArgb(0x1F, 0x1F, 0x1F);
        private static readonly Color StageText = Color.FromArgb(0xB8, 0xB8, 0xB8);

        private TextBox txtPath;
        private ComboBox cmbProfile;
        private Label lblStatus;
        private Label lblProfileState;
        private Label lblPortState;
        private Panel pnlSignal;
        private Panel pnlStage;
        private Button btnStart;
        private Button btnStop;
        private Button btnRestart;
        private Button btnBrowse;
        private Button btnOpenWeb;
        private NotifyIcon notifyIcon;
        private System.Windows.Forms.Timer timerStatus;
        private bool _reallyExit;

        public MainForm()
        {
            Text = "DeepSeek Harness Control";
            ClientSize = new Size(720, 420);
            MinimumSize = new Size(720, 420);
            MaximumSize = new Size(720, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Paper;
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildUi();
            InitTray();
            AutoDetectPath();

            timerStatus = new System.Windows.Forms.Timer();
            timerStatus.Interval = 2000;
            timerStatus.Tick += (s, e) => RefreshStatus();
            timerStatus.Start();
        }

        private void BuildUi()
        {
            // Dock order matters: body is added first so edge docks are laid out first,
            // then the fill panel takes the remaining area.
            var body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Paper;
            Controls.Add(body);

            var rail = new Panel();
            rail.Dock = DockStyle.Left;
            rail.Width = 84;
            rail.BackColor = Rail;
            Controls.Add(rail);

            var brand = new Label();
            brand.Text = "DSH";
            brand.ForeColor = Color.White;
            brand.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            brand.AutoSize = true;
            brand.Location = new Point(16, 20);
            rail.Controls.Add(brand);

            var code = new Label();
            code.Text = "CTRL-01";
            code.ForeColor = Signal;
            code.Font = new Font("Consolas", 8F, FontStyle.Bold);
            code.AutoSize = true;
            code.Location = new Point(14, 58);
            rail.Controls.Add(code);

            rail.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Signal, 3F))
                {
                    e.Graphics.DrawLine(pen, 81, 16, 81, 84);
                }
                using (var brush = new SolidBrush(Color.FromArgb(0x7A, 0x7A, 0x7A)))
                {
                    e.Graphics.TranslateTransform(16, 360);
                    e.Graphics.RotateTransform(-90);
                    e.Graphics.DrawString("FIELD ENGINEERING SYSTEM", new Font("Segoe UI", 7F), brush, PointF.Empty);
                }
            };

            var bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 36;
            bottom.BackColor = Rail;
            Controls.Add(bottom);

            pnlSignal = new Panel();
            pnlSignal.Dock = DockStyle.Left;
            pnlSignal.Width = 4;
            pnlSignal.BackColor = Signal;
            bottom.Controls.Add(pnlSignal);

            lblStatus = new Label();
            lblStatus.SetBounds(10, 0, 440, 36);
            lblStatus.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.ForeColor = Color.FromArgb(0xE8, 0xE8, 0xE6);
            lblStatus.Font = new Font("Segoe UI", 8F);
            lblStatus.AutoEllipsis = true;
            bottom.Controls.Add(lblStatus);

            lblProfileState = new Label();
            lblProfileState.SetBounds(470, 0, 110, 36);
            lblProfileState.Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            lblProfileState.TextAlign = ContentAlignment.MiddleLeft;
            lblProfileState.ForeColor = Signal;
            lblProfileState.Font = new Font("Consolas", 8F, FontStyle.Bold);
            bottom.Controls.Add(lblProfileState);

            lblPortState = new Label();
            lblPortState.SetBounds(590, 0, 120, 36);
            lblPortState.Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            lblPortState.TextAlign = ContentAlignment.MiddleLeft;
            lblPortState.ForeColor = Color.FromArgb(0xC8, 0xC8, 0xC8);
            lblPortState.Font = new Font("Consolas", 8F);
            bottom.Controls.Add(lblPortState);

            var index = new Label();
            index.Text = "01 / CTRL";
            index.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            index.ForeColor = Muted;
            index.AutoSize = true;
            index.Location = new Point(500, 28);
            body.Controls.Add(index);

            var title = new Label();
            title.Text = "DeepSeek Harness";
            title.Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold);
            title.ForeColor = Ink;
            title.AutoSize = true;
            title.Location = new Point(24, 18);
            body.Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = "CONTROL CONSOLE / 现场工程系统";
            subtitle.Font = new Font("Segoe UI", 8F);
            subtitle.ForeColor = Muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(26, 60);
            body.Controls.Add(subtitle);

            var rule = new Panel();
            rule.BackColor = Color.FromArgb(0xC9, 0xC9, 0xC9);
            rule.SetBounds(24, 88, 560, 1);
            body.Controls.Add(rule);

            pnlStage = new Panel();
            pnlStage.SetBounds(24, 104, 588, 220);
            pnlStage.BackColor = Stage;
            body.Controls.Add(pnlStage);

            pnlStage.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Signal, 4F))
                {
                    e.Graphics.DrawLine(pen, 0, 0, 0, 220);
                }
                using (var pen = new Pen(Color.FromArgb(0x3A, 0x3A, 0x3A)))
                {
                    e.Graphics.DrawLine(pen, 0, 36, 588, 36);
                    e.Graphics.DrawLine(pen, 0, 148, 588, 148);
                }
                using (var pen = new Pen(Color.FromArgb(0x6A, 0x6A, 0x6A)))
                {
                    e.Graphics.DrawLine(pen, 0, 0, 24, 0);
                    e.Graphics.DrawLine(pen, 0, 0, 0, 24);
                    e.Graphics.DrawLine(pen, 588, 0, 564, 0);
                    e.Graphics.DrawLine(pen, 588, 0, 588, 24);
                }
            };

            var stageCaption = new Label();
            stageCaption.Text = "FIELD / INSTALL";
            stageCaption.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            stageCaption.ForeColor = Signal;
            stageCaption.AutoSize = true;
            stageCaption.Location = new Point(16, 12);
            pnlStage.Controls.Add(stageCaption);

            var stageCode = new Label();
            stageCode.Text = "SYS-01";
            stageCode.Font = new Font("Consolas", 8F, FontStyle.Bold);
            stageCode.ForeColor = Color.FromArgb(0x8A, 0x8A, 0x8A);
            stageCode.AutoSize = true;
            stageCode.Location = new Point(470, 12);
            pnlStage.Controls.Add(stageCode);

            var pathCaption = new Label();
            pathCaption.Text = "HARNESS DIRECTORY  /  安装目录";
            pathCaption.Font = new Font("Segoe UI", 7.5F);
            pathCaption.ForeColor = StageText;
            pathCaption.AutoSize = true;
            pathCaption.Location = new Point(16, 40);
            pnlStage.Controls.Add(pathCaption);

            txtPath = new TextBox();
            txtPath.Location = new Point(16, 58);
            txtPath.Width = 400;
            txtPath.Height = 28;
            txtPath.Font = new Font("Microsoft YaHei UI", 9F);
            txtPath.ForeColor = Ink;
            txtPath.BackColor = Color.White;
            txtPath.BorderStyle = BorderStyle.FixedSingle;
            txtPath.TabIndex = 0;
            pnlStage.Controls.Add(txtPath);

            btnBrowse = MakeButton("浏览…", Color.FromArgb(0x2A, 0x2A, 0x2A), Color.White,
                Color.FromArgb(0x8A, 0x8A, 0x8A), 424, 56, 70, 32, (s, e) => BrowseFolder());
            btnBrowse.TabIndex = 1;
            pnlStage.Controls.Add(btnBrowse);

            btnOpenWeb = MakeButton("打开界面", Color.FromArgb(0x2A, 0x2A, 0x2A), Color.White,
                Color.FromArgb(0x8A, 0x8A, 0x8A), 500, 56, 70, 32, (s, e) => OpenWebUI());
            btnOpenWeb.TabIndex = 2;
            pnlStage.Controls.Add(btnOpenWeb);

            var profileCaption = new Label();
            profileCaption.Text = "PROFILE  /  配置";
            profileCaption.Font = new Font("Segoe UI", 7.5F);
            profileCaption.ForeColor = StageText;
            profileCaption.AutoSize = true;
            profileCaption.Location = new Point(16, 102);
            pnlStage.Controls.Add(profileCaption);

            cmbProfile = new ComboBox();
            cmbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProfile.Items.Add("web");
            cmbProfile.Items.Add("desktop");
            cmbProfile.SelectedIndex = 0;
            cmbProfile.Location = new Point(16, 120);
            cmbProfile.Width = 140;
            cmbProfile.Font = new Font("Microsoft YaHei UI", 9F);
            cmbProfile.TabIndex = 3;
            cmbProfile.SelectedIndexChanged += (s, e) => UpdateInstruments();
            pnlStage.Controls.Add(cmbProfile);

            var actionsCaption = new Label();
            actionsCaption.Text = "ACTIONS  /  操作";
            actionsCaption.Font = new Font("Segoe UI", 7.5F);
            actionsCaption.ForeColor = StageText;
            actionsCaption.AutoSize = true;
            actionsCaption.Location = new Point(196, 102);
            pnlStage.Controls.Add(actionsCaption);

            btnStart = MakeButton("一键启动", Signal, Ink, Signal, 196, 120, 120, 40, (s, e) => StartHarness());
            btnStart.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnStart.TabIndex = 4;
            pnlStage.Controls.Add(btnStart);

            btnStop = MakeButton("一键停止", Color.FromArgb(0x2A, 0x2A, 0x2A), Color.White,
                Color.FromArgb(0x8A, 0x8A, 0x8A), 322, 120, 120, 40, (s, e) => StopHarness());
            btnStop.TabIndex = 5;
            pnlStage.Controls.Add(btnStop);

            btnRestart = MakeButton("一键重启", Color.FromArgb(0x3A, 0x3A, 0x3A), Color.White,
                Color.FromArgb(0x8A, 0x8A, 0x8A), 448, 120, 120, 40, (s, e) => RestartHarness());
            btnRestart.TabIndex = 6;
            pnlStage.Controls.Add(btnRestart);

            AcceptButton = btnStart;
        }

        private void InitTray()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Text = "DeepSeek Harness Control";
            notifyIcon.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, (s, e) => ShowMainWindow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("一键启动", null, (s, e) => StartHarness());
            menu.Items.Add("一键停止", null, (s, e) => StopHarness());
            menu.Items.Add("一键重启", null, (s, e) => RestartHarness());
            menu.Items.Add("打开 Web UI", null, (s, e) => OpenWebUI());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => ExitApplication());
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private Button MakeButton(string text, Color back, Color fore, Color border, int x, int y, int w, int h, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = border;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.06F);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.06F);
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = new Font("Microsoft YaHei UI", 9F);
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.Click += onClick;
            return b;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (notifyIcon != null) notifyIcon.Visible = true;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                if (notifyIcon != null) notifyIcon.Visible = true;
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshStatus();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            _reallyExit = true;
            if (notifyIcon != null) notifyIcon.Visible = false;
            Application.Exit();
        }

        private void RefreshStatus()
        {
            if (IsDisposed) return;
            string path = txtPath.Text.Trim();
            if (path.Length == 0) return;
            bool running = IsHarnessRunning();
            SetStatus(running ? "Harness 运行中：" + path : "Harness 未运行：" + path,
                running ? Online : Signal);
            UpdateInstruments();
        }

        private void UpdateInstruments()
        {
            if (cmbProfile == null || lblProfileState == null || lblPortState == null) return;
            string profile = cmbProfile.SelectedItem != null ? cmbProfile.SelectedItem.ToString() : "web";
            lblProfileState.Text = "PROFILE / " + profile.ToUpperInvariant();

            string url = Environment.GetEnvironmentVariable("DSH_WEB_URL");
            if (String.IsNullOrEmpty(url)) url = "http://127.0.0.1:3080";
            try
            {
                lblPortState.Text = "PORT / " + new Uri(url).Port;
            }
            catch
            {
                lblPortState.Text = "PORT / --";
            }
        }

        private void OpenWebUI()
        {
            string url = Environment.GetEnvironmentVariable("DSH_WEB_URL");
            if (String.IsNullOrEmpty(url)) url = "http://127.0.0.1:3080";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                SetStatus("已打开 Web UI：" + url, Signal);
            }
            catch (Exception ex)
            {
                SetStatus("打开 Web UI 失败：" + ex.Message, Error);
            }
        }

        private void AutoDetectPath()
        {
            string detected = FindHarnessRoot();
            if (detected != null)
            {
                txtPath.Text = detected;
                bool running = IsHarnessRunning();
                SetStatus("已自动检测到 Harness：" + detected + (running ? "（正在运行）" : "（未运行）"),
                    running ? Online : Signal);
            }
            else
            {
                txtPath.Text = "";
                SetStatus("未自动检测到 Harness，请点击“浏览…”手动选择。", Muted);
            }
            UpdateInstruments();
        }

        private static void AddCandidate(List<string> list, string path)
        {
            if (String.IsNullOrEmpty(path)) return;
            foreach (string existing in list)
            {
                if (String.Equals(existing, path, StringComparison.OrdinalIgnoreCase)) return;
            }
            list.Add(path);
        }

        private string FindHarnessRoot()
        {
            string fromProcess = FindHarnessRootFromProcess();
            if (fromProcess != null) return fromProcess;

            List<string> candidates = new List<string>();
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            AddCandidate(candidates, Path.Combine(user, "deepseek-harness"));
            AddCandidate(candidates, Path.Combine(user, ".dsh", "profiles", "node_modules", "@deepseek-ai", "dsh"));
            AddCandidate(candidates, Path.Combine(user, "source", "repos", "deepseek-harness"));
            AddCandidate(candidates, Path.Combine(user, "Documents", "deepseek-harness"));

            string dshHome = Environment.GetEnvironmentVariable("DSH_HOME");
            if (!String.IsNullOrEmpty(dshHome))
            {
                AddCandidate(candidates, Path.Combine(dshHome, "profiles", "node_modules", "@deepseek-ai", "dsh"));
            }

            string[] commonDirs = { "dev", "projects", "source", "tools", "workspace", "code", "repos" };
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;

                AddCandidate(candidates, Path.Combine(root, "deepseek-harness"));
                AddCandidate(candidates, Path.Combine(root, "dsh"));

                foreach (string dir in commonDirs)
                {
                    AddCandidate(candidates, Path.Combine(root, dir, "deepseek-harness"));
                }
            }

            foreach (string candidate in candidates)
            {
                if (IsHarnessRoot(candidate)) return candidate;
            }
            return null;
        }

        private string FindHarnessRootFromProcess()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE Name = 'node.exe' OR Name = 'cmd.exe'");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string cmd = Convert.ToString(mo["CommandLine"]) ?? "";
                    string root = ExtractHarnessRoot(cmd);
                    if (root != null && IsHarnessRoot(root)) return root;
                }
            }
            catch
            {
            }
            return null;
        }

        private static string ExtractHarnessRoot(string cmd)
        {
            if (String.IsNullOrEmpty(cmd)) return null;
            string[] markers = { "apps\\cli\\src\\bin.ts", "apps/cli/src/bin.ts" };
            foreach (string marker in markers)
            {
                int index = cmd.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index < 0) continue;

                int start = index;
                while (start > 0)
                {
                    char c = cmd[start - 1];
                    if (c == '"' || c == '\'') break;
                    if (Char.IsWhiteSpace(c)) break;
                    start--;
                }

                string raw = cmd.Substring(start, index - start);
                string path = raw.Trim().Trim('"', '\'', '\\', '/');
                if (path.Length >= 2 && path[1] == ':') return path;
            }
            return null;
        }

        private bool IsHarnessRoot(string path)
        {
            try
            {
                if (String.IsNullOrEmpty(path)) return false;
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
                    SetStatus("已选择：" + fbd.SelectedPath, Signal);
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
                    "SELECT CommandLine FROM Win32_Process WHERE Name = 'node.exe' OR Name = 'cmd.exe'");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string cmd = Convert.ToString(mo["CommandLine"]) ?? "";
                    bool isServer = HasServerPath(cmd)
                        && IsDshProfile(cmd);
                    bool isLauncher = cmd.IndexOf("pnpm", StringComparison.OrdinalIgnoreCase) >= 0
                        && cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0
                        && IsDshProfile(cmd);
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
                SetStatus("请先选择 Harness 目录。", Error);
                return;
            }
            if (IsHarnessRunning())
            {
                SetStatus("Harness 已在运行，请先停止或使用“一键重启”。", Online);
                return;
            }
            try
            {
                string profile = cmbProfile.SelectedItem != null ? cmbProfile.SelectedItem.ToString() : "web";
                var psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/k node --import tsx/esm apps/cli/src/bin.ts " + profile;
                psi.WorkingDirectory = repo;
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Normal;
                Process.Start(psi);
                SetStatus("已启动 Harness（" + profile + "），新窗口已打开。", Online);
            }
            catch (Exception ex)
            {
                SetStatus("启动失败：" + ex.Message, Error);
            }
        }

        private static bool HasServerPath(string cmd)
        {
            return cmd.IndexOf("apps\\cli\\src\\bin.ts", StringComparison.OrdinalIgnoreCase) >= 0
                || cmd.IndexOf("apps/cli/src/bin.ts", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDshProfile(string cmd)
        {
            return cmd.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0
                || cmd.IndexOf("desktop", StringComparison.OrdinalIgnoreCase) >= 0;
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
                        && IsDshProfile(cmd);
                    bool isLauncher = processName.Equals("node.exe", StringComparison.OrdinalIgnoreCase)
                        && cmd.IndexOf("pnpm", StringComparison.OrdinalIgnoreCase) >= 0
                        && cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0
                        && IsDshProfile(cmd);
                    bool isCmdWindow = processName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)
                        && ((HasServerPath(cmd)
                             && IsDshProfile(cmd))
                            || (cmd.IndexOf("pnpm", StringComparison.OrdinalIgnoreCase) >= 0
                                && cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0
                                && IsDshProfile(cmd)));
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
                SetStatus("未发现运行中的 Harness。", Signal);
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
            SetStatus("已停止 " + killed + " 个 Harness 进程（共检测到 " + pids.Count + " 个）。", Signal);
        }

        private void RestartHarness()
        {
            StopHarness();
            Thread.Sleep(800);
            StartHarness();
        }

        private void SetStatus(string text, Color signal)
        {
            lblStatus.Text = text;
            pnlSignal.BackColor = signal;
        }
    }
}
