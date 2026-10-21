using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YangCAD.Setup
{
    internal sealed class MainForm : Form
    {
        private readonly InstallerService _service;
        private readonly Label _statusTitle;
        private readonly Label _statusDetail;
        private readonly Button _installButton;
        private readonly Button _updateButton;
        private readonly Button _repairButton;
        private readonly Button _uninstallButton;
        private readonly CheckBox _purgeCheckBox;
        private readonly TextBox _log;
        private readonly ProgressBar _progress;

        public MainForm()
        {
            Text = "YangCAD Agent 快速安装器";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(650, 500);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _service = new InstallerService(AppDomain.CurrentDomain.BaseDirectory);

            var title = new Label
            {
                Text = "YangCAD Agent",
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(28, 24)
            };
            var subtitle = new Label
            {
                Text = "快速安装模式 · 无管理员 · TRUSTEDPATHS + ApplicationPlugins",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(31, 66)
            };
            _statusTitle = new Label
            {
                Text = "正在检查…",
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(31, 104)
            };
            _statusDetail = new Label
            {
                AutoSize = false,
                Size = new Size(585, 70),
                Location = new Point(31, 132),
                ForeColor = Color.DimGray
            };

            _installButton = MakeButton("安装", 31, 214);
            _updateButton = MakeButton("更新", 181, 214);
            _repairButton = MakeButton("修复", 331, 214);
            _uninstallButton = MakeButton("卸载", 481, 214);

            _installButton.Click += async delegate { await RunAction(SetupAction.Install); };
            _updateButton.Click += async delegate { await RunAction(SetupAction.Update); };
            _repairButton.Click += async delegate { await RunAction(SetupAction.Repair); };
            _uninstallButton.Click += async delegate { await RunAction(SetupAction.Uninstall); };

            _purgeCheckBox = new CheckBox
            {
                Text = "彻底卸载：同时删除 %LOCALAPPDATA%\\YangCADAgent 用户数据",
                AutoSize = true,
                Location = new Point(31, 274)
            };

            _progress = new ProgressBar
            {
                Location = new Point(31, 306),
                Size = new Size(585, 5),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            _log = new TextBox
            {
                Location = new Point(31, 328),
                Size = new Size(585, 125),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle
            };

            var openFolder = new LinkLabel
            {
                Text = "打开安装目录",
                AutoSize = true,
                Location = new Point(526, 465)
            };
            openFolder.LinkClicked += delegate
            {
                var path = _service.InstallPath;
                var target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                Process.Start("explorer.exe", "\"" + target + "\"");
            };

            Controls.AddRange(new Control[]
            {
                title, subtitle, _statusTitle, _statusDetail,
                _installButton, _updateButton, _repairButton, _uninstallButton,
                _purgeCheckBox, _progress, _log, openFolder
            });

            Shown += delegate { RefreshStatus(); };
        }

        private static Button MakeButton(string text, int x, int y)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(135, 46),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                UseVisualStyleBackColor = true
            };
        }

        private void RefreshStatus()
        {
            var status = _service.GetStatus();
            var trustText = status.AutodeskProfilesFound == 0
                ? "未发现 AutoCAD Profile"
                : (status.TrustedPathConfigured ? "可信路径正常" : "可信路径待修复") + " / " + (status.AutoLoadConfigured ? "自动加载正常" : "自动加载待修复");
            var packageText = status.SignedPackage ? "正式签名包" : "快速安装包";

            if (status.Installed)
            {
                _statusTitle.Text = "已安装" + (string.IsNullOrWhiteSpace(status.InstalledVersion) ? string.Empty : " · " + status.InstalledVersion);
                _statusDetail.Text = "发布包：" + (status.PackageVersion ?? "未知") + " · " + packageText + Environment.NewLine +
                                     trustText + " · Profiles=" + status.AutodeskProfilesFound + Environment.NewLine + status.InstallPath;
            }
            else
            {
                _statusTitle.Text = "未安装";
                _statusDetail.Text = "发布包：" + (status.PackageVersion ?? "未知") + " · " + packageText + Environment.NewLine +
                                     "安装时自动配置 YangCAD 专用 TRUSTEDPATHS / APPAUTOLOAD" + Environment.NewLine + status.InstallPath;
            }

            _installButton.Enabled = status.PackageReady && !status.Installed;
            _updateButton.Enabled = status.PackageReady && status.Installed;
            _repairButton.Enabled = status.PackageReady && status.Installed;
            _uninstallButton.Enabled = status.Installed;
            if (!string.IsNullOrWhiteSpace(status.Message)) AppendLog(status.Message);
        }

        private async Task RunAction(SetupAction action)
        {
            if (action == SetupAction.Uninstall && _purgeCheckBox.Checked)
            {
                var confirm = MessageBox.Show(this,
                    "彻底卸载会删除 %LOCALAPPDATA%\\YangCADAgent 用户数据。\n\nYangCAD 专用 TRUSTEDPATHS 无论是否勾选都会移除；不会改动其他可信路径或 SECURELOAD。\n\n确认继续？",
                    "彻底卸载确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
            }

            SetBusy(true);
            _log.Clear();
            AppendLog("开始：" + action);
            try
            {
                var purge = _purgeCheckBox.Checked;
                var result = await Task.Run(() => _service.Execute(action, purge, AppendLogThreadSafe));
                AppendLog(result);
                MessageBox.Show(this, result, "YangCAD Agent", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                RefreshStatus();
            }
        }

        private void SetBusy(bool busy)
        {
            _progress.Visible = busy;
            _installButton.Enabled = !busy && _installButton.Enabled;
            _updateButton.Enabled = !busy && _updateButton.Enabled;
            _repairButton.Enabled = !busy && _repairButton.Enabled;
            _uninstallButton.Enabled = !busy && _uninstallButton.Enabled;
            _purgeCheckBox.Enabled = !busy;
            if (busy)
            {
                _installButton.Enabled = false;
                _updateButton.Enabled = false;
                _repairButton.Enabled = false;
                _uninstallButton.Enabled = false;
            }
        }

        private void AppendLogThreadSafe(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }
            AppendLog(message);
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message.Replace("\n", Environment.NewLine) + Environment.NewLine);
        }
    }
}
