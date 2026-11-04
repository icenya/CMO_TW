// CMO 中文化更新助手 (CMO_Updater.exe)
// 职责：开机检查 + 每 6 小时定时检查 + 手动检查；托盘常驻；下载/校验/静默安装与失败回滚。
// 与安装器共用注册表设置 HKCU\Software\CMOChinese（安装器写入，本程序读取/更新）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using CMOShared;
using Microsoft.Win32;

namespace CMOUpdater
{
    internal static class Cfg
    {
        // 发布常量的唯一来源是 CMOShared.ReleaseConstants（禁止在本文件再写字面量副本）。
        public const string RegPath = ReleaseConstants.RegPath;
        public const string RunKeyName = ReleaseConstants.RunKeyName;
        public const string UpdaterVersion = ReleaseConstants.Version;
        public const string RepoApi = ReleaseConstants.RepoApi;

        public static string GamePath = "";
        public static string InstalledVersion = "";
        public static bool AutoCheck = true;
        public static int IntervalHours = 6;
        public static bool AutoStart = true;
        public static string SkippedVersion = "";
        public static string LastCheckUtc = "";
        public static string LastNotifiedVersion = "";
        public static string Mirror = ReleaseConstants.Mirror;
        public static int NotifyMode = 1;      // 0=弹窗 1=托盘气泡 2=静默
        public static bool AutoInstall = true;
        public static bool TrayOnClose = true;              // 关闭窗口时最小化到托盘
        public static string PendingInstallVersion = "";     // 升级中标记：目标版本
        public static string PendingPreviousVersion = "";    // 升级前版本（回滚目标）

        public static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }

        public static string DataDir
        {
            get
            {
                string d = CmoPaths.LocalDir;
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string UpdateDir
        {
            get
            {
                string d = Path.Combine(DataDir, "update");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        private static CmoSettings disk;   // 最近一次从磁盘读到的副本（用于并发写告警）

        public static void Load()
        {
            try
            {
                disk = CmoSettings.Load();
                GamePath = disk.GamePath;
                AutoCheck = disk.AutoCheck;
                IntervalHours = disk.CheckIntervalHours;
                AutoStart = disk.AutoStart;
                SkippedVersion = disk.SkippedVersion;
                LastCheckUtc = disk.LastCheckUtc;
                LastNotifiedVersion = disk.LastNotifiedVersion;
                Mirror = disk.Mirror;
                NotifyMode = disk.NotifyMode;
                AutoInstall = disk.AutoInstall;
                TrayOnClose = disk.TrayOnClose;
                PendingInstallVersion = disk.PendingInstallVersion;
                PendingPreviousVersion = disk.PendingPreviousVersion;
                if (IntervalHours < 1) IntervalHours = 6;
            }
            catch (Exception ex) { Log.Warn("讀取設定失敗: " + ex.Message); }
        }

        public static void Save()
        {
            try
            {
                CmoSettings fresh = CmoSettings.Load();   // 写前重读：可能被安装器或另一个窗口改过
                if (disk != null && fresh.ToJson() != disk.ToJson())
                    Log.Warn("檢測到設定被其它視窗/安裝器修改，本次以當前視窗的值爲準");

                CmoSettings now = new CmoSettings();
                now.GamePath = GamePath;
                now.AutoCheck = AutoCheck;
                now.CheckIntervalHours = IntervalHours;
                now.AutoStart = AutoStart;
                now.SkippedVersion = SkippedVersion;
                now.LastCheckUtc = LastCheckUtc;
                now.LastNotifiedVersion = LastNotifiedVersion;
                now.Mirror = Mirror;
                now.NotifyMode = NotifyMode;
                now.AutoInstall = AutoInstall;
                now.TrayOnClose = TrayOnClose;
                now.PendingInstallVersion = PendingInstallVersion;
                now.PendingPreviousVersion = PendingPreviousVersion;
                now.Save();
                disk = now;
            }
            catch (Exception ex) { Log.Warn("儲存設定失敗: " + ex.Message); }
        }

        /// <summary>游戏目录：设置里的路径，否则用助手自身所在目录（绿色包解压即用）。</summary>
        public static string GameDir()
        {
            string game = GamePath;
            if (string.IsNullOrEmpty(game)) game = Path.GetDirectoryName(ExePath);
            return game ?? "";
        }

        /// <summary>状态真相：安装清单 → 自愈登记（adopt）→ 注册表（迁移期）→ 空。</summary>
        public static string ResolveInstalled()
        {
            string game = GameDir();
            if (string.IsNullOrEmpty(game) || !Directory.Exists(game)) return "";
            InstallManifest m = InstallManifest.Read(game);
            if (m == null)
            {
                string reg = CmoSettings.RegistryInstalledVersion();
                if (InstallManifest.Adopt(game, reg))
                {
                    m = InstallManifest.Read(game);
                    Log.Info("已登記當前安裝狀態（adopt）: v" + (m == null ? "?" : m.Version));
                }
                else if (reg.Length > 0)
                {
                    return reg;   // 引擎文件不在但有安装记录 → 保留显示，不自作判断
                }
            }
            if (m == null) return "";
            string detail;
            if (InstallManifest.Verify(game, out detail) == ManifestState.Modified)
                Log.Warn("安裝清單校驗不通過: " + detail);
            return m.Version == "unknown" ? "" : m.Version;
        }

        public static bool IsAutoStartOn()
        {
            string stored = "";
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue(RunKeyName);
                        // 安装器按用户选择的目录写入，可能带正斜杠；比较前统一分隔符
                        if (v != null) stored = v.ToString().Replace('/', '\\');
                    }
                }
            }
            catch { }
            string self = ExePath.Replace('/', '\\');
            return stored.IndexOf(self, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void ApplyAutoStart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k == null) return;
                    if (on) k.SetValue(RunKeyName, "\"" + ExePath + "\" --boot", RegistryValueKind.String);
                    else k.DeleteValue(RunKeyName, false);
                }
                AutoStart = on;
                Save();
                Log.Info("開機啓動已" + (on ? "開啓" : "關閉"));
            }
            catch (Exception ex) { Log.Warn("寫入開機啓動失敗: " + ex.Message); }
        }

        public static string NextCheckText()
        {
            if (!AutoCheck) return "已關閉自動檢查";
            DateTime last;
            if (!DateTime.TryParse(LastCheckUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out last))
            {
                return "等待首次檢查";
            }
            DateTime next = last.ToLocalTime().AddHours(IntervalHours);
            if (next <= DateTime.Now) return "即將檢查";
            return next.ToString("今天 HH:mm", CultureInfo.InvariantCulture);
        }

        public static string LastCheckText()
        {
            if (string.IsNullOrEmpty(LastCheckUtc)) return "尚未檢查";
            DateTime last;
            if (!DateTime.TryParse(LastCheckUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out last))
                return "尚未檢查";
            return last.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }

    internal static class Log
    {
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get { return Path.Combine(Cfg.DataDir, "updater.log"); }
        }

        private static void Write(string level, string msg)
        {
            try
            {
                lock (Gate)
                {
                    string p = FilePath;
                    if (File.Exists(p) && new FileInfo(p).Length > 512 * 1024)
                    {
                        string old = p + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(p, old);
                    }
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + msg + "\r\n", Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void Info(string m) { Write("INFO", m); }
        public static void Warn(string m) { Write("WARN", m); }
        public static void Error(string m) { Write("ERROR", m); }
    }

    internal sealed class ReleaseInfo
    {
        public string Tag = "";
        public string Version = "";
        public string Notes = "";
        public string PublishedAt = "";
        public string HtmlUrl = "";
        public string SetupName = "";
        public string SetupUrl = "";
        public string SetupShaUrl = "";
        public long SetupSize = 0;

        public static int Compare(string a, string b)
        {
            try { return new Version(Clean(a)).CompareTo(new Version(Clean(b))); }
            catch { return string.Compare(a, b, StringComparison.OrdinalIgnoreCase); }
        }

        private static string Clean(string v)
        {
            if (v == null) return "0.0";
            v = v.Trim().TrimStart('v', 'V');
            int dash = v.IndexOf('-');
            if (dash > 0) v = v.Substring(0, dash);
            return v.Length == 0 ? "0.0" : v;
        }
    }

    internal static class UpdateService
    {
        public static void InitTls()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; }   // TLS 1.2
            catch { }
        }

        private static HttpWebRequest Req(string url, int timeoutMs)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "CMOChineseUpdater/" + Cfg.UpdaterVersion;
            r.Accept = "application/vnd.github+json";
            r.Timeout = timeoutMs;
            r.ReadWriteTimeout = timeoutMs;
            r.AllowAutoRedirect = true;
            try
            {
                IWebProxy p = WebRequest.DefaultWebProxy;
                if (p != null) { p.Credentials = CredentialCache.DefaultCredentials; r.Proxy = p; }
            }
            catch { }
            return r;
        }

        private static string GetString(string url, int timeoutMs)
        {
            using (HttpWebResponse resp = (HttpWebResponse)Req(url, timeoutMs).GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        public static ReleaseInfo QueryLatest(out string error)
        {
            error = null;
            try
            {
                InitTls();
                string json = GetString(Cfg.RepoApi, 20000);
                ReleaseInfo r = new ReleaseInfo();
                r.Tag = JsonField(json, "tag_name");
                r.Notes = JsonField(json, "body");
                r.PublishedAt = JsonField(json, "published_at");
                r.HtmlUrl = JsonField(json, "html_url");
                r.Version = r.Tag.TrimStart('v', 'V');

                foreach (string asset in AssetBlocks(json))
                {
                    string name = JsonField(asset, "name");
                    string url = JsonField(asset, "browser_download_url");
                    long size = JsonNumber(asset, "size");
                    if (name.StartsWith("CMO_Chinese_Setup", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (r.SetupUrl.Length == 0 || name.IndexOf(r.Version, StringComparison.OrdinalIgnoreCase) >= 0)
                        { r.SetupName = name; r.SetupUrl = url; r.SetupSize = size; }
                    }
                    else if (name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase) && name.StartsWith("CMO_Chinese_Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        if (name.IndexOf(r.Version, StringComparison.OrdinalIgnoreCase) >= 0 || r.SetupShaUrl.Length == 0)
                            r.SetupShaUrl = url;
                    }
                }
                if (r.SetupUrl.Length == 0) { error = "該 Release 未包含安裝包資產"; return null; }
                return r;
            }
            catch (WebException wex)
            {
                error = "網路請求失敗: " + wex.Message;
                Log.Warn("檢查更新失敗: " + wex.Message);
                return null;
            }
            catch (Exception ex)
            {
                error = "檢查更新失敗: " + ex.Message;
                Log.Error("檢查更新異常: " + ex);
                return null;
            }
        }

        private static List<string> AssetBlocks(string json)
        {
            List<string> list = new List<string>();
            int i = json.IndexOf("\"assets\"", StringComparison.Ordinal);
            if (i < 0) return list;
            string s = json.Substring(i);
            int first = s.IndexOf('[');
            int last = s.LastIndexOf(']');
            if (first < 0 || last <= first) return list;
            s = s.Substring(first + 1, last - first - 1);
            string[] parts = Regex.Split(s, "\\{\\s*\"url\"");
            for (int n = 1; n < parts.Length; n++) list.Add(parts[n]);
            return list;
        }

        private static string JsonField(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return "";
            return Unescape(m.Groups[1].Value);
        }

        private static long JsonNumber(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + name + "\"" + "\\s*:\\s*(-?\\d+)");
            long v = 0;
            if (m.Success) long.TryParse(m.Groups[1].Value, out v);
            return v;
        }

        private static string Unescape(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 < s.Length)
                        {
                            int cp;
                            if (int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cp))
                            { sb.Append((char)cp); i += 4; }
                        }
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>下载并返回本地路径；直连失败时按设置的镜像兜底。</summary>
        public static bool Download(string url, string dest, Action<int, long, long> progress, out string error)
        {
            error = null;
            List<string> tries = new List<string>();
            tries.Add(url);
            if (!string.IsNullOrEmpty(Cfg.Mirror))
            {
                string m = Cfg.Mirror.TrimEnd('/') + "/" + url;
                if (!tries.Contains(m)) tries.Add(m);
            }
            for (int i = 0; i < tries.Count; i++)
            {
                try
                {
                    InitTls();
                    if (File.Exists(dest)) File.Delete(dest);
                    using (HttpWebResponse resp = (HttpWebResponse)Req(tries[i], 30000).GetResponse())
                    using (Stream rs = resp.GetResponseStream())
                    using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
                    {
                        long total = resp.ContentLength;
                        byte[] buf = new byte[65536];
                        long got = 0;
                        int n, lastPct = -1;
                        while ((n = rs.Read(buf, 0, buf.Length)) > 0)
                        {
                            fs.Write(buf, 0, n);
                            got += n;
                            if (progress != null && total > 0)
                            {
                                int pct = (int)(got * 100 / total);
                                if (pct != lastPct) { lastPct = pct; progress(pct, got, total); }
                            }
                        }
                        fs.Flush();
                    }
                    if (i > 0) Log.Info("已通過鏡像下載: " + tries[i]);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    Log.Warn("下載失敗(" + tries[i] + "): " + ex.Message);
                }
            }
            return false;
        }

        public static string Sha256OfFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static bool VerifySha(string file, string shaUrl, out string detail)
        {
            return VerifySha(file, shaUrl, GetString, out detail);
        }

        internal static bool VerifySha(string file, string shaUrl, Func<string, int, string> readDigest, out string detail)
        {
            detail = "";
            if (string.IsNullOrWhiteSpace(shaUrl))
            {
                detail = "該 Release 未提供 SHA256 摘要，已拒絕自動安裝；請從發佈頁面手動覈驗。";
                Log.Warn(detail);
                return false;
            }
            try
            {
                string text = readDigest(shaUrl, 20000);
                Match m = Regex.Match(text, "\\b([0-9a-fA-F]{64})\\b");
                if (!m.Success) { detail = "摘要檔案格式無法識別"; return false; }
                string expect = m.Groups[1].Value.ToLowerInvariant();
                string actual = Sha256OfFile(file);
                if (expect != actual)
                {
                    detail = "SHA256 不匹配（期望 " + expect.Substring(0, 12) + "…，實際 " + actual.Substring(0, 12) + "…）";
                    Log.Error(detail);
                    return false;
                }
                detail = "SHA256 校驗通過";
                return true;
            }
            catch (Exception ex)
            {
                detail = "摘要校驗失敗: " + ex.Message;
                return false;
            }
        }

        public static bool GameRunning()
        {
            try { return Process.GetProcessesByName("Command").Length > 0; }
            catch { return false; }
        }

        /// <summary>静默运行新版安装器：等待本进程退出后再写入，避免文件占用。</summary>
        public static bool LaunchInstaller(string setupPath, string gamePath, out string error)
        {
            error = null;
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                string args = "--install \"" + gamePath + "\" --silent --wait-pid " + pid + " --restart-updater";
                ProcessStartInfo psi = new ProcessStartInfo(setupPath, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetDirectoryName(setupPath);
                Process.Start(psi);
                Log.Info("已啓動靜默安裝: " + setupPath + " " + args);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Error("啓動安裝器失敗: " + ex.Message);
                return false;
            }
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon icon;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Form host = new Form();
        private bool checking;
        private int failStreak;
        private int backoffHours;

        public TrayApp(bool boot)
        {
            host.ShowInTaskbar = false;
            host.WindowState = FormWindowState.Minimized;
            host.FormBorderStyle = FormBorderStyle.None;
            host.Load += delegate { host.Visible = false; };

            icon = new NotifyIcon();
            icon.Icon = IconFactory.Create();
            icon.Text = "CMO 中文化更新助手 v" + Cfg.UpdaterVersion;
            icon.Visible = true;
            icon.DoubleClick += delegate { CheckNow(true); };
            icon.ContextMenuStrip = BuildMenu();
            menu = icon.ContextMenuStrip;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 30000;
            timer.Tick += delegate { TimerTick(); };
            timer.Start();

            Log.Info("更新助手啓動 (開機啓動=" + Cfg.AutoStart + ", 自動檢查=" + Cfg.AutoCheck +
                     ", 間隔=" + Cfg.IntervalHours + "h, 已安裝=" + Cfg.InstalledVersion + ")");

            int delay = boot ? (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline ? 300 : 60) : 5;
            System.Threading.Timer t = null;
            t = new System.Threading.Timer(delegate
            {
                try { t.Dispose(); } catch { }
                Post(delegate { if (Cfg.AutoCheck) CheckNow(false); });
            }, null, delay * 1000, Timeout.Infinite);
        }

        private ContextMenuStrip menu;

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("立即檢查更新", null, delegate { CheckNow(true); });
            string prev = VersionStore.Previous(InstalledText());
            ToolStripMenuItem rb = new ToolStripMenuItem(prev != null
                ? ("回退到上一版本 v" + prev + "…")
                : "回退到上一版本（本地無緩存）");
            rb.Enabled = prev != null;
            string target = prev;
            rb.Click += delegate { StartRollback(target); };
            m.Items.Add(rb);
            m.Items.Add("打開更新設定…", null, delegate { OpenSettings(); });
            m.Items.Add("打開遊戲目錄", null, delegate { OpenGameDir(); });
            m.Items.Add("檢視日誌", null, delegate { OpenLog(); });
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem ver = new ToolStripMenuItem("當前版本 v" + InstalledText());
            ver.Enabled = false;
            m.Items.Add(ver);
            m.Items.Add("退出助手", null, delegate { ExitApp(); });
            return m;
        }

        /// <summary>已安装版本（无安装记录时显示助手自身版本，避免显示空白）。</summary>
        private static string InstalledText()
        {
            return Cfg.InstalledVersion.Length > 0 ? Cfg.InstalledVersion : Cfg.UpdaterVersion;
        }

        private void RebuildMenu()
        {
            ContextMenuStrip m = BuildMenu();
            ContextMenuStrip old = menu;
            menu = m;
            icon.ContextMenuStrip = m;
            if (old != null) old.Dispose();
        }

        private void Post(Action a)
        {
            try
            {
                if (host.IsHandleCreated) host.BeginInvoke(a);
                else a();
            }
            catch (Exception ex) { Log.Warn("調度失敗: " + ex.Message); }
        }

        private void TimerTick()
        {
            if (!Cfg.AutoCheck) return;
            DateTime last;
            if (!DateTime.TryParse(Cfg.LastCheckUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out last))
            {
                CheckNow(false);
                return;
            }
            int hours = Cfg.IntervalHours + backoffHours;
            if (DateTime.UtcNow >= last.AddHours(hours)) CheckNow(false);
            else if (failStreak > 0 && DateTime.UtcNow >= last.AddHours(backoffHours))
                CheckNow(false);
        }

        public void CheckNow(bool manual)
        {
            if (checking)
            {
                if (manual) MessageBox.Show("正在檢查更新，請稍候…", "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            checking = true;
            if (manual) icon.Text = "CMO 中文化更新助手 — 正在檢查更新…";
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                ReleaseInfo r = UpdateService.QueryLatest(out error);
                Cfg.LastCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                Cfg.InstalledVersion = Cfg.ResolveInstalled();   // 状态真相：安装清单（含自愈登记）
                string local = Cfg.InstalledVersion.Length > 0 ? Cfg.InstalledVersion : Cfg.UpdaterVersion;
                int cmp = (r == null || r.Version.Length == 0) ? 0 : ReleaseGate.Compare(r.Version, local);
                bool hasNew = cmp > 0;
                // 「可回退」只在本地真的缓存了该版本安装包时才提：
                // 否则（例如本地跑着未发布的更新版本）会把用户往旧版本赶。
                bool offerRollback = cmp < 0 && VersionStore.CachedPackage(r.Version) != null;
                if (r != null && (hasNew || offerRollback)) { failStreak = 0; backoffHours = 0; }
                if (r == null)
                {
                    failStreak++;
                    if (failStreak >= 3 && backoffHours == 0) backoffHours = 3;
                }
                Cfg.Save();
                Post(delegate
                {
                    checking = false;
                    icon.Text = "CMO 中文化更新助手 v" + Cfg.UpdaterVersion;
                    RebuildMenu();
                    OnChecked(r, hasNew, offerRollback, error, manual);
                });
            });
        }

        private void OnChecked(ReleaseInfo r, bool hasNew, bool offerRollback, string error, bool manual)
        {
            if (r == null)
            {
                if (manual) MessageBox.Show("檢查更新失敗：\n" + (error ?? "未知錯誤") + "\n\n可點擊托盤選單「檢視日誌」瞭解詳情。",
                    "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!hasNew && !offerRollback)
            {
                if (manual)
                    MessageBox.Show("當前已是最新版本 v" + Cfg.InstalledVersion + "（適配遊戲內部版本 " + ReleaseConstants.GameBuild + "）。\n\n上次檢查：" + Cfg.LastCheckText() +
                        "\n下次自動檢查：" + Cfg.NextCheckText(), "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!manual && r.Version == Cfg.SkippedVersion) return;

            string tip = offerRollback
                ? ("發現可回退版本 v" + r.Version + "（當前 v" + Cfg.InstalledVersion + "，本地已緩存該版本安裝包）")
                : ("發現新版本 v" + r.Version + "（當前 v" + Cfg.InstalledVersion + "，" + FormatSize(r.SetupSize) + "）");
            Log.Info(tip);
            bool showDialog = manual || Cfg.NotifyMode == 0;
            if (showDialog) PresentUpdate(r, offerRollback);
            else if (Cfg.NotifyMode == 1)
            {
                icon.BalloonTipTitle = offerRollback ? "CMO 中文化可回退版本" : "CMO 中文化更新可用";
                icon.BalloonTipText = tip + "。點擊托盤圖標檢視詳情。";
                icon.ShowBalloonTip(15000);
                Cfg.LastNotifiedVersion = r.Version;
                Cfg.Save();
            }
        }

        private void PresentUpdate(ReleaseInfo r, bool rollback)
        {
            using (UpdateForm f = new UpdateForm(r, rollback))
            {
                DialogResult dr = f.ShowDialog();
                Cfg.LastNotifiedVersion = r.Version;
                Cfg.Save();
                if (dr == DialogResult.OK)
                {
                    if (rollback) StartRollback(r.Version);
                    else if (Cfg.AutoInstall) StartInstall(r);
                    else OpenUrl(r.HtmlUrl);
                }
                else if (dr == DialogResult.Ignore)   // 稍后提醒
                {
                    Cfg.LastCheckUtc = DateTime.UtcNow.AddHours(-Cfg.IntervalHours).AddHours(1).ToString("o", CultureInfo.InvariantCulture);
                    Cfg.Save();
                }
                else if (dr == DialogResult.Abort)    // 不再提醒
                {
                    Silent();
                }
            }
        }

        private void Silent()
        {
            Cfg.AutoCheck = false;
            if (!string.IsNullOrEmpty(Cfg.SkippedVersion)) { }
            Cfg.Save();
            Log.Info("已按用戶選擇關閉自動檢查（不再提醒）");
        }

        /// <summary>确保游戏已退出（升级/回退前调用）；false = 用户取消或仍失败。</summary>
        private bool EnsureGameStopped(string action)
        {
            if (!UpdateService.GameRunning()) return true;
            DialogResult dr = MessageBox.Show("檢測到遊戲正在運行，" + action + "前需要先退出遊戲。\n\n是否立即退出遊戲並繼續？",
                "CMO 中文化更新助手", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return false;
            try
            {
                foreach (Process p in Process.GetProcessesByName("Command")) { p.CloseMainWindow(); }
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(1000);
                    if (!UpdateService.GameRunning()) break;
                }
                if (UpdateService.GameRunning())
                {
                    foreach (Process p in Process.GetProcessesByName("Command")) { try { p.Kill(); } catch { } }
                    Thread.Sleep(1500);
                }
            }
            catch (Exception ex) { Log.Warn("退出遊戲失敗: " + ex.Message); }
            if (UpdateService.GameRunning())
            {
                MessageBox.Show("遊戲仍在運行，請手動退出後重試。", "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private string PickGameDir()
        {
            string game = Cfg.GameDir();
            if (!string.IsNullOrEmpty(game) && Directory.Exists(game)) return game;
            FolderBrowserDialog fb = new FolderBrowserDialog();
            fb.Description = "請選擇 Command - Modern Operations 遊戲根目錄（含 Command.exe）";
            if (fb.ShowDialog() != DialogResult.OK) return null;
            Cfg.GamePath = fb.SelectedPath;
            Cfg.Save();
            return Cfg.GamePath;
        }

        /// <summary>回退：用本地缓存的上一版本安装包重装（与安装器共用 Rollback 实现）。</summary>
        public void StartRollback(string targetVersion)
        {
            if (string.IsNullOrEmpty(targetVersion))
            {
                MessageBox.Show("本地沒有更舊版本的安裝包緩存，無法離線回退。\n\n可在發佈頁下載對應版本安裝包後手動安裝。",
                    "回退到上一版本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!EnsureGameStopped("回退")) return;
            if (MessageBox.Show("將用本地緩存的 v" + targetVersion + " 安裝包重新安裝，把修補程式回退到 v" + targetVersion
                + "。\n\n確定繼續？", "回退到上一版本", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            string err;
            if (Rollback.Run(Cfg.GameDir(), targetVersion, false, out err))
            {
                Cfg.InstalledVersion = targetVersion;
                RebuildMenu();
                Log.Info("已回退到 v" + targetVersion);
                MessageBox.Show("已回退到 v" + targetVersion + "。\n\n如果遊戲正在運行過，請重新啓動遊戲。",
                    "回退成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log.Error("回退失敗: " + err);
                MessageBox.Show("回退失敗：\n" + err + "\n\n日誌：" + Log.FilePath,
                    "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StartInstall(ReleaseInfo r)
        {
            if (!EnsureGameStopped("升級")) return;

            string game = PickGameDir();
            if (game == null) return;
            using (InstallForm f = new InstallForm(r, game))
            {
                f.ShowDialog();
            }
        }

        private void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm())
            {
                f.ShowDialog();
            }
            timer.Interval = 30000;
            Log.Info("設定已更新: 自動檢查=" + Cfg.AutoCheck + ", 間隔=" + Cfg.IntervalHours + "h, 提醒方式=" + Cfg.NotifyMode);
        }

        private void OpenGameDir()
        {
            try { if (Directory.Exists(Cfg.GamePath)) Process.Start("explorer.exe", "\"" + Cfg.GamePath + "\""); }
            catch (Exception ex) { Log.Warn(ex.Message); }
        }

        private void OpenLog()
        {
            try { Process.Start("notepad.exe", Log.FilePath); }
            catch (Exception ex) { Log.Warn(ex.Message); }
        }

        private void ExitApp()
        {
            timer.Stop();
            icon.Visible = false;
            Log.Info("更新助手退出");
            Application.Exit();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "未知大小";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
        }

        private void OpenUrl(string url)
        {
            try { if (!string.IsNullOrEmpty(url)) Process.Start(url); }
            catch (Exception ex) { Log.Warn(ex.Message); }
        }
    }

    internal static class IconFactory
    {
        public static Icon Create()
        {
            try
            {
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(62, 127, 193)))
                    {
                        g.FillRectangle(b, 0, 0, 32, 32);
                    }
                    using (SolidBrush w = new SolidBrush(Color.White))
                    using (Font f = new Font("Microsoft YaHei", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("中", f, w, new RectangleF(0, 0, 32, 32), sf);
                    }
                }
                IntPtr h = bmp.GetHicon();
                using (Icon tmp = Icon.FromHandle(h))
                {
                    return (Icon)tmp.Clone();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("生成圖標失敗: " + ex.Message);
                return SystemIcons.Application;
            }
        }
    }

    /// <summary>「发现新版本」提示窗（设计稿 W2）。</summary>
    internal sealed class UpdateForm : Form
    {
        public UpdateForm(ReleaseInfo r, bool rollback)
        {
            this.Text = (rollback ? "發現可回退版本" : "發現新版本") + " — CMO 完整中文中文化修補程式";
            this.Size = new Size(560, 470);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 9.5f);

            Label head = new Label();
            head.Text = (rollback ? "發現可回退版本 v" : "發現新版本 v") + r.Version;
            head.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            head.ForeColor = Color.FromArgb(20, 60, 120);
            head.AutoSize = true;
            head.Location = new Point(20, 18);
            this.Controls.Add(head);

            Label sub = new Label();
            sub.Text = rollback
                ? ("發佈日期 " + SafeDate(r.PublishedAt) + "　·　當前版本 v" + Cfg.InstalledVersion + "　·　用本地緩存的安裝包回退")
                : ("發佈日期 " + SafeDate(r.PublishedAt) + "　·　安裝包 " + SizeText(r.SetupSize) + "　·　當前版本 v" + Cfg.InstalledVersion);
            sub.Text += "\n適配遊戲內部版本 " + ReleaseConstants.GameBuild + "（遊戲標題裏的 Build 號）";
            sub.ForeColor = Color.DimGray;
            sub.AutoSize = true;
            sub.Location = new Point(22, 52);
            this.Controls.Add(sub);

            Label lbl = new Label();
            lbl.Text = "更新內容:";
            lbl.AutoSize = true;
            lbl.Location = new Point(22, 84);
            this.Controls.Add(lbl);

            TextBox notes = new TextBox();
            notes.Multiline = true;
            notes.ReadOnly = true;
            notes.ScrollBars = ScrollBars.Vertical;
            notes.BackColor = Color.White;
            notes.Location = new Point(22, 108);
            notes.Size = new Size(500, 240);
            notes.Text = string.IsNullOrEmpty(r.Notes) ? "（該版本未提供更新說明）" : PlainNotes(r.Notes);
            this.Controls.Add(notes);

            Button ok = new Button();
            ok.Text = rollback ? "立即回退" : "立即升級";
            ok.Size = new Size(110, 34);
            ok.Location = new Point(22, 368);
            ok.BackColor = Color.FromArgb(30, 110, 200);
            ok.ForeColor = Color.White;
            ok.FlatStyle = FlatStyle.Flat;
            ok.DialogResult = DialogResult.OK;
            this.Controls.Add(ok);

            Button later = new Button();
            later.Text = "稍後提醒";
            later.Size = new Size(100, 34);
            later.Location = new Point(140, 368);
            later.DialogResult = DialogResult.Ignore;
            this.Controls.Add(later);

            Button never = new Button();
            never.Text = "不再提醒";
            never.Size = new Size(100, 34);
            never.Location = new Point(248, 368);
            never.DialogResult = DialogResult.Abort;
            this.Controls.Add(never);

            Button manual = new Button();
            manual.Text = "手動下載頁面";
            manual.Size = new Size(130, 34);
            manual.Location = new Point(392, 368);
            manual.Click += delegate
            {
                try { if (!string.IsNullOrEmpty(r.HtmlUrl)) Process.Start(r.HtmlUrl); }
                catch { }
            };
            this.Controls.Add(manual);

            Label tip = new Label();
            tip.Text = rollback
                ? "「不再提醒」= 關閉自動檢查並記住該版本；回退後仍可隨時升級回來。"
                : "「不再提醒」= 關閉自動檢查並記住該版本；日後可在更新助手中重新開啓。";
            tip.ForeColor = Color.DimGray;
            tip.AutoSize = true;
            tip.Location = new Point(22, 410);
            this.Controls.Add(tip);
        }

        /// <summary>把 Release 说明里的 Markdown 记号去掉，避免在纯文本框里显示 ** 与 #。</summary>
        internal static string PlainNotes(string raw)
        {
            string t = raw.Replace("\r\n", "\n");
            t = Regex.Replace(t, @"\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);
            t = Regex.Replace(t, @"\s*[-*]\s+", "· ", RegexOptions.Multiline);
            t = t.Replace("**", "").Replace("`", "");
            t = Regex.Replace(t, "\n{3,}", "\n\n");
            return t.Trim().Replace("\n", "\r\n");
        }

        internal static string SafeDate(string iso)
        {
            DateTime d;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out d))
                return d.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return "未知";
        }

        internal static string SizeText(long bytes)
        {
            if (bytes <= 0) return "未知大小";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
        }
    }

    /// <summary>下载/校验/安装进度窗（设计稿 W3）。</summary>
    internal sealed class InstallForm : Form
    {
        private readonly ReleaseInfo info;
        private readonly string game;
        private readonly ProgressBar bar;
        private readonly Label line1;
        private readonly Label line2;
        private readonly Label stages;
        private readonly Button cancel;
        private volatile bool abort;

        public InstallForm(ReleaseInfo r, string gamePath)
        {
            info = r;
            game = gamePath;
            this.Text = "正在更新 CMO 完整中文中文化修補程式";
            this.Size = new Size(560, 330);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 9.5f);
            this.FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (!cancel.Enabled)
                {
                    // 安装进行中：默认不允许关窗；勾了「关闭时最小化到托盘」就改成隐藏，后台继续。
                    e.Cancel = true;
                    if (e.CloseReason == CloseReason.UserClosing && Cfg.TrayOnClose) this.Hide();
                }
            };

            line1 = new Label();
            line1.Text = "正在下載 " + r.SetupName + " …";
            line1.AutoSize = true;
            line1.Location = new Point(20, 18);
            this.Controls.Add(line1);

            bar = new ProgressBar();
            bar.Location = new Point(20, 44);
            bar.Size = new Size(505, 20);
            bar.Minimum = 0;
            bar.Maximum = 100;
            this.Controls.Add(bar);

            line2 = new Label();
            line2.Text = "準備中…";
            line2.ForeColor = Color.DimGray;
            line2.AutoSize = true;
            line2.Location = new Point(20, 70);
            this.Controls.Add(line2);

            stages = new Label();
            stages.Text = "✔ 校驗新版本資訊（GitHub Releases）\r\n▶ 下載安裝包（失敗自動切換鏡像）\r\n○ 校驗 SHA256 摘要\r\n○ 備份 CMOLocalizer.dll → 靜默安裝\r\n○ 寫入版本號並清理臨時檔案";
            stages.Location = new Point(20, 98);
            stages.Size = new Size(505, 130);
            this.Controls.Add(stages);

            Button back = new Button();
            back.Text = "後臺運行";
            back.Size = new Size(100, 32);
            back.Location = new Point(310, 245);
            back.Click += delegate { this.Hide(); };
            this.Controls.Add(back);

            cancel = new Button();
            cancel.Text = "取消";
            cancel.Size = new Size(115, 32);
            cancel.Location = new Point(418, 245);
            cancel.Click += delegate
            {
                abort = true;
                cancel.Enabled = false;
                line1.Text = "正在取消…";
            };
            this.Controls.Add(cancel);

            this.Shown += delegate { System.Threading.ThreadPool.QueueUserWorkItem(delegate { Run(); }); };
        }

        private void Ui(Action a) { try { this.BeginInvoke(a); } catch { } }

        private void Run()
        {
            string dest = Path.Combine(Cfg.UpdateDir, info.SetupName);
            Action<int, long, long> prog = delegate(int pct, long got, long total)
            {
                Ui(delegate
                {
                    bar.Value = pct > 100 ? 100 : pct;
                    line2.Text = SizeText(got) + " / " + SizeText(total) + "　·　" + pct + "%";
                });
            };

            string error;
            if (!UpdateService.Download(info.SetupUrl, dest, prog, out error))
            {
                Fail("下載失敗：\n" + error + "\n\n可稍後在更新助手或安裝器內重試，或點擊「手動下載頁面」。");
                return;
            }
            if (abort) { Cancelled(dest); return; }

            Ui(delegate
            {
                bar.Value = 100;
                line2.Text = "下載完成，正在校驗摘要…";
                stages.Text = "✔ 校驗新版本資訊（GitHub Releases）\r\n✔ 下載安裝包\r\n▶ 校驗 SHA256 摘要\r\n○ 備份 CMOLocalizer.dll → 靜默安裝\r\n○ 寫入版本號並清理臨時檔案";
            });

            string detail;
            if (!UpdateService.VerifySha(dest, info.SetupShaUrl, out detail))
            {
                try { File.Delete(dest); } catch { }
                Fail("安裝包校驗失敗：\n" + detail + "\n\n已刪除下載檔案，未執行安裝。");
                return;
            }
            if (abort) { Cancelled(dest); return; }

            Ui(delegate
            {
                line1.Text = "正在安裝更新（約 10 秒），完成後助手會自動重啓…";
                line2.Text = detail + "　·　遊戲目錄：" + game;
                stages.Text = "✔ 校驗新版本資訊（GitHub Releases）\r\n✔ 下載安裝包\r\n✔ 校驗 SHA256 摘要\r\n▶ 備份 CMOLocalizer.dll → 靜默安裝\r\n○ 寫入版本號並清理臨時檔案";
            });

            // 缓存安装包：回退与"安裝失敗自動回滾"都靠它（只留最近 snapshot_keep 个）
            CmoSettings st = CmoSettings.Load();
            VersionStore.Cache(dest);
            VersionStore.Prune(st.SnapshotKeep);
            // 标记本次升级：助手重启后会校验清单，对不上就用缓存包自动回滚
            Cfg.PendingInstallVersion = info.Version;
            Cfg.PendingPreviousVersion = Cfg.InstalledVersion.Length > 0 ? Cfg.InstalledVersion : "";
            Cfg.Save();

            if (!UpdateService.LaunchInstaller(dest, game, out error))
            {
                Fail("啓動安裝程式失敗：\n" + error);
                return;
            }

            // 安装器会等待本进程退出后再写文件，这里立即结束自身。
            Ui(delegate
            {
                cancel.Enabled = false;
                Application.Exit();
            });
        }

        private void Cancelled(string dest)
        {
            try { if (File.Exists(dest)) File.Delete(dest); } catch { }
            Log.Info("用戶取消更新");
            Ui(delegate
            {
                this.Hide();
                MessageBox.Show("已取消更新。", "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            });
        }

        private void Fail(string msg)
        {
            Log.Error(msg.Replace("\n", " "));
            Ui(delegate
            {
                cancel.Enabled = true;
                MessageBox.Show(msg + "\n\n下載頁面：" + info.HtmlUrl, "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            });
        }

        private static string SizeText(long b)
        {
            if (b <= 0) return "?";
            return b < 1048576 ? (b / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB"
                               : (b / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
        }
    }

    /// <summary>更新设置窗（设计稿 W5）。</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly ComboBox cbInterval;
        private readonly RadioButton rDialog, rBalloon, rSilent;
        private readonly RadioButton rAuto, rManual;
        private readonly TextBox txtMirror;
        private readonly CheckBox chkStart;
        private readonly CheckBox chkAuto;
        private readonly CheckBox chkTray;
        private readonly Button btnNever;

        public SettingsForm()
        {
            this.Text = "更新設定 — CMO 中文化更新助手";
            this.Size = new Size(560, 470);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Microsoft YaHei UI", 9.5f);

            Label l1 = new Label(); l1.Text = "自動檢查頻率:"; l1.AutoSize = true; l1.Location = new Point(20, 20);
            this.Controls.Add(l1);

            cbInterval = new ComboBox();
            cbInterval.DropDownStyle = ComboBoxStyle.DropDownList;
            cbInterval.Location = new Point(140, 16);
            cbInterval.Size = new Size(180, 26);
            cbInterval.Items.AddRange(new object[] { "每次開機檢查一次", "每 1 小時", "每 3 小時", "每 6 小時（推薦）", "每 12 小時", "每 24 小時" });
            cbInterval.SelectedIndex = IntervalPresets.ToIndex(Cfg.IntervalHours);
            this.Controls.Add(cbInterval);

            chkAuto = new CheckBox();
            chkAuto.Text = "啓用自動檢查更新";
            chkAuto.Checked = Cfg.AutoCheck;
            chkAuto.AutoSize = true;
            chkAuto.Location = new Point(340, 18);
            this.Controls.Add(chkAuto);

            chkStart = new CheckBox();
            chkStart.Text = "隨 Windows 啓動並在後臺檢查更新";
            chkStart.Checked = Cfg.IsAutoStartOn();
            chkStart.AutoSize = true;
            chkStart.Location = new Point(20, 52);
            this.Controls.Add(chkStart);

            Label l2 = new Label(); l2.Text = "提醒方式:"; l2.AutoSize = true; l2.Location = new Point(20, 86);
            this.Controls.Add(l2);
            // WinForms 的单选按钮按「父容器」分组：提醒方式与升级方式必须各自放进独立容器，
            // 否则五颗单选钮同属一个组，最后一次 Checked 赋值会把前面的选择清空。
            Panel pNotify = new Panel();
            pNotify.Location = new Point(18, 104);
            pNotify.Size = new Size(510, 74);
            this.Controls.Add(pNotify);
            Panel pUpgrade = new Panel();
            pUpgrade.Location = new Point(18, 206);
            pUpgrade.Size = new Size(510, 52);
            this.Controls.Add(pUpgrade);
            rDialog = new RadioButton(); rDialog.Text = "彈窗提醒（發現新版本時彈出提示窗）"; rDialog.AutoSize = true; rDialog.Location = new Point(6, 6); pNotify.Controls.Add(rDialog);
            rBalloon = new RadioButton(); rBalloon.Text = "僅托盤氣泡提醒（不打斷當前操作）"; rBalloon.AutoSize = true; rBalloon.Location = new Point(6, 29); pNotify.Controls.Add(rBalloon);
            rSilent = new RadioButton(); rSilent.Text = "不提醒（僅在更新助手視窗內顯示）"; rSilent.AutoSize = true; rSilent.Location = new Point(6, 52); pNotify.Controls.Add(rSilent);
            rDialog.Checked = Cfg.NotifyMode == 0;
            rBalloon.Checked = Cfg.NotifyMode == 1;
            rSilent.Checked = Cfg.NotifyMode == 2;

            Label l3 = new Label(); l3.Text = "升級方式:"; l3.AutoSize = true; l3.Location = new Point(20, 188);
            this.Controls.Add(l3);
            rAuto = new RadioButton(); rAuto.Text = "自動下載並靜默安裝（需先退出遊戲）"; rAuto.AutoSize = true; rAuto.Location = new Point(6, 6); pUpgrade.Controls.Add(rAuto);
            rManual = new RadioButton(); rManual.Text = "只提示新版本，由我手動下載安裝"; rManual.AutoSize = true; rManual.Location = new Point(6, 29); pUpgrade.Controls.Add(rManual);
            if (Cfg.AutoInstall) rAuto.Checked = true; else rManual.Checked = true;

            Label l4 = new Label(); l4.Text = "下載鏡像（直連 GitHub 失敗時使用）:"; l4.AutoSize = true; l4.Location = new Point(20, 268);
            this.Controls.Add(l4);
            txtMirror = new TextBox();
            txtMirror.Location = new Point(24, 292);
            txtMirror.Size = new Size(400, 26);
            txtMirror.Text = Cfg.Mirror;
            this.Controls.Add(txtMirror);

            btnNever = new Button();
            btnNever.Text = Cfg.AutoCheck ? "不再提醒（關閉自動檢查）" : "開啓提醒";
            btnNever.Size = new Size(190, 30);
            btnNever.Location = new Point(24, 366);
            btnNever.Click += delegate
            {
                Cfg.AutoCheck = !Cfg.AutoCheck;
                chkAuto.Checked = Cfg.AutoCheck;
                if (!Cfg.AutoCheck) Cfg.SkippedVersion = Cfg.InstalledVersion;
                btnNever.Text = Cfg.AutoCheck ? "不再提醒（關閉自動檢查）" : "開啓提醒";
                Log.Info("不再提醒按鈕: AutoCheck=" + Cfg.AutoCheck);
            };
            this.Controls.Add(btnNever);

            chkTray = new CheckBox();
            chkTray.Text = "關閉更新視窗時最小化到托盤（後臺繼續）";
            chkTray.AutoSize = true;
            chkTray.Checked = Cfg.TrayOnClose;
            chkTray.Location = new Point(24, 332);
            this.Controls.Add(chkTray);

            Button save = new Button();
            save.Text = "儲存";
            save.Size = new Size(100, 32);
            save.Location = new Point(330, 366);
            save.BackColor = Color.FromArgb(30, 110, 200);
            save.ForeColor = Color.White;
            save.FlatStyle = FlatStyle.Flat;
            save.Click += delegate
            {
                Cfg.IntervalHours = IntervalPresets.ToHours(cbInterval.SelectedIndex);
                Cfg.AutoCheck = chkAuto.Checked;
                Cfg.NotifyMode = rDialog.Checked ? 0 : (rBalloon.Checked ? 1 : 2);
                Cfg.AutoInstall = rAuto.Checked;
                Cfg.Mirror = txtMirror.Text.Trim();
                Cfg.TrayOnClose = chkTray.Checked;
                Cfg.ApplyAutoStart(chkStart.Checked);
                Cfg.Save();
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(save);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Size = new Size(100, 32);
            cancel.Location = new Point(436, 366);
            cancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(cancel);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            UpdateService.InitTls();
            Cfg.Load();
            Cfg.InstalledVersion = Cfg.ResolveInstalled();
            Log.Info("---- 啓動 " + string.Join(" ", args) + " (v" + Cfg.UpdaterVersion + ") ----");

            bool boot = false;
            bool doCheck = false, doSettings = false, doVersion = false;
            if (args != null) foreach (string a in args)
            {
                if (a == "--boot") boot = true;
                else if (a == "--check") doCheck = true;
                else if (a == "--settings") doSettings = true;
                else if (a == "--version") doVersion = true;
            }
            if (doVersion) return 0;
            if (!doSettings) PostInstallCheck();   // 上次升级未完成 → 用缓存包自动回滚
            if (doCheck) return ManualCheck();
            if (doSettings) { Application.Run(new SettingsHost()); return 0; }

            bool created;
            using (Mutex mtx = new Mutex(true, "CMOChineseUpdater_SingleInstance", out created))
            {
                if (!created)
                {
                    Log.Info("已有實例在運行，本次啓動忽略");
                    return 0;
                }
                if (boot)
                {
                    // 开机启动：与安装状态同步一次（用户可能在设置里关掉了自启）
                    if (Cfg.AutoStart && !Cfg.IsAutoStartOn()) Cfg.ApplyAutoStart(true);
                    if (!Cfg.AutoStart && Cfg.IsAutoStartOn()) Cfg.ApplyAutoStart(false);
                }
                Application.Run(new TrayApp(boot));
            }
            return 0;
        }

        /// <summary>
        /// 升级后自检：上次升级留下的标记若未落实为对应清单版本，就用缓存的上一版本安装包回滚，
        /// 避免用户停在「半更新」状态。
        /// </summary>
        private static void PostInstallCheck()
        {
            if (string.IsNullOrEmpty(Cfg.PendingInstallVersion)) return;
            string target = Cfg.PendingInstallVersion;
            string prev = Cfg.PendingPreviousVersion;
            string game = Cfg.GameDir();
            Cfg.PendingInstallVersion = "";
            Cfg.PendingPreviousVersion = "";
            Cfg.Save();
            Cfg.InstalledVersion = Cfg.ResolveInstalled();
            if (Cfg.InstalledVersion == target)
            {
                Log.Info("升級自檢通過: v" + target);
                return;
            }
            Log.Error("升級自檢未通過: 期望 v" + target + "，實際 v" + (Cfg.InstalledVersion.Length > 0 ? Cfg.InstalledVersion : "(未安裝)"));
            if (string.IsNullOrEmpty(prev) || VersionStore.CachedPackage(prev) == null)
            {
                MessageBox.Show("檢測到上次更新未完成（當前未安裝或版本不符），且本地沒有 v" + prev
                    + " 的安裝包緩存，無法自動回滾。\n\n請重新運行 v" + target + " 安裝包修復。\n\n日誌：" + Log.FilePath,
                    "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string err;
            if (Rollback.Run(game, prev, false, out err))
            {
                Cfg.InstalledVersion = prev;
                MessageBox.Show("上次更新未完成，已自動回滾到 v" + prev + "。\n\n如需重試更新，請點擊托盤選單「立即檢查更新」。",
                    "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log.Error("自動回滾失敗: " + err);
                MessageBox.Show("上次更新未完成，自動回滾也失敗了：\n" + err + "\n\n請手動運行 v" + prev
                    + " 安裝包修復。\n\n日誌：" + Log.FilePath, "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static int ManualCheck()
        {
            string error;
            ReleaseInfo r = UpdateService.QueryLatest(out error);
            Cfg.LastCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            Cfg.Save();
            if (r == null)
            {
                MessageBox.Show("檢查更新失敗：\n" + (error ?? "未知錯誤") + "\n\n日誌：" + Log.FilePath,
                    "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }
            string current = Cfg.ResolveInstalled();
            Cfg.InstalledVersion = current;
            if (current.Length == 0) current = Cfg.UpdaterVersion;
            int cmp = ReleaseGate.Compare(r.Version, current);
            if (cmp == 0)
            {
                MessageBox.Show("當前已是最新版本 v" + current + "。\n\n上次檢查：" + Cfg.LastCheckText() +
                    "\n下次自動檢查：" + Cfg.NextCheckText(), "CMO 中文化更新助手",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            if (cmp < 0 && VersionStore.CachedPackage(r.Version) == null)
            {
                MessageBox.Show("當前版本 v" + current + " 高於線上最新版 v" + r.Version
                    + "（可能是未發佈的測試版本），且本地沒有該版本的安裝包緩存，因此不提供回退。",
                    "CMO 中文化更新助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            using (UpdateForm f = new UpdateForm(r, cmp < 0))
            {
                DialogResult dr = f.ShowDialog();
                if (dr == DialogResult.Abort)
                {
                    Cfg.AutoCheck = false;
                    Cfg.SkippedVersion = r.Version;
                    Cfg.Save();
                    Log.Info("用戶選擇不再提醒 v" + r.Version + "（已關閉自動檢查）");
                    return 0;
                }
                if (dr == DialogResult.OK)
                {
                    if (cmp < 0)   // 手动检查路径下的回退
                    {
                        string rberr;
                        Rollback.Run(Cfg.GameDir(), r.Version, false, out rberr);
                        MessageBox.Show(rberr.Length == 0 ? "已回退到 v" + r.Version : "回退失敗：" + rberr,
                            "CMO 中文化更新助手", MessageBoxButtons.OK,
                            rberr.Length == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                        return rberr.Length == 0 ? 0 : 4;
                    }
                    if (UpdateService.GameRunning())
                    {
                        MessageBox.Show("請先退出遊戲，再回到安裝器/更新助手執行升級。", "CMO 中文化更新助手",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return 3;
                    }
                    string game = Cfg.GamePath;
                    if (string.IsNullOrEmpty(game) || !Directory.Exists(game))
                    {
                        FolderBrowserDialog fb = new FolderBrowserDialog();
                        fb.Description = "請選擇 Command - Modern Operations 遊戲根目錄（含 Command.exe）";
                        if (fb.ShowDialog() != DialogResult.OK) return 0;
                        game = fb.SelectedPath;
                        Cfg.GamePath = game;
                        Cfg.Save();
                    }
                    using (InstallForm inf = new InstallForm(r, game))
                    {
                        inf.ShowDialog();
                    }
                }
                Cfg.LastNotifiedVersion = r.Version;
                Cfg.Save();
            }
            return 0;
        }
    }

    internal sealed class SettingsHost : ApplicationContext
    {
        public SettingsHost()
        {
            SettingsForm f = new SettingsForm();
            f.FormClosed += delegate { Application.Exit(); };
            f.Show();
        }
    }
}
