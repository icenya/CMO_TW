using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using CMOShared;
using Microsoft.Win32;

namespace CMOLocalizerInstaller
{
    public class InstallerForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private Button btnInstall;
        private Button btnUninstall;
        private Button btnCopyright;
        private TextBox txtLog;
        private Label lblStatus;

        public InstallerForm(string initialPath)
        {
            this.Text = "Command: Modern Operations (CMO) 完整中文中文化一鍵安裝工具 v4.2.6（適配遊戲內部版本 " + ReleaseConstants.GameBuild + "）";
            this.Size = new Size(700, 630);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            Label lblTitle = new Label();
            lblTitle.Text = "Command: Modern Operations 完整繁體中文化模組";
            lblTitle.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(20, 60, 120);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, 18);
            this.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "採用 CLR AppDomainManager 零侵入記憶體級雙重本地化引擎 | 不修改原版 EXE | 支持一鍵還原";
            lblSub.Text += "\n適配遊戲內部版本 " + ReleaseConstants.GameBuild + "（遊戲標題裏的 Build 號；遊戲更新後需等待對應版本的修補程式）";
            lblSub.ForeColor = Color.DimGray;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(22, 50);
            this.Controls.Add(lblSub);

            Label lblPath = new Label();
            lblPath.Text = "CMO 遊戲根目錄:";
            lblPath.AutoSize = true;
            lblPath.Location = new Point(22, 90);
            this.Controls.Add(lblPath);

            txtPath = new TextBox();
            txtPath.Location = new Point(22, 115);
            txtPath.Size = new Size(520, 26);
            txtPath.Text = initialPath;
            this.Controls.Add(txtPath);

            btnBrowse = new Button();
            btnBrowse.Text = "瀏覽目錄...";
            btnBrowse.Location = new Point(552, 113);
            btnBrowse.Size = new Size(95, 28);
            btnBrowse.Click += new EventHandler(OnBrowseClick);
            this.Controls.Add(btnBrowse);

            lblStatus = new Label();
            lblStatus.Text = "當前狀態: 正在檢測...";
            lblStatus.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(22, 155);
            this.Controls.Add(lblStatus);

            btnInstall = new Button();
            btnInstall.Text = "一鍵安裝繁體中文模組";
            btnInstall.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            btnInstall.BackColor = Color.FromArgb(30, 110, 200);
            btnInstall.ForeColor = Color.White;
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.Location = new Point(22, 190);
            btnInstall.Size = new Size(300, 46);
            btnInstall.Click += new EventHandler(OnInstallClick);
            this.Controls.Add(btnInstall);

            btnUninstall = new Button();
            btnUninstall.Text = "一鍵還原原版英文";
            btnUninstall.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular);
            btnUninstall.Location = new Point(347, 190);
            btnUninstall.Size = new Size(300, 46);
            btnUninstall.Click += new EventHandler(OnUninstallClick);
            this.Controls.Add(btnUninstall);

            // ── 版權與開源協定分組 ─────────────────────────────────
            GroupBox grpCopyright = new GroupBox();
            grpCopyright.Text = "版權與開放原始碼宣告";
            grpCopyright.Location = new Point(22, 252);
            grpCopyright.Size = new Size(645, 115);
            this.Controls.Add(grpCopyright);

            Label lblUpstream = new Label();
            lblUpstream.Text = "原作者專案倉庫 (Upstream)：https://github.com/mj8724/CMO_CN";
            lblUpstream.AutoSize = true;
            lblUpstream.Location = new Point(16, 24);
            grpCopyright.Controls.Add(lblUpstream);

            Label lblFork = new Label();
            lblFork.Text = "正體在地化專案 (Taiwan Fork)：https://github.com/NekoDream/CMO_TW";
            lblFork.AutoSize = true;
            lblFork.Location = new Point(16, 51);
            grpCopyright.Controls.Add(lblFork);

            Label lblLicense = new Label();
            lblLicense.Text = "開放原始碼協定：MIT License（完全免費自由使用、修改與散發）";
            lblLicense.AutoSize = true;
            lblLicense.Location = new Point(16, 78);
            grpCopyright.Controls.Add(lblLicense);

            btnCopyright = new Button();
            btnCopyright.Text = "📜 查看完整版權與開源條款";
            btnCopyright.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            btnCopyright.Location = new Point(445, 72);
            btnCopyright.Size = new Size(185, 30);
            btnCopyright.Click += new EventHandler(OnCopyrightClick);
            grpCopyright.Controls.Add(btnCopyright);

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.FromArgb(245, 247, 250);
            txtLog.Location = new Point(22, 380);
            txtLog.Size = new Size(645, 185);
            this.Controls.Add(txtLog);

            Log("歡迎使用 Command: Modern Operations (CMO) 完整繁體中文化安裝程式。");
            Log("本模組涵蓋：主介面、選單、作戰條令(RoE)、任務編輯、航空作業、全部45個教程及全球4608處地名。");
            RefreshStatus();
        }

        private void OnCopyrightClick(object sender, EventArgs e)
        {
            using (CopyrightForm cf = new CopyrightForm())
            {
                cf.ShowDialog(this);
            }
        }

        private void Log(string msg)
        {
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
            InstallerCore.WriteLog(msg);   // 同时落盘：报障时能看到界面日志框以外的上下文
        }

        private void OnBrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "請選擇 Command: Modern Operations 遊戲安裝根目錄 (包含 Command.exe)";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = fbd.SelectedPath;
                    RefreshStatus();
                }
            }
        }

        private void RefreshStatus()
        {
            string gp = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(gp) || !File.Exists(Path.Combine(gp, "Command.exe")))
            {
                lblStatus.Text = "當前狀態: 未找到 Command.exe，請點擊[瀏覽目錄]選擇正確的遊戲根目錄";
                lblStatus.ForeColor = Color.Red;
                return;
            }

            // 状态以游戏目录内的安装清单（部署实证）为准，不再单看注册表 Version：
            // 注册表值会被任意安装包覆盖、会被旧版还原清空，从而“说谎”。
            string detail;
            ManifestState state = InstallManifest.Verify(gp, out detail);
            if (state == ManifestState.Ok)
            {
                InstallManifest m = InstallManifest.Read(gp);
                lblStatus.Text = "當前狀態: 已安裝完整繁體中文化模組 v" + m.Version
                    + "（" + InstallerCore.LocalTimeText(m.InstalledAt) + " 安裝）";
                lblStatus.ForeColor = Color.Green;
            }
            else if (state == ManifestState.Modified)
            {
                lblStatus.Text = "當前狀態: 檢測到" + detail + "，建議重新點擊[一鍵安裝繁體中文模組]修復";
                lblStatus.ForeColor = Color.DarkOrange;
            }
            else if (InstallerCore.CheckInstalled(gp))
            {
                // 存量 4.1.x 用户 / 绿色包用户：有文件无清单 → 自愈登记，不再误报“未安装”
                InstallManifest.Adopt(gp, CmoSettings.RegistryInstalledVersion());
                InstallManifest adopted = InstallManifest.Read(gp);
                string shown = (adopted != null && adopted.Version.Length > 0) ? adopted.Version : "未知";
                lblStatus.Text = "當前狀態: 已安裝完整繁體中文化模組（已登記當前狀態，v" + shown + "）";
                lblStatus.ForeColor = Color.Green;
            }
            else
            {
                lblStatus.Text = "當前狀態: 原版英文環境 (未安裝中文化模組)";
                lblStatus.ForeColor = Color.DarkOrange;
            }
        }

        private void OnInstallClick(object sender, EventArgs e)
        {
            string gp = txtPath.Text.Trim();
            string err;
            bool ok = InstallerCore.Install(gp, out err);

            // 失败后优先分辨原因：① 目录不可写（Steam 装在 C:\Program Files 的典型情形，需提权）
            // ② 目标文件被运行中的游戏占用（需先关游戏）。两者的处置完全不同，必须分开给指引。
            if (!ok)
            {
                string why;
                if (!InstallerCore.CanWriteToDir(gp, out why))
                {
                    DialogResult dr = MessageBox.Show(
                        "安裝失敗：遊戲目錄當前不可寫（普通權限）。\n\n目錄：" + gp +
                        "\n原因：" + why +
                        "\n\n遊戲裝在 C:\\Program Files 下時（Steam 預設位置）需要管理員權限。\n" +
                        "是否現在以管理員身份重新啓動安裝器？",
                        "需要管理員權限", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == DialogResult.Yes)
                    {
                        Log("目錄不可寫，嘗試以管理員身份重啓安裝器。");
                        if (InstallerCore.RelaunchElevated())
                        {
                            Close();
                            return;
                        }
                        MessageBox.Show("提權啓動失敗，請手動右鍵安裝器選「以管理員身份運行」。",
                            "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        Log("[取消] 目錄不可寫，用戶拒絕提權重試。");
                    }
                }
                else if (InstallerCore.IsGameRunning())
                {
                    DialogResult dr = MessageBox.Show(
                        "安裝失敗：目標檔案正被運行中的遊戲佔用。\n\n" + err +
                        "\n\n是否現在關閉遊戲並重試安裝？\n（遊戲通常有自動存檔；未儲存的推演進度會丟失）",
                        "遊戲正在運行", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == DialogResult.Yes)
                    {
                        Log("目標檔案被遊戲佔用，用戶同意關閉遊戲後重試安裝。");
                        if (InstallerCore.CloseGame(30))
                        {
                            ok = InstallerCore.Install(gp, out err);
                        }
                        else
                        {
                            err = "無法關閉遊戲處理程序，請手動退出遊戲後重試。";
                        }
                    }
                    else
                    {
                        Log("[取消] 遊戲正在運行，用戶選擇不關閉遊戲，安裝中止。");
                    }
                }
            }

            if (ok)
            {
                Log("Successfully Installed! 已成功複製 CMOLocalizer.dll 與 CMOLocalizer.json。");
                Log("已自動備份原版設定爲 Command.exe.config.cmo_bak 並啓用本地化引擎。");
                RefreshStatus();
                MessageBox.Show("CMO 完整繁體中文化模組安裝成功！\n現在直接從 Steam 或運行 Command.exe 啓動遊戲即可顯示中文。\n\n已同時啓用自動升級：開機後自動檢查一次，之後每 6 小時檢查一次。", "安裝成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log("[錯誤] 安裝失敗: " + err);
                MessageBox.Show("安裝失敗:\n" + err, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnUninstallClick(object sender, EventArgs e)
        {
            string gp = txtPath.Text.Trim();
            string err;
            if (InstallerCore.Uninstall(gp, out err))
            {
                InstallManifest.Clear(gp);
                CmoSettings.Purge();
                Log("已成功卸載繁體中文化模組並恢復原版 Command.exe.config 設定。");
                RefreshStatus();
                MessageBox.Show("已無損還原為原版英文環境！", "還原成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log("[錯誤] 還原失敗: " + err);
                MessageBox.Show("還原失敗:\n" + err, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class CopyrightForm : Form
    {
        public CopyrightForm()
        {
            this.Text = "版權與開放原始碼協定 - Command: Modern Operations (CMO_TW)";
            this.Size = new Size(660, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.White;

            Label lblTitle = new Label();
            lblTitle.Text = "📜 版權、開源協定與專案宣告";
            lblTitle.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(20, 60, 120);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, 16);
            this.Controls.Add(lblTitle);

            Label lblDesc = new Label();
            lblDesc.Text = "本專案為《Command: Modern Operations》臺灣正體中文在地化修補程式。\n全面對齊中華民國國防部《美華軍語辭典》標準軍語，並維護我國格稱謂與地圖地名。\n本專案為完全自由開放原始碼社群專案，不含遊戲主程式與任何商業檔案，請支持正版。";
            lblDesc.ForeColor = Color.FromArgb(80, 80, 80);
            lblDesc.AutoSize = true;
            lblDesc.Location = new Point(22, 48);
            this.Controls.Add(lblDesc);

            // 倉庫資訊群組
            GroupBox grpRepos = new GroupBox();
            grpRepos.Text = "專案與倉庫資訊";
            grpRepos.Location = new Point(22, 115);
            grpRepos.Size = new Size(600, 105);
            this.Controls.Add(grpRepos);

            Label lblOrig = new Label();
            lblOrig.Text = "原作者專案倉庫 (Upstream)：https://github.com/mj8724/CMO_CN";
            lblOrig.AutoSize = true;
            lblOrig.Location = new Point(15, 28);
            grpRepos.Controls.Add(lblOrig);

            Button btnOpenOrig = new Button();
            btnOpenOrig.Text = "開啟網頁";
            btnOpenOrig.Location = new Point(480, 23);
            btnOpenOrig.Size = new Size(100, 26);
            btnOpenOrig.Click += new EventHandler(delegate(object s, EventArgs e) {
                OpenUrl("https://github.com/mj8724/CMO_CN");
            });
            grpRepos.Controls.Add(btnOpenOrig);

            Label lblTw = new Label();
            lblTw.Text = "正體在地化專案 (Taiwan Fork)：https://github.com/NekoDream/CMO_TW";
            lblTw.AutoSize = true;
            lblTw.Location = new Point(15, 65);
            grpRepos.Controls.Add(lblTw);

            Button btnOpenTw = new Button();
            btnOpenTw.Text = "開啟網頁";
            btnOpenTw.Location = new Point(480, 60);
            btnOpenTw.Size = new Size(100, 26);
            btnOpenTw.Click += new EventHandler(delegate(object s, EventArgs e) {
                OpenUrl("https://github.com/NekoDream/CMO_TW");
            });
            grpRepos.Controls.Add(btnOpenTw);

            // 協定全文文字框
            Label lblLicenseTitle = new Label();
            lblLicenseTitle.Text = "開放原始碼協定條款 (MIT License)：";
            lblLicenseTitle.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            lblLicenseTitle.AutoSize = true;
            lblLicenseTitle.Location = new Point(22, 230);
            this.Controls.Add(lblLicenseTitle);

            TextBox txtLicense = new TextBox();
            txtLicense.Multiline = true;
            txtLicense.ReadOnly = true;
            txtLicense.ScrollBars = ScrollBars.Vertical;
            txtLicense.BackColor = Color.FromArgb(248, 249, 250);
            txtLicense.Font = new Font("Consolas", 9f, FontStyle.Regular);
            txtLicense.Location = new Point(22, 255);
            txtLicense.Size = new Size(600, 215);
            txtLicense.Text = GetMitLicenseText();
            this.Controls.Add(txtLicense);

            Button btnClose = new Button();
            btnClose.Text = "關 閉";
            btnClose.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
            btnClose.Location = new Point(265, 480);
            btnClose.Size = new Size(110, 32);
            btnClose.Click += new EventHandler(delegate(object s, EventArgs e) {
                this.Close();
            });
            this.Controls.Add(btnClose);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("無法開啟網址：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string GetMitLicenseText()
        {
            return "MIT License\r\n\r\n"
                + "Copyright (c) 2024-2026 CMO Localization Project Contributors\r\n\r\n"
                + "Permission is hereby granted, free of charge, to any person obtaining a copy\r\n"
                + "of this software and associated documentation files (the \"Software\"), to deal\r\n"
                + "in the Software without restriction, including without limitation the rights\r\n"
                + "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell\r\n"
                + "copies of the Software, and to permit persons to whom the Software is\r\n"
                + "furnished to do so, subject to the following conditions:\r\n\r\n"
                + "The above copyright notice and this permission notice shall be included in all\r\n"
                + "copies or substantial portions of the Software.\r\n\r\n"
                + "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\r\n"
                + "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\r\n"
                + "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\r\n"
                + "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\r\n"
                + "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\r\n"
                + "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\r\n"
                + "SOFTWARE.\r\n\r\n"
                + "-------------------------------------------------------------------------\r\n"
                + "開放原始碼協定條款摘要：\r\n"
                + "本專案採用 MIT 授權條款開放原始碼。任何人皆得自由、免費使用、複製、修改、合併、\r\n"
                + "發布、散布、再授權及販售軟體之副本，並得授權受領軟體者同樣享有上述權利。\r\n"
                + "唯一條件為：所有副本或重要部分均應包含上述版權聲明及本授權條款。\r\n";
        }
    }

    public static class InstallerCore
    {
        /// <summary>
        /// 游戏是否正在运行（Command.exe）。用于判断安装失败是否由“目标 DLL 被
        /// 已加载映像占用”造成——那种情况下只能让用户先关游戏。
        /// </summary>
        public static bool IsGameRunning()
        {
            try
            {
                Process[] ps = Process.GetProcessesByName("Command");
                bool any = ps != null && ps.Length > 0;
                if (ps != null) foreach (Process p in ps) { try { p.Dispose(); } catch { } }
                return any;
            }
            catch { return false; }
        }

        /// <summary>
        /// 关闭游戏：先礼貌关闭主窗口，超时再强制结束。返回是否已退出。
        /// 只在用户明确同意后调用（GUI 会弹窗确认）。
        /// </summary>
        public static bool CloseGame(int waitSeconds)
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("Command"))
                {
                    try { if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow(); } catch { }
                    try { p.Dispose(); } catch { }
                }
                int waited = 0;
                while (waited < waitSeconds * 1000)
                {
                    if (!IsGameRunning()) return true;
                    System.Threading.Thread.Sleep(500);
                    waited += 500;
                }
                foreach (Process p in Process.GetProcessesByName("Command"))
                {
                    try { p.Kill(); } catch { }
                    try { p.Dispose(); } catch { }
                }
                System.Threading.Thread.Sleep(1500);
                return !IsGameRunning();
            }
            catch { return false; }
        }

        /// <summary>
        /// 目标目录是否可写。Steam 默认装在 C:\Program Files 下，普通权限写不进去；
        /// 旧实现把写入失败吞成「资源缺失」，用户只能看到莫名报错，所以先探测一次。
        /// </summary>
        public static bool CanWriteToDir(string dir, out string reason)
        {
            reason = "";
            try
            {
                string probe = Path.Combine(dir, ".cmo_write_test.tmp");
                using (FileStream fs = new FileStream(probe, FileMode.Create, FileAccess.Write))
                {
                    fs.WriteByte(0);
                }
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 以管理员身份重新启动本安装器（保留原命令行参数）。
        /// 游戏装在 C:\Program Files 下时只能靠提权写入，这是用户侧最常见的堵点。
        /// </summary>
        public static bool RelaunchElevated()
        {
            try
            {
                string exe = Application.ExecutablePath;
                string[] argv = Environment.GetCommandLineArgs();
                StringBuilder sb = new StringBuilder();
                for (int i = 1; i < argv.Length; i++)
                {
                    sb.Append('"').Append(argv[i].Replace("\"", "")).Append('"').Append(' ');
                }
                ProcessStartInfo psi = new ProcessStartInfo(exe, sb.ToString().Trim());
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                WriteLog("提权重启失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 落盤日誌（盡力而爲）。舊實現只往介面日誌框寫文本、異常又全被吞，
        /// 用戶報障時拿不到任何可分析的資訊（2026-10-18 排查就卡在這裏）。
        /// </summary>
        public static void WriteLog(string msg)
        {
            try
            {
                string dir = CmoPaths.LocalDir;
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "installer.log");
                if (File.Exists(file) && new FileInfo(file).Length > 512 * 1024)
                {
                    File.WriteAllText(file, "", Encoding.UTF8);
                }
                File.AppendAllText(file, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + "] [v" + ReleaseConstants.Version + "] " + msg + "\r\n", Encoding.UTF8);
            }
            catch { }
        }

        public static string DetectGamePath()
        {
            // 1. 如果安裝程式直接放在遊戲根目錄下運行
            string cur = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(cur, "Command.exe"))) return cur;

            // 2. 通過註冊表檢測 Steam 遊戲安裝路徑 (支持 32/64 位及 CurrentUser)
            string[] uninstallKeys = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1076160",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1076160"
            };

            foreach (string sub in uninstallKeys)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(sub))
                    {
                        if (key != null)
                        {
                            object loc = key.GetValue("InstallLocation");
                            if (loc != null && File.Exists(Path.Combine(loc.ToString(), "Command.exe")))
                                return loc.ToString();
                        }
                    }
                }
                catch { }

                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(sub))
                    {
                        if (key != null)
                        {
                            object loc = key.GetValue("InstallLocation");
                            if (loc != null && File.Exists(Path.Combine(loc.ToString(), "Command.exe")))
                                return loc.ToString();
                        }
                    }
                }
                catch { }
            }

            // 3. 讀取 Steam 主目錄並解析所有 Steam 庫資料夾 (libraryfolders.vdf)
            string steamMainDir = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object sp = key.GetValue("SteamPath");
                        if (sp != null) steamMainDir = sp.ToString().Replace('/', '\\');
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(steamMainDir))
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                    {
                        if (key != null)
                        {
                            object ip = key.GetValue("InstallPath");
                            if (ip != null) steamMainDir = ip.ToString().Replace('/', '\\');
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(steamMainDir) && Directory.Exists(steamMainDir))
            {
                // 檢查 Steam 預設主庫
                string mainCmo = Path.Combine(steamMainDir, @"steamapps\common\Command - Modern Operations");
                if (File.Exists(Path.Combine(mainCmo, "Command.exe"))) return mainCmo;

                // 解析 libraryfolders.vdf 獲取用戶的所有自定義多盤符庫
                string vdfPath = Path.Combine(steamMainDir, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    try
                    {
                        string vdfText = File.ReadAllText(vdfPath);
                        var matches = System.Text.RegularExpressions.Regex.Matches(vdfText, "\"path\"\\s+\"([^\"]+)\"");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            if (m.Success && m.Groups.Count > 1)
                            {
                                string libPath = m.Groups[1].Value.Replace("\\\\", "\\");
                                string cmoInLib = Path.Combine(libPath, @"steamapps\common\Command - Modern Operations");
                                if (File.Exists(Path.Combine(cmoInLib, "Command.exe"))) return cmoInLib;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 4. 自动全盘驱动器遍历扫描 (涵盖常见安装盘符与自建库)
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                string[] checkRel = new string[]
                {
                    @"soft\steam\steamapps\common\Command - Modern Operations",
                    @"Steam\steamapps\common\Command - Modern Operations",
                    @"SteamLibrary\steamapps\common\Command - Modern Operations",
                    @"Program Files (x86)\Steam\steamapps\common\Command - Modern Operations",
                    @"Program Files\Steam\steamapps\common\Command - Modern Operations",
                    @"Games\Steam\steamapps\common\Command - Modern Operations",
                    @"Games\SteamLibrary\steamapps\common\Command - Modern Operations"
                };

                foreach (DriveInfo d in drives)
                {
                    if (!d.IsReady) continue;
                    foreach (string rel in checkRel)
                    {
                        string p = Path.Combine(d.RootDirectory.FullName, rel);
                        if (File.Exists(Path.Combine(p, "Command.exe"))) return p;
                    }
                }
            }
            catch { }

            return "";
        }

        public static bool CheckInstalled(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath)) return false;
            string cfgPath = Path.Combine(gamePath, "Command.exe.config");
            string dllDest = Path.Combine(gamePath, "CMOLocalizer.dll");
            string jsonDest = Path.Combine(gamePath, "CMOLocalizer.json");
            if (!File.Exists(cfgPath) || !File.Exists(dllDest) || !File.Exists(jsonDest)) return false;
            try
            {
                string txt = File.ReadAllText(cfgPath);
                return txt.Contains("CMOLocalizer.LocalizerDomainManager");
            }
            catch
            {
                return false;
            }
        }

        public static bool Install(string gamePath, out string errorMsg)
        {
            return Install(gamePath, WriteRegistrySettings, out errorMsg);
        }

        // 文件部署仍走同一路径；隔离测试仅替换用户设置副作用。
        internal static bool Install(string gamePath, Action<string> writeSettings, out string errorMsg)
        {
            errorMsg = "";
            if (string.IsNullOrEmpty(gamePath) || !File.Exists(Path.Combine(gamePath, "Command.exe")))
            {
                errorMsg = "未在目標路徑找到 Command.exe，請確認遊戲目錄是否正確。";
                return false;
            }

            string curDir = AppDomain.CurrentDomain.BaseDirectory;

            // 先探测目标目录可写性：Steam 默认装在 C:\Program Files 下，普通权限写不进去，
            // 而这正是「在别的电脑上、没开游戏也装不上」的首要原因。直接探针比事后猜靠谱。
            string writeReason;
            if (!CanWriteToDir(gamePath, out writeReason))
            {
                errorMsg = "遊戲目錄不可寫: " + gamePath + "\n原因: " + writeReason
                    + "\n\n遊戲裝在 C:\\Program Files 下時（Steam 預設位置）需要管理員權限："
                    + "請右鍵安裝器選「以管理員身份運行」後重試，或把遊戲移到用戶可寫目錄。";
                WriteLog("安裝中止：目錄不可寫 " + gamePath + " -> " + writeReason);
                return false;
            }
            WriteLog("安裝開始：遊戲目錄=" + gamePath + "；安裝包目錄=" + curDir
                + "；酬載來源=安裝包內嵌資源");

            try
            {
                string dllDest = Path.Combine(gamePath, "CMOLocalizer.dll");
                string jsonDest = Path.Combine(gamePath, "CMOLocalizer.json");
                string writeErr;

                // 运行中的游戏会占住 CMOLocalizer.dll（已加载映像）：直接 File.Copy 覆盖会被
                // 共享冲突拒绝，而旧实现把这个异常吞掉、只能报「未找到资源」——用户实测
                // 2026-10-18 21:02 在游戏运行时安装拿到的完全不着边际的报错就是这个。
                // 统一走「写同目录临时文件 + MoveFileEx 原子替换」：重命名不受文件占用影响。
                // 发布包只部署自身内嵌字节：旁边或目标目录里的旧载荷不能代表新包。
                // 无内嵌资源的开发构建明确失败，不把磁盘旧文件登记为 CurrentVersion。
                if (!ExtractEmbeddedResource("CMOLocalizer.dll", dllDest, out writeErr))
                {
                    errorMsg = writeErr;
                    return false;
                }

                if (!ExtractEmbeddedResource("CMOLocalizer.json", jsonDest, out writeErr))
                {
                    errorMsg = writeErr;
                    return false;
                }

                string cfgPath = Path.Combine(gamePath, "Command.exe.config");
                string bakPath = Path.Combine(gamePath, "Command.exe.config.cmo_bak");
                if (File.Exists(cfgPath) && !File.Exists(bakPath))
                {
                    try
                    {
                        string txt = File.ReadAllText(cfgPath);
                        if (txt.Contains("appDomainManagerType") || txt.Contains("appDomainManagerAssembly"))
                        {
                            XmlDocument bakDoc = new XmlDocument();
                            bakDoc.Load(cfgPath);
                            RemoveDomainManagerNodes(bakDoc);
                            EnsureRequiredConfigElements(bakDoc);
                            bakDoc.Save(bakPath);
                        }
                        else
                        {
                            File.Copy(cfgPath, bakPath, true);
                        }
                    }
                    catch
                    {
                        File.Copy(cfgPath, bakPath, true);
                    }
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(cfgPath);

                XmlNode runtime = doc.SelectSingleNode("//configuration/runtime");
                if (runtime == null)
                {
                    XmlNode config = doc.SelectSingleNode("//configuration");
                    runtime = doc.CreateElement("runtime");
                    config.AppendChild(runtime);
                }

                NormalizeDomainManagerSetting(doc, runtime, "appDomainManagerType",
                    "CMOLocalizer.LocalizerDomainManager");
                NormalizeDomainManagerSetting(doc, runtime, "appDomainManagerAssembly",
                    "CMOLocalizer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

                EnsureRequiredConfigElements(doc);

                doc.Save(cfgPath);

                // 释放中文化版武器数据库 HTML 模板 (DB/Templates)
                string tplDir = Path.Combine(gamePath, "DB", "Templates");
                string tplBakDir = Path.Combine(gamePath, "DB", "Templates_Original_Backup");
                if (Directory.Exists(tplDir))
                {
                    if (!Directory.Exists(tplBakDir))
                    {
                        Directory.CreateDirectory(tplBakDir);
                        foreach (string f in Directory.GetFiles(tplDir, "*.html"))
                        {
                            File.Copy(f, Path.Combine(tplBakDir, Path.GetFileName(f)), true);
                        }
                    }
                    string[] tplFiles = new string[] {
                        "Aircraft.html", "Facility.html", "GroundUnit.html", "Platform.html",
                        "Satellite.html", "Ship.html", "Submarine.html", "Weapon.html"
                    };
                    foreach (string tf in tplFiles)
                    {
                        ExtractEmbeddedResource("TPL_" + tf, Path.Combine(tplDir, tf));
                    }
                }

                // 部署已全量中文化的 Scenarios HTML 简报/教程正文（内嵌 zip，带 .orig 备份）
                DeployScenarioHtml(gamePath);

                // 部署中文化地名集（地图矢量地名层的数据源，必须先于标签瓦片重绘）
                DeployPlacenames(gamePath);

                // 部署 Symbols\Menu 中文按钮图与启动器中文资源
                DeploySymbolsAndLauncher(gamePath);

                // 寫入基本設定並確保無任何後台自啟動
                writeSettings(gamePath);
                CleanupUpdater(gamePath);

                // 兜底：对 zip 未覆盖的 HTML 做标题级替换 (带 .orig 备份)
                LocalizeScenarioBriefings(Path.Combine(gamePath, "Scenarios"));

                // 自动中文化 World Wind 标注图层瓦片 (WW/Cache/Stamen Terrain - Labels)
                LocalizeMapLabelTiles(gamePath);

                // 最后一步：写「安装清单」（部署实证）—— 它是「装了什么版本」的唯一真相，
                // 也是「一键还原后立即显示原版英文环境」的依据；失败不得留下半份清单。
                // 先做一次版本戳一致性自检：安装包内嵌词典 _meta.version 应与 CurrentVersion 一致，
                // 不一致说明打包时忘了同步版本戳（历史上出现过，会让“已装版本”与实际资产不符）。
                string bundleVer = VersionStore.PackageVersion(Application.ExecutablePath);
                if (!string.IsNullOrEmpty(bundleVer) && bundleVer != CurrentVersion)
                {
                    CmoLog.Warn("版本戳不一致：安裝包內嵌詞典 _meta.version=" + bundleVer
                        + "，安裝器 CurrentVersion=" + CurrentVersion);
                }
                Dictionary<string, string> manifestFiles = InstallManifest.HashEngineFiles(gamePath);
                if (!PackageAssets.VerifyEngineFiles(Application.ExecutablePath, manifestFiles, out errorMsg))
                {
                    CmoLog.Error(errorMsg);
                    return false;
                }
                if (!InstallManifest.Write(gamePath, CurrentVersion, Application.ExecutablePath, manifestFiles))
                {
                    errorMsg = "中文化檔案已部署，但寫入安裝清單失敗（狀態可能顯示不準，重新安裝即可修復）。";
                    return false;
                }
                // 缓存自身安装包：回退以整个安装包为单位（权威、自带全部资产）
                VersionStore.Cache(Application.ExecutablePath);

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }



        /// <summary>
        /// 部署中文化后的 World Wind 地名集（WW\PlacenameSets\*\0000_0000.wwp）。
        /// 地图上的国名/城市名以及 Stamen 标签瓦片重绘都以它为数据源；
        /// 不部署它则用户装完包地图仍是英文。覆盖前写 .orig 备份，卸载可还原。
        /// </summary>
        /// <summary>
        /// 部署 Symbols\Menu 中文按钮图（引擎启动时预加载，遗漏会导致按钮图仍是英文）
        /// 与启动器中文资源（Launcher\Localization\stringsChinese.resx）。
        /// 覆盖前写 .orig 备份，卸载可还原。
        /// </summary>
        private static void DeploySymbolsAndLauncher(string gamePath)
        {
            try
            {
                string menuDir = Path.Combine(gamePath, @"Symbols\Menu");
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("SYMBOLS_MENU_ZIP"))
                {
                    if (s != null && Directory.Exists(menuDir))
                    {
                        using (ZipArchive zip = new ZipArchive(s, ZipArchiveMode.Read))
                        {
                            foreach (ZipArchiveEntry e in zip.Entries)
                            {
                                if (e.Name.Length == 0) continue;
                                string dest;
                                if (!CmoPaths.TryResolveInside(menuDir, e.FullName, out dest))
                                {
                                    CmoLog.Warn("跳過越界 zip 條目: " + e.FullName);
                                    continue;
                                }
                                string dir = Path.GetDirectoryName(dest);
                                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                                string bak = dest + ".orig";
                                if (File.Exists(dest) && !File.Exists(bak)) File.Copy(dest, bak, true);
                                using (Stream es = e.Open())
                                using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
                                {
                                    es.CopyTo(fs);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                string locDir = Path.Combine(gamePath, @"Launcher\Localization");
                if (Directory.Exists(locDir))
                {
                    string dest = Path.Combine(locDir, "stringsChinese.resx");
                    string bak = dest + ".orig";
                    if (File.Exists(dest) && !File.Exists(bak)) File.Copy(dest, bak, true);
                    ExtractEmbeddedResource("LAUNCHER_ZH", dest);
                }
            }
            catch { }
        }

        /// <summary>卸载：还原 Symbols\Menu 与启动器中文资源。</summary>
        private static void RestoreSymbolsAndLauncher(string gamePath)
        {
            try
            {
                string menuDir = Path.Combine(gamePath, @"Symbols\Menu");
                if (Directory.Exists(menuDir))
                {
                    foreach (string f in Directory.GetFiles(menuDir, "*.orig", SearchOption.AllDirectories))
                    {
                        string target = f.Substring(0, f.Length - ".orig".Length);
                        File.Copy(f, target, true);
                        File.Delete(f);
                    }
                }
            }
            catch { }
            try
            {
                string orig = Path.Combine(gamePath, @"Launcher\Localization\stringsChinese.resx.orig");
                string res = Path.Combine(gamePath, @"Launcher\Localization\stringsChinese.resx");
                if (File.Exists(orig))
                {
                    File.Copy(orig, res, true);
                    File.Delete(orig);
                }
            }
            catch { }
        }

        private static void DeployPlacenames(string gamePath)
        {
            string[][] sets = new string[][] {
                new string[] { "PLACE_SYNOPTIC", @"WW\PlacenameSets\Synoptic" },
                new string[] { "PLACE_CITIES",   @"WW\PlacenameSets\Cities" }
            };
            foreach (string[] s in sets)
            {
                try
                {
                    string dir = Path.Combine(gamePath, s[1]);
                    if (!Directory.Exists(dir)) continue;
                    string target = Path.Combine(dir, "0000_0000.wwp");
                    string backup = target + ".orig";
                    if (File.Exists(target) && !File.Exists(backup)) File.Copy(target, backup, true);
                    ExtractEmbeddedResource(s[0], target);
                }
                catch { }
            }
        }

        /// <summary>卸载：还原原始英文地名集。</summary>
        private static void RestorePlacenames(string gamePath)
        {
            string[] dirs = new string[] { @"WW\PlacenameSets\Synoptic", @"WW\PlacenameSets\Cities" };
            foreach (string d in dirs)
            {
                try
                {
                    string target = Path.Combine(gamePath, d, "0000_0000.wwp");
                    string backup = target + ".orig";
                    if (File.Exists(backup))
                    {
                        File.Copy(backup, target, true);
                        File.Delete(backup);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 部署随包分发的已中文化 Scenarios HTML（内嵌 SCEN_HTML_ZIP）。
        /// 覆盖前先写 .orig 原始英文备份；已存在的 .orig 永不覆盖，保证卸载可 100% 还原。
        /// </summary>
        private static void DeployScenarioHtml(string gamePath)
        {
            try
            {
                string scenDir = Path.Combine(gamePath, "Scenarios");
                if (!Directory.Exists(scenDir)) return;
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("SCEN_HTML_ZIP"))
                {
                    if (s == null) return;
                    using (ZipArchive zip = new ZipArchive(s, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry e in zip.Entries)
                        {
                            if (e.Length == 0) continue;
                            string dest;
                            if (!CmoPaths.TryResolveInside(gamePath, e.FullName, out dest))
                            {
                                CmoLog.Warn("跳過越界 zip 條目: " + e.FullName);
                                continue;
                            }
                            string dir = Path.GetDirectoryName(dest);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            if (dest.EndsWith(".orig", StringComparison.OrdinalIgnoreCase))
                            {
                                if (File.Exists(dest)) continue;
                            }
                            else
                            {
                                string orig = dest + ".orig";
                                if (!File.Exists(orig) && File.Exists(dest)) File.Copy(dest, orig, true);
                            }
                            using (Stream es = e.Open())
                            using (FileStream fs = File.Create(dest))
                            {
                                es.CopyTo(fs);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static readonly System.Text.RegularExpressions.Regex HanRegex =
            new System.Text.RegularExpressions.Regex("[\u4e00-\u9fff]");

        private static string ReadScenarioHtml(string path, out Encoding encoding, out byte[] preamble)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096));
            System.Text.RegularExpressions.Match charset = System.Text.RegularExpressions.Regex.Match(
                header, @"charset\s*=\s*[""']?\s*([\w-]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            encoding = Encoding.UTF8;
            if (charset.Success)
            {
                try { encoding = Encoding.GetEncoding(charset.Groups[1].Value); }
                catch (ArgumentException) { } // 未知声明保持原有 UTF-8 回退。
            }
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (StreamReader reader = new StreamReader(stream, encoding, true))
            {
                string text = reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
                preamble = encoding.GetPreamble();
                bool hasPreamble = preamble.Length <= bytes.Length;
                for (int i = 0; hasPreamble && i < preamble.Length; i++)
                    hasPreamble = bytes[i] == preamble[i];
                if (!hasPreamble) preamble = new byte[0];
                return text;
            }
        }

        private static string EncodeScenarioReplacement(string value, Encoding encoding)
        {
            Encoding strict = (Encoding)encoding.Clone();
            strict.EncoderFallback = EncoderFallback.ExceptionFallback;
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                int length = char.IsSurrogatePair(value, i) ? 2 : 1;
                string character = value.Substring(i, length);
                try
                {
                    strict.GetByteCount(character);
                    result.Append(character);
                }
                catch (EncoderFallbackException)
                {
                    int codepoint = length == 2 ? char.ConvertToUtf32(value, i) : value[i];
                    result.Append("&#").Append(codepoint).Append(';');
                }
                i += length - 1;
            }
            return result.ToString();
        }

        private static void LocalizeScenarioBriefings(string scenDir)
        {
            if (!Directory.Exists(scenDir)) return;
            try
            {
                string[] htmls = Directory.GetFiles(scenDir, "*.htm*", SearchOption.AllDirectories);
                foreach (string h in htmls)
                {
                    if (h.EndsWith(".orig", StringComparison.OrdinalIgnoreCase)) continue;
                    Encoding encoding;
                    byte[] preamble;
                    string text = ReadScenarioHtml(h, out encoding, out preamble);
                    // 已由 zip 全量中文化的文件不得再用标题级替换覆盖（否则会降级成中英混排）
                    // 先按声明编码读取（BOM 优先），兼容原生汉字及数字实体载荷。
                    if (HanRegex.IsMatch(System.Net.WebUtility.HtmlDecode(text))) continue;
                    string orig = h + ".orig";
                    if (!File.Exists(orig))
                    {
                        File.Copy(h, orig, true);
                    }
                    // .orig 只用于卸载还原；补译必须基于当前版本，不能回退更新后的正文。
                    Func<string, string> replacement = value => EncodeScenarioReplacement(value, encoding);
                    string trans = text
                        .Replace("<h2>Briefing</h2>", replacement("<h2>作戰任務簡報 (Briefing)</h2>"))
                        .Replace("<h1>Introduction</h1>", replacement("<h1>戰役背景導言 (Introduction)</h1>"))
                        .Replace("<h3>Situation</h3>", replacement("<h3>戰場態勢 (Situation)</h3>"))
                        .Replace("<h3>Timeline:</h3>", replacement("<h3>行動時間線:</h3>"))
                        .Replace("<h3>Threat:</h3>", replacement("<h3>敵情威脅評估:</h3>"))
                        .Replace("<h3>Assets:</h3>", replacement("<h3>我方兵力配屬:</h3>"))
                        .Replace("<h3>Task:</h3>", replacement("<h3>作戰任務指標:</h3>"))
                        .Replace("<h3>Execution</h3>", replacement("<h3>作戰實施決心 (Execution)</h3>"))
                        .Replace("<h3>Friendly Forces</h3>", replacement("<h3>我方友鄰部隊 (Friendly Forces)</h3>"))
                        .Replace("<h3>Scenario Notes:</h3>", replacement("<h3>想定設計說明:</h3>"))
                        .Replace("<B>Original Author:</B>", replacement("<B>想定原作者:</B>"))
                        .Replace("<B>Location:</B>", replacement("<B>戰區位置:</B>"))
                        .Replace("<B>Playable Sides:</B>", replacement("<B>可扮演參戰方:</B>"))
                        .Replace("You are NOT, REPEAT NOT authorized to use nuclear weapons.", replacement("你嚴禁（重複：嚴禁）動用任何核武器。"));
                    if (trans != text)
                    {
                        byte[] content = encoding.GetBytes(trans);
                        using (FileStream stream = File.Create(h))
                        {
                            stream.Write(preamble, 0, preamble.Length);
                            stream.Write(content, 0, content.Length);
                        }
                    }
                }
            }
            catch { }
        }

        private static void RestoreScenarioBriefings(string scenDir)
        {
            if (!Directory.Exists(scenDir)) return;
            try
            {
                string[] origs = Directory.GetFiles(scenDir, "*.orig", SearchOption.AllDirectories);
                foreach (string orig in origs)
                {
                    string target = orig.Substring(0, orig.Length - 5);
                    File.Copy(orig, target, true);
                    File.Delete(orig);
                }
            }
            catch { }
        }


        /// <summary>
        /// 重绘缓存的地名标签瓦片（与运行时共用 CMOShared.MapLabelRenderer，
        /// 保证离线缓存与运行时像素一致）。返回实际成功保存的瓦片数。
        /// </summary>
        private static int LocalizeMapLabelTiles(string gamePath)
        {
            int saved = 0;
            try
            {
                string cacheDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels");
                string backupDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels.orig");
                if (!Directory.Exists(cacheDir)) return 0;

                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                    foreach (string f in Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories))
                    {
                        string rel = f.Substring(cacheDir.Length).TrimStart('\\', '/');
                        string dest = Path.Combine(backupDir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(f, dest, true);
                    }
                }

                List<MapLabelPlace> list = new List<MapLabelPlace>();
                string synWwp = Path.Combine(gamePath, @"WW\PlacenameSets\Synoptic\0000_0000.wwp");
                string citWwp = Path.Combine(gamePath, @"WW\PlacenameSets\Cities\0000_0000.wwp");

                Action<string, int> loadWwp = (path, lvl) =>
                {
                    if (!File.Exists(path)) return;
                    try
                    {
                        using (BinaryReader br = new BinaryReader(File.OpenRead(path), Encoding.UTF8))
                        {
                            int count = br.ReadInt32();
                            for (int i = 0; i < count; i++)
                            {
                                string name = br.ReadString();
                                float lat = br.ReadSingle();
                                float lon = br.ReadSingle();
                                br.ReadUInt32();
                                list.Add(new MapLabelPlace { Name = name, Lat = lat, Lon = lon, Level = lvl });
                            }
                        }
                    }
                    catch { }
                };

                loadWwp(synWwp, 0);
                loadWwp(citWwp, 1);
                if (list.Count == 0) return 0;

                string[] pngs = Directory.GetFiles(cacheDir, "*.png", SearchOption.AllDirectories);
                Parallel.ForEach(pngs, pngPath =>
                {
                    try
                    {
                        string norm = pngPath.Replace('\\', '/');
                        string[] parts = norm.Split('/');
                        int z = int.Parse(parts[parts.Length - 3]);
                        int x = int.Parse(parts[parts.Length - 2]);
                        string fName = parts[parts.Length - 1];
                        int y = int.Parse(fName.Substring(0, fName.IndexOf('.')));

                        int candidates;
                        int rendered;
                        using (Bitmap bmp = MapLabelRenderer.RenderTile(list, z, x, y, out candidates, out rendered))
                        {
                            bmp.Save(pngPath, ImageFormat.Png);
                            System.Threading.Interlocked.Increment(ref saved);
                        }
                    }
                    catch { }
                });
                return saved;
            }
            catch { return 0; }
        }


        private static void RestoreMapLabelTiles(string gamePath)
        {
            try
            {
                string cacheDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels");
                string backupDir = Path.Combine(gamePath, @"WW\Cache\Stamen Terrain - Labels.orig");
                if (Directory.Exists(backupDir))
                {
                    if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
                    Directory.Move(backupDir, cacheDir);
                }
            }
            catch { }
        }

        // ── 原子替换：目标文件可能正被运行中的游戏占用 ──────────────────────────
        // 重命名（MoveFileEx REPLACE_EXISTING）不受「文件被占用」影响：运行中的进程继续
        // 用旧映像，磁盘上换成新文件，游戏下次启动即生效；而按内容覆盖写会被共享冲突拒绝。
        private const uint MOVEFILE_REPLACE_EXISTING = 0x00000001;
        private const uint MOVEFILE_WRITE_THROUGH = 0x00000008;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);

        private static void ReplaceFileAtomic(string tempPath, string destPath)
        {
            if (!MoveFileEx(tempPath, destPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                int code = Marshal.GetLastWin32Error();
                throw new IOException("替換被拒絕 (Win32 " + code + "): "
                    + new System.ComponentModel.Win32Exception(code).Message);
            }
        }

        private static bool CopyFileAtomic(string srcPath, string destPath, out string error)
        {
            error = "";
            string temp = destPath + ".cmo_new";
            try
            {
                File.Copy(srcPath, temp, true);
                ReplaceFileAtomic(temp, destPath);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                error = "寫入 " + destPath + " 失敗: " + ex.Message
                    + "（若遊戲正在運行，先關閉遊戲後重試；此項失敗通常是目標檔案被佔用）";
                return false;
            }
        }

        private static bool ExtractEmbeddedResource(string resName, string destPath)
        {
            string ignored;
            return ExtractEmbeddedResource(resName, destPath, out ignored);
        }

        /// <summary>
        /// 释放内嵌资源到目标路径。error 区分「资源缺失」与「写入失败」——
        /// 旧实现把两类失败都返回 false，调用方只能报「安装包内未找到 XX 资源文件」，
        /// 于是在游戏运行时安装会看到与真实原因无关的报错（2026-10-18 用户实测）。
        /// </summary>
        private static bool ExtractEmbeddedResource(string resName, string destPath, out string error)
        {
            error = "";
            string temp = destPath + ".cmo_new";
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] allRes = asm.GetManifestResourceNames();
                string match = null;
                foreach (string name in allRes)
                {
                    if (name.EndsWith(resName, StringComparison.OrdinalIgnoreCase))
                    {
                        match = name;
                        break;
                    }
                }
                if (match == null)
                {
                    error = "安裝包內未找到 " + resName + " 資原始檔！";
                    return false;
                }

                using (Stream stream = asm.GetManifestResourceStream(match))
                {
                    if (stream == null)
                    {
                        error = "安裝包內未找到 " + resName + " 資原始檔！";
                        return false;
                    }
                    using (FileStream fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                    }
                }
                ReplaceFileAtomic(temp, destPath);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                error = "寫入 " + destPath + " 失敗: " + ex.Message
                    + "（若遊戲正在運行，先關閉遊戲後重試；此項失敗通常是目標檔案被佔用）";
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  自动升级（更新助手 CMO_Updater.exe）集成
        // ──────────────────────────────────────────────────────────────
        // 发布常量的唯一来源是 CMOShared.ReleaseConstants（禁止在本文件再写字面量副本）。
        private struct BindingRedirectSpec
        {
            public string Name;
            public string PublicKeyToken;
            public string OldVersion;
            public string NewVersion;

            public BindingRedirectSpec(string name, string token, string oldVer, string newVer)
            {
                Name = name;
                PublicKeyToken = token;
                OldVersion = oldVer;
                NewVersion = newVer;
            }
        }

        private static readonly BindingRedirectSpec[] RequiredRedirects = new BindingRedirectSpec[]
        {
            new BindingRedirectSpec("System.Runtime.CompilerServices.Unsafe", "b03f5f7f11d50a3a", "0.0.0.0-6.0.0.0", "6.0.0.0"),
            new BindingRedirectSpec("System.Buffers", "cc7b13ffcd2ddd51", "0.0.0.0-4.0.4.0", "4.0.4.0"),
            new BindingRedirectSpec("System.Memory", "cc7b13ffcd2ddd51", "0.0.0.0-4.0.1.2", "4.0.1.2")
        };

        private const string AsmBindingNs = "urn:schemas-microsoft-com:asm.v1";

        internal static bool EnsureRequiredConfigElements(XmlDocument doc)
        {
            if (doc == null) return false;
            bool modified = false;

            XmlElement root = doc.DocumentElement;
            if (root == null || !string.Equals(root.LocalName, "configuration", StringComparison.OrdinalIgnoreCase))
            {
                root = doc.CreateElement("configuration");
                doc.AppendChild(root);
                modified = true;
            }

            // 1. Ensure <startup useLegacyV2RuntimeActivationPolicy="true">
            XmlElement startup = null;
            XmlElement runtime = null;
            foreach (XmlNode n in root.ChildNodes)
            {
                if (n.NodeType == XmlNodeType.Element)
                {
                    if (string.Equals(n.LocalName, "startup", StringComparison.OrdinalIgnoreCase))
                        startup = (XmlElement)n;
                    else if (string.Equals(n.LocalName, "runtime", StringComparison.OrdinalIgnoreCase))
                        runtime = (XmlElement)n;
                }
            }

            if (startup == null)
            {
                startup = doc.CreateElement("startup");
                if (runtime != null) root.InsertBefore(startup, runtime);
                else root.AppendChild(startup);
                modified = true;
            }

            if (startup.GetAttribute("useLegacyV2RuntimeActivationPolicy") != "true")
            {
                startup.SetAttribute("useLegacyV2RuntimeActivationPolicy", "true");
                modified = true;
            }

            XmlElement supportedRuntime = null;
            foreach (XmlNode n in startup.ChildNodes)
            {
                if (n.NodeType == XmlNodeType.Element && string.Equals(n.LocalName, "supportedRuntime", StringComparison.OrdinalIgnoreCase))
                {
                    supportedRuntime = (XmlElement)n;
                    break;
                }
            }
            if (supportedRuntime == null)
            {
                supportedRuntime = doc.CreateElement("supportedRuntime");
                startup.AppendChild(supportedRuntime);
                modified = true;
            }
            if (!supportedRuntime.HasAttribute("version"))
            {
                supportedRuntime.SetAttribute("version", "v4.0");
                modified = true;
            }
            if (!supportedRuntime.HasAttribute("sku"))
            {
                supportedRuntime.SetAttribute("sku", ".NETFramework,Version=v4.8");
                modified = true;
            }

            // 2. Ensure <runtime>
            if (runtime == null)
            {
                runtime = doc.CreateElement("runtime");
                root.AppendChild(runtime);
                modified = true;
            }

            // 3. Find primary <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
            XmlElement primaryAsmBinding = null;
            foreach (XmlNode n in runtime.ChildNodes)
            {
                if (n.NodeType == XmlNodeType.Element && string.Equals(n.LocalName, "assemblyBinding", StringComparison.OrdinalIgnoreCase))
                {
                    primaryAsmBinding = (XmlElement)n;
                    break;
                }
            }
            if (primaryAsmBinding == null)
            {
                primaryAsmBinding = doc.CreateElement("assemblyBinding", AsmBindingNs);
                runtime.AppendChild(primaryAsmBinding);
                modified = true;
            }

            // 4. Ensure each required bindingRedirect exists
            for (int r = 0; r < RequiredRedirects.Length; r++)
            {
                BindingRedirectSpec spec = RequiredRedirects[r];
                XmlElement foundDep = null;

                foreach (XmlNode bNode in runtime.ChildNodes)
                {
                    if (bNode.NodeType != XmlNodeType.Element || !string.Equals(bNode.LocalName, "assemblyBinding", StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (XmlNode dNode in bNode.ChildNodes)
                    {
                        if (dNode.NodeType != XmlNodeType.Element || !string.Equals(dNode.LocalName, "dependentAssembly", StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (XmlNode idNode in dNode.ChildNodes)
                        {
                            if (idNode.NodeType == XmlNodeType.Element && string.Equals(idNode.LocalName, "assemblyIdentity", StringComparison.OrdinalIgnoreCase))
                            {
                                XmlElement idElem = (XmlElement)idNode;
                                if (string.Equals(idElem.GetAttribute("name"), spec.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    foundDep = (XmlElement)dNode;
                                    break;
                                }
                            }
                        }
                        if (foundDep != null) break;
                    }
                    if (foundDep != null) break;
                }

                if (foundDep == null)
                {
                    string ns = primaryAsmBinding.NamespaceURI;
                    foundDep = string.IsNullOrEmpty(ns) ? doc.CreateElement("dependentAssembly") : doc.CreateElement("dependentAssembly", ns);

                    XmlElement idElem = string.IsNullOrEmpty(ns) ? doc.CreateElement("assemblyIdentity") : doc.CreateElement("assemblyIdentity", ns);
                    idElem.SetAttribute("name", spec.Name);
                    idElem.SetAttribute("publicKeyToken", spec.PublicKeyToken);
                    idElem.SetAttribute("culture", "neutral");
                    foundDep.AppendChild(idElem);

                    XmlElement redirectElem = string.IsNullOrEmpty(ns) ? doc.CreateElement("bindingRedirect") : doc.CreateElement("bindingRedirect", ns);
                    redirectElem.SetAttribute("oldVersion", spec.OldVersion);
                    redirectElem.SetAttribute("newVersion", spec.NewVersion);
                    foundDep.AppendChild(redirectElem);

                    primaryAsmBinding.AppendChild(foundDep);
                    modified = true;
                }
                else
                {
                    XmlElement idElem = null;
                    XmlElement redirectElem = null;
                    foreach (XmlNode c in foundDep.ChildNodes)
                    {
                        if (c.NodeType == XmlNodeType.Element)
                        {
                            if (string.Equals(c.LocalName, "assemblyIdentity", StringComparison.OrdinalIgnoreCase))
                                idElem = (XmlElement)c;
                            else if (string.Equals(c.LocalName, "bindingRedirect", StringComparison.OrdinalIgnoreCase))
                                redirectElem = (XmlElement)c;
                        }
                    }

                    if (idElem != null)
                    {
                        if (idElem.GetAttribute("name") != spec.Name) { idElem.SetAttribute("name", spec.Name); modified = true; }
                        if (idElem.GetAttribute("publicKeyToken") != spec.PublicKeyToken) { idElem.SetAttribute("publicKeyToken", spec.PublicKeyToken); modified = true; }
                    }
                    if (redirectElem != null)
                    {
                        if (redirectElem.GetAttribute("newVersion") != spec.NewVersion) { redirectElem.SetAttribute("newVersion", spec.NewVersion); modified = true; }
                        if (redirectElem.GetAttribute("oldVersion") != spec.OldVersion) { redirectElem.SetAttribute("oldVersion", spec.OldVersion); modified = true; }
                    }
                    else
                    {
                        string ns = foundDep.NamespaceURI;
                        redirectElem = string.IsNullOrEmpty(ns) ? doc.CreateElement("bindingRedirect") : doc.CreateElement("bindingRedirect", ns);
                        redirectElem.SetAttribute("oldVersion", spec.OldVersion);
                        redirectElem.SetAttribute("newVersion", spec.NewVersion);
                        foundDep.AppendChild(redirectElem);
                        modified = true;
                    }
                }
            }

            return modified;
        }

        internal static bool RemoveDomainManagerNodes(XmlDocument doc)
        {
            if (doc == null || doc.DocumentElement == null) return false;
            bool modified = false;
            foreach (XmlNode rNode in doc.DocumentElement.ChildNodes)
            {
                if (rNode.NodeType != XmlNodeType.Element || !string.Equals(rNode.LocalName, "runtime", StringComparison.OrdinalIgnoreCase))
                    continue;

                List<XmlNode> toRemove = new List<XmlNode>();
                foreach (XmlNode child in rNode.ChildNodes)
                {
                    if (child.NodeType == XmlNodeType.Element &&
                        (string.Equals(child.LocalName, "appDomainManagerType", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(child.LocalName, "appDomainManagerAssembly", StringComparison.OrdinalIgnoreCase)))
                    {
                        toRemove.Add(child);
                    }
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    rNode.RemoveChild(toRemove[i]);
                    modified = true;
                }
            }
            return modified;
        }

        private static void NormalizeDomainManagerSetting(XmlDocument doc, XmlNode runtime, string name, string value)
        {
            XmlNodeList existing = runtime.SelectNodes(name);
            XmlElement setting = existing.Count == 0 ? doc.CreateElement(name) : (XmlElement)existing[0];
            for (int i = existing.Count - 1; i > 0; i--) runtime.RemoveChild(existing[i]);
            if (existing.Count == 0) runtime.AppendChild(setting);
            setting.SetAttribute("value", value);
        }

        public const string CurrentVersion = ReleaseConstants.Version;
        private const string RegPath = ReleaseConstants.RegPath;
        private const string RunKeyName = ReleaseConstants.RunKeyName;

        private static object RegGet(string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    return k == null ? null : k.GetValue(name);
                }
            }
            catch { return null; }
        }

        private static void RegSet(string name, object value, RegistryValueKind kind)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    if (k != null) k.SetValue(name, value, kind);
                }
            }
            catch { }
        }

        public static string InstalledVersion()
        {
            object v = RegGet("Version");
            return v == null ? "" : v.ToString();
        }

        /// <summary>状态显示用的已安装版本：清单优先，其次（迁移期）注册表。</summary>
        public static string StateVersion(string gamePath)
        {
            if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
            {
                InstallManifest m = InstallManifest.Read(gamePath);
                if (m != null && m.Version.Length > 0 && m.Version != "unknown") return m.Version;
            }
            string reg = CmoSettings.RegistryInstalledVersion();
            return reg == null ? "" : reg;
        }

        public static string LocalTimeText(string iso)
        {
            DateTime d;
            if (DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out d))
                return d.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return "未知時間";
        }

        public static void RememberGamePath(string gamePath)
        {
            if (!string.IsNullOrEmpty(gamePath)) RegSet("GamePath", gamePath, RegistryValueKind.String);
        }

        public static bool GetAutoCheck()
        {
            return false;
        }

        public static void SetAutoCheck(bool on)
        {
        }

        public static bool ToggleNeverRemind()
        {
            return false;
        }

        public static bool GetAutoStart()
        {
            return false;
        }

        public static string GetGamePath()
        {
            object v = RegGet("GamePath");
            if (v != null && Directory.Exists(v.ToString())) return v.ToString();
            return DetectGamePath();
        }

        public static void SetAutoStart(bool on)
        {
            try
            {
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (run != null) run.DeleteValue(RunKeyName, false);
                }
            }
            catch { }
        }

        public static string LastCheckText()
        {
            return "尚未檢查";
        }

        public static string NextCheckText()
        {
            return "已關閉";
        }

        /// <summary>自動更新已移除，不再部署更新助手。</summary>
        public static bool DeployUpdater(string gamePath)
        {
            return true;
        }

        /// <summary>寫入基本設定（僅遊戲路徑與版本；不設定開機自啟與背景更新）。</summary>
        public static void WriteRegistrySettings(string gamePath)
        {
            RegSet("GamePath", gamePath, RegistryValueKind.String);
            RegSet("Version", CurrentVersion, RegistryValueKind.String);
            SetAutoStart(false);
        }

        /// <summary>自動更新已移除，不再啟動更新助手。</summary>
        public static void StartUpdater(string gamePath)
        {
        }

        /// <summary>自動更新已移除。</summary>
        public static string LaunchUpdater(string gamePath, out string error)
        {
            error = "自動更新功能已移除。";
            return null;
        }

        /// <summary>卸载：结束更新助手、清理开机启动、注册表设置与程序本体。</summary>
        public static void CleanupUpdater(string gamePath)
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("CMO_Updater"))
                {
                    try { p.Kill(); } catch { }
                }
                System.Threading.Thread.Sleep(700);
            }
            catch { }
            try
            {
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (run != null) run.DeleteValue(RunKeyName, false);
                }
            }
            catch { }
            try
            {
                // 只清「安装态」的注册表版本号；更新频率/提醒方式/自启偏好等设置必须保留
                //（用户明确反馈过：还原后设置全没了）
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath, true))
                {
                    if (k != null) k.DeleteValue("Version", false);
                }
            }
            catch { }
            try
            {
                if (!string.IsNullOrEmpty(gamePath))
                {
                    string exe = Path.Combine(gamePath, "CMO_Updater.exe");
                    if (File.Exists(exe)) File.Delete(exe);
                }
            }
            catch { }
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "CMO_Updater.exe");
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch { }
        }

        public static bool Uninstall(string gamePath, out string errorMsg)
        {
            return Uninstall(gamePath, CleanupUpdater, out errorMsg);
        }

        internal static bool Uninstall(string gamePath, Action<string> cleanupUpdater, out string errorMsg)
        {
            errorMsg = "";
            if (string.IsNullOrEmpty(gamePath) || !File.Exists(Path.Combine(gamePath, "Command.exe")))
            {
                errorMsg = "未在目標路徑找到 Command.exe。";
                return false;
            }

            try
            {
                string cfgPath = Path.Combine(gamePath, "Command.exe.config");
                string bakPath = Path.Combine(gamePath, "Command.exe.config.cmo_bak");

                if (File.Exists(bakPath))
                {
                    File.Copy(bakPath, cfgPath, true);
                    File.Delete(bakPath);
                    try
                    {
                        string restoredTxt = File.ReadAllText(cfgPath);
                        if (restoredTxt.Contains("CMOLocalizer.LocalizerDomainManager"))
                        {
                            XmlDocument restoredDoc = new XmlDocument();
                            restoredDoc.Load(cfgPath);
                            if (RemoveDomainManagerNodes(restoredDoc))
                            {
                                EnsureRequiredConfigElements(restoredDoc);
                                restoredDoc.Save(cfgPath);
                            }
                        }
                    }
                    catch { }
                }
                else if (File.Exists(cfgPath))
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(cfgPath);
                        RemoveDomainManagerNodes(doc);
                        EnsureRequiredConfigElements(doc);
                        doc.Save(cfgPath);
                    }
                    catch { }
                }

                string dllDest = Path.Combine(gamePath, "CMOLocalizer.dll");
                string jsonDest = Path.Combine(gamePath, "CMOLocalizer.json");
                string curDir = AppDomain.CurrentDomain.BaseDirectory;
                // Only delete if installer is not running from the game folder itself, or leave backup copy
                if (!string.Equals(Path.GetFullPath(curDir).TrimEnd('\\'), Path.GetFullPath(gamePath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(dllDest)) File.Delete(dllDest);
                    if (File.Exists(jsonDest)) File.Delete(jsonDest);
                }
                // 还原武器数据库 HTML 模板
                string tplDir = Path.Combine(gamePath, "DB", "Templates");
                string tplBakDir = Path.Combine(gamePath, "DB", "Templates_Original_Backup");
                if (Directory.Exists(tplBakDir) && Directory.Exists(tplDir))
                {
                    foreach (string f in Directory.GetFiles(tplBakDir, "*.html"))
                    {
                        File.Copy(f, Path.Combine(tplDir, Path.GetFileName(f)), true);
                    }
                }

                // 还原 Scenarios 下的 .orig 简报备份
                RestoreScenarioBriefings(Path.Combine(gamePath, "Scenarios"));

                // 还原 World Wind 标注图层瓦片
                RestoreMapLabelTiles(gamePath);

                // 还原原始英文地名集
                RestorePlacenames(gamePath);

                // 还原 Symbols\Menu 与启动器资源
                RestoreSymbolsAndLauncher(gamePath);

                // 清理自动升级：结束助手进程、删除开机启动项、注册表设置与助手本体
                cleanupUpdater(gamePath);

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            string gp = DetectGamePath();

            if (args != null && args.Length > 0)
            {
                string cmd = null;
                string gpArg = null;
                int waitPid = 0;
                bool purgeSettings = false;

                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    if (a == "--install" || a == "/install") cmd = "install";
                    else if (a == "--uninstall" || a == "/uninstall") cmd = "uninstall";
                    else if (a == "--rollback" || a == "/rollback") cmd = "rollback";
                    else if (a == "--adopt" || a == "/adopt") cmd = "adopt";
                    else if (a == "--list-versions" || a == "/list-versions") cmd = "list";
                    else if (a == "--purge-settings" || a == "/purge-settings") purgeSettings = true;
                    else if (a == "--silent" || a == "/silent") { }
                    else if (a == "--restart-updater") { /* legacy no-op */ }
                    else if (a == "--wait-pid" && i + 1 < args.Length)
                    {
                        int.TryParse(args[++i], out waitPid);
                    }
                    else if (Directory.Exists(a)) gpArg = a;
                }

                if (gpArg != null) gp = gpArg;

                // 自更新：等待旧更新助手退出后再写文件，避免文件占用导致覆盖失败
                if (waitPid > 0)
                {
                    try
                    {
                        Process old = Process.GetProcessById(waitPid);
                        if (!old.WaitForExit(120000))
                        {
                            try { old.Kill(); } catch { }
                            System.Threading.Thread.Sleep(1500);
                        }
                    }
                    catch { }
                    System.Threading.Thread.Sleep(1200);
                }

                string err;
                if (cmd == "install")
                {
                    bool ok = Install(gp, out err);
                    Console.WriteLine(ok ? "[SUCCESS] Installed to " + gp : "[ERROR] " + err);
                    WriteLog(ok ? "[SUCCESS] install " + gp : "[ERROR] install " + gp + " -> " + err);
                    // 自動更新已移除
                    return ok ? 0 : 1;
                }
                else if (cmd == "uninstall")
                {
                    bool ok = Uninstall(gp, out err);
                    if (ok)
                    {
                        InstallManifest.Clear(gp);
                        if (purgeSettings) CmoSettings.Purge();
                    }
                    Console.WriteLine(ok
                        ? "[SUCCESS] Uninstalled from " + gp + (purgeSettings ? " (settings purged)" : " (settings kept)")
                        : "[ERROR] " + err);
                    return ok ? 0 : 1;
                }
                else if (cmd == "rollback")
                {
                    string installed = StateVersion(gp);
                    string prev = VersionStore.Previous(installed);
                    if (prev == null)
                    {
                        Console.WriteLine("[ERROR] 本地沒有比 v" + installed + " 更舊的安裝包緩存");
                        return 1;
                    }
                    bool ok = Rollback.Run(gp, prev, true, out err);
                    Console.WriteLine(ok ? "[SUCCESS] Rolled back to v" + prev : "[ERROR] " + err);
                    return ok ? 0 : 1;
                }
                else if (cmd == "adopt")
                {
                    bool ok = InstallManifest.Adopt(gp, CmoSettings.RegistryInstalledVersion());
                    InstallManifest m = InstallManifest.Read(gp);
                    Console.WriteLine(ok
                        ? "[SUCCESS] Adopted v" + (m == null ? "?" : m.Version) + " from " + gp
                        : "[ERROR] 未找到完整引擎檔案，無法登記安裝狀態");
                    return ok ? 0 : 1;
                }
                else if (cmd == "list")
                {
                    InstallManifest m = InstallManifest.Read(gp);
                    Console.WriteLine("game=" + gp);
                    Console.WriteLine("installed=" + (m == null ? "none" : "v" + m.Version + " (" + m.Source + ")"));
                    Console.WriteLine("cached=" + string.Join(",", VersionStore.CachedVersions().ToArray()));
                    return 0;
                }

                if (purgeSettings)
                {
                    CmoSettings.Purge();
                    Console.WriteLine("[SUCCESS] 已清除全部更新設定");
                    return 0;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm(gp));
            return 0;
        }
    }
}
