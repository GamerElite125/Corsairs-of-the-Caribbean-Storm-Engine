// Corsairs of the Caribbean launcher
// Plays the game, shows news and patch notes, installs updates published as GitHub Releases.
// Updates only ever write the files contained in a patch, and back up every file they replace
// to Docs_AND_Report\UpdateBackups\<version>\ first. Nothing is ever deleted.
//
// Build (no SDK needed, uses the .NET Framework 4.8 compiler that ships with Windows):
//   build.cmd
//
// Settings: launcher_cotc.ini next to the exe
//   [Updates]
//   Repo=owner/name        GitHub repository that holds releases and news.json
//   Branch=main            branch for news.json
//   CheckOnStart=1

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Corsairs of the Caribbean Launcher")]
[assembly: System.Reflection.AssemblyProduct("Corsairs of the Caribbean")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace CotcLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (SelfUpdate.FinishPending()) return;
            Application.Run(new MainForm());
        }
    }

    // ------------------------------------------------------------------ theme
    static class Theme
    {
        public static readonly Color Gold = Color.FromArgb(232, 196, 104);
        public static readonly Color GoldDim = Color.FromArgb(170, 140, 70);
        public static readonly Color Wood = Color.FromArgb(58, 38, 22);
        public static readonly Color WoodHover = Color.FromArgb(92, 60, 32);
        public static readonly Color Panel = Color.FromArgb(200, 14, 12, 10);
        public static readonly Color Text = Color.FromArgb(236, 228, 210);
        public static Font Title = new Font("Georgia", 15f, FontStyle.Bold);
        public static Font Button = new Font("Georgia", 12f, FontStyle.Bold);
        public static Font Body = new Font("Segoe UI", 10f);
        public static Font Small = new Font("Segoe UI", 9f);
    }

    class GoldButton : Control
    {
        bool hover;
        public bool Selected;
        public GoldButton(string text)
        {
            Text = text;
            Font = Theme.Button;
            Size = new Size(230, 44);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath p = Rounded(r, 6))
            {
                Color fill = (hover || Selected) && Enabled ? Theme.WoodHover : Theme.Wood;
                using (LinearGradientBrush b = new LinearGradientBrush(r, ControlPaint.Light(fill, 0.15f), fill, 90f))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Enabled ? (hover || Selected ? Theme.Gold : Theme.GoldDim) : Color.Gray, 2f))
                    g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Text, Font, r, Enabled ? Theme.Gold : Color.Gray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        public static GraphicsPath Rounded(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    class GlassPanel : Panel
    {
        public GlassPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = GoldButton.Rounded(r, 8))
            {
                using (SolidBrush b = new SolidBrush(Theme.Panel)) e.Graphics.FillPath(b, p);
                using (Pen pen = new Pen(Theme.GoldDim, 1.5f)) e.Graphics.DrawPath(pen, p);
            }
            base.OnPaint(e);
        }
    }

    // ------------------------------------------------------------------ small helpers
    static class Paths
    {
        public static string Root { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string Ini { get { return Path.Combine(Root, "launcher_cotc.ini"); } }
        public static string VersionFile { get { return Path.Combine(Root, "cotc_version.txt"); } }
        public static string EngineIni { get { return Path.Combine(Root, "engine.ini"); } }
        public static string Engine { get { return Path.Combine(Root, "engine.exe"); } }
        public static string Backups { get { return Path.Combine(Root, "Docs_AND_Report\\UpdateBackups"); } }
        public static string Asset(string n) { return Path.Combine(Root, "launcher_assets\\" + n); }
    }

    static class Ini
    {
        public static string Get(string section, string key, string def)
        {
            if (!File.Exists(Paths.Ini)) return def;
            string cur = "";
            foreach (string raw in File.ReadAllLines(Paths.Ini))
            {
                string l = raw.Trim();
                if (l.StartsWith("[") && l.EndsWith("]")) { cur = l.Substring(1, l.Length - 2); continue; }
                int eq = l.IndexOf('=');
                if (eq > 0 && string.Equals(cur, section, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(l.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    return l.Substring(eq + 1).Trim();
            }
            return def;
        }
    }

    static class GameVersion
    {
        public static string Local()
        {
            try { if (File.Exists(Paths.VersionFile)) return File.ReadAllText(Paths.VersionFile).Trim(); }
            catch { }
            return "0.0.0";
        }
        public static void Save(string v) { File.WriteAllText(Paths.VersionFile, v); }
        public static int[] Parse(string v)
        {
            v = (v ?? "").Trim().TrimStart('v', 'V');
            string[] parts = v.Split('.', '-', ' ');
            int[] r = new int[4];
            for (int i = 0; i < 4 && i < parts.Length; i++) { int n; if (int.TryParse(parts[i], out n)) r[i] = n; }
            return r;
        }
        public static int Compare(string a, string b)
        {
            int[] x = Parse(a), y = Parse(b);
            for (int i = 0; i < 4; i++) if (x[i] != y[i]) return x[i].CompareTo(y[i]);
            return 0;
        }
    }

    // engine.ini: change "key = value" lines in place, keep everything else exactly
    static class EngineIni
    {
        public static string Get(string key, string def)
        {
            if (!File.Exists(Paths.EngineIni)) return def;
            foreach (string l in File.ReadAllLines(Paths.EngineIni, Encoding.Default))
            {
                int eq = l.IndexOf('=');
                if (eq > 0 && !l.TrimStart().StartsWith(";") && l.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return l.Substring(eq + 1).Trim();
            }
            return def;
        }
        public static void Set(Dictionary<string, string> values)
        {
            List<string> lines = new List<string>(File.ReadAllLines(Paths.EngineIni, Encoding.Default));
            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Count; i++)
            {
                string l = lines[i];
                int eq = l.IndexOf('=');
                if (eq <= 0 || l.TrimStart().StartsWith(";") || l.TrimStart().StartsWith("[")) continue;
                string k = l.Substring(0, eq).Trim();
                if (values.ContainsKey(k) && !done.Contains(k)) { lines[i] = k + " = " + values[k]; done.Add(k); }
            }
            int insertAt = lines.FindIndex(x => x.TrimStart().StartsWith("["));
            if (insertAt < 0) insertAt = lines.Count;
            foreach (KeyValuePair<string, string> kv in values)
                if (!done.Contains(kv.Key)) lines.Insert(insertAt++, kv.Key + " = " + kv.Value);
            File.Copy(Paths.EngineIni, Paths.EngineIni + ".bak", true);
            File.WriteAllLines(Paths.EngineIni, lines.ToArray(), Encoding.Default);
        }
    }

    static class Net
    {
        static Net() { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } // TLS 1.2
        public static string GetText(string url)
        {
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            rq.UserAgent = "CotC-Launcher";
            rq.Accept = "application/vnd.github+json, */*";
            rq.Timeout = 15000;
            using (WebResponse rs = rq.GetResponse())
            using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }
        public static void Download(string url, string file, Action<long, long> progress)
        {
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            rq.UserAgent = "CotC-Launcher";
            rq.Accept = "application/octet-stream";
            rq.Timeout = 30000;
            using (WebResponse rs = rq.GetResponse())
            using (Stream s = rs.GetResponseStream())
            using (FileStream f = File.Create(file))
            {
                long total = rs.ContentLength, got = 0;
                byte[] buf = new byte[1 << 16];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    f.Write(buf, 0, n); got += n;
                    if (progress != null) progress(got, total);
                }
            }
        }
    }

    // ------------------------------------------------------------------ updates
    class Release
    {
        public string Tag, Name, Body, Date, ZipUrl;
    }

    static class Updates
    {
        public static string Repo { get { return Ini.Get("Updates", "Repo", ""); } }
        public static string Branch { get { return Ini.Get("Updates", "Branch", "main"); } }

        public static List<Release> FetchReleases()
        {
            string json = Net.GetText("https://api.github.com/repos/" + Repo + "/releases?per_page=50");
            object[] arr = new JavaScriptSerializer().DeserializeObject(json) as object[];
            List<Release> list = new List<Release>();
            if (arr == null) return list;
            foreach (object o in arr)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null) continue;
                if (d.ContainsKey("draft") && (d["draft"] is bool) && (bool)d["draft"]) continue;
                Release r = new Release();
                r.Tag = Str(d, "tag_name"); r.Name = Str(d, "name"); r.Body = Str(d, "body");
                r.Date = Str(d, "published_at"); if (r.Date.Length > 10) r.Date = r.Date.Substring(0, 10);
                object[] assets = d.ContainsKey("assets") ? d["assets"] as object[] : null;
                if (assets != null)
                    foreach (object a in assets)
                    {
                        Dictionary<string, object> ad = a as Dictionary<string, object>;
                        if (ad != null && Str(ad, "name").EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        { r.ZipUrl = Str(ad, "browser_download_url"); break; }
                    }
                list.Add(r);
            }
            list.Sort((a, b) => GameVersion.Compare(a.Tag, b.Tag));
            return list;
        }

        public static List<Dictionary<string, object>> FetchNews()
        {
            string json = Net.GetText("https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/news.json");
            object[] arr = new JavaScriptSerializer().DeserializeObject(json) as object[];
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            if (arr != null) foreach (object o in arr) { Dictionary<string, object> d = o as Dictionary<string, object>; if (d != null) list.Add(d); }
            return list;
        }

        public static string Str(Dictionary<string, object> d, string k)
        {
            return d.ContainsKey(k) && d[k] != null ? d[k].ToString() : "";
        }

        // Installs one patch zip: backs up every file it replaces, never deletes anything.
        public static int Install(string zip, string tag, Action<string> log)
        {
            string root = Path.GetFullPath(Paths.Root);
            string backup = Path.Combine(Paths.Backups, tag);
            string selfExe = Path.GetFullPath(Application.ExecutablePath);
            int count = 0;
            using (ZipArchive za = ZipFile.OpenRead(zip))
            {
                foreach (ZipArchiveEntry en in za.Entries)
                {
                    if (string.IsNullOrEmpty(en.Name)) continue; // folder entry
                    string rel = en.FullName.Replace('/', '\\').TrimStart('\\');
                    string target = Path.GetFullPath(Path.Combine(root, rel));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { log("skipped unsafe path " + rel); continue; }
                    if (File.Exists(target))
                    {
                        string b = Path.Combine(backup, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(b));
                        if (!File.Exists(b)) File.Copy(target, b);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    if (string.Equals(target, selfExe, StringComparison.OrdinalIgnoreCase))
                    {
                        en.ExtractToFile(target + ".new", true); // swapped in when the launcher restarts
                        log("launcher update staged, it will finish when the launcher restarts");
                    }
                    else
                    {
                        en.ExtractToFile(target, true);
                    }
                    count++;
                }
            }
            return count;
        }
    }

    static class SelfUpdate
    {
        // If an update staged "<exe>.new", swap it in now and restart.
        public static bool FinishPending()
        {
            try
            {
                string exe = Application.ExecutablePath;
                if (!File.Exists(exe + ".new")) return false;
                string cmd = "/c timeout /t 1 /nobreak >nul & move /y \"" + exe + ".new\" \"" + exe + "\" & start \"\" \"" + exe + "\"";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd);
                psi.CreateNoWindow = true; psi.UseShellExecute = false;
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }
    }

    // ------------------------------------------------------------------ main window
    class MainForm : Form
    {
        GlassPanel content;
        RichTextBox text;
        Panel settingsPanel;
        Label status, version;
        ProgressBar bar;
        GoldButton bPlay, bNews, bUpdates, bSettings, bCredits, bFolder, bExit, bInstall;
        List<Release> pending = new List<Release>();
        CheckBox cFull, cVsync, cFps;
        ComboBox cRes;

        public MainForm()
        {
            Text = "Corsairs of the Caribbean";
            ClientSize = new Size(1180, 680);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Color.Black;
            try { BackgroundImage = Image.FromFile(Paths.Asset("background.jpg")); BackgroundImageLayout = ImageLayout.Stretch; } catch { }
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            PictureBox logo = new PictureBox();
            logo.BackColor = Color.Transparent;
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Bounds = new Rectangle(14, 6, 420, 210);
            try { logo.Image = Image.FromFile(Paths.Asset("logo.png")); } catch { }
            Controls.Add(logo);

            int x = 40, y = 230, step = 52;
            bPlay = Btn("PLAY", x, y); y += step + 10;
            bNews = Btn("News", x, y); y += step;
            bUpdates = Btn("Updates", x, y); y += step;
            bSettings = Btn("Settings", x, y); y += step;
            bCredits = Btn("Credits", x, y); y += step;
            bFolder = Btn("Game folder", x, y); y += step;
            bExit = Btn("Exit", x, y);
            bPlay.Font = new Font("Georgia", 15f, FontStyle.Bold);
            bPlay.Height = 54;

            content = new GlassPanel();
            content.Bounds = new Rectangle(310, 222, 840, 380);
            Controls.Add(content);

            text = new RichTextBox();
            text.ReadOnly = true;
            text.BorderStyle = BorderStyle.None;
            text.BackColor = Color.FromArgb(20, 17, 14);
            text.ForeColor = Theme.Text;
            text.Font = Theme.Body;
            text.Bounds = new Rectangle(18, 16, 804, 296);
            text.DetectUrls = true;
            text.LinkClicked += (s, e) => { try { Process.Start(e.LinkText); } catch { } };
            content.Controls.Add(text);

            bInstall = new GoldButton("Install updates");
            bInstall.Bounds = new Rectangle(18, 322, 230, 44);
            bInstall.Visible = false;
            bInstall.Click += (s, e) => InstallPending();
            content.Controls.Add(bInstall);

            BuildSettings();

            status = new Label();
            status.BackColor = Color.Transparent; status.ForeColor = Theme.Text; status.Font = Theme.Small;
            status.Bounds = new Rectangle(310, 612, 600, 22);
            Controls.Add(status);
            bar = new ProgressBar();
            bar.Bounds = new Rectangle(310, 638, 600, 14);
            bar.Visible = false;
            Controls.Add(bar);
            version = new Label();
            version.BackColor = Color.Transparent; version.ForeColor = Theme.Gold; version.Font = Theme.Small;
            version.TextAlign = ContentAlignment.MiddleRight;
            version.Bounds = new Rectangle(880, 612, 270, 22);
            Controls.Add(version);
            RefreshVersion();

            bPlay.Click += (s, e) => Play();
            bNews.Click += (s, e) => ShowNews();
            bUpdates.Click += (s, e) => ShowUpdates();
            bSettings.Click += (s, e) => ShowSettings();
            bCredits.Click += (s, e) => ShowCredits();
            bFolder.Click += (s, e) => { try { Process.Start("explorer.exe", "\"" + Paths.Root + "\""); } catch { } };
            bExit.Click += (s, e) => Close();

            ShowNews();
            if (Ini.Get("Updates", "CheckOnStart", "1") == "1" && Updates.Repo != "") CheckUpdates(false);
        }

        GoldButton Btn(string t, int x, int y)
        {
            GoldButton b = new GoldButton(t);
            b.Location = new Point(x, y);
            Controls.Add(b);
            return b;
        }

        void SelectTab(GoldButton b)
        {
            foreach (GoldButton o in new GoldButton[] { bNews, bUpdates, bSettings, bCredits }) { o.Selected = (o == b); o.Invalidate(); }
            settingsPanel.Visible = (b == bSettings);
            text.Visible = (b != bSettings);
            bInstall.Visible = (b == bUpdates && pending.Count > 0);
        }

        void RefreshVersion() { version.Text = "Version " + GameVersion.Local(); }
        void Status(string s) { if (InvokeRequired) { BeginInvoke(new Action<string>(Status), s); return; } status.Text = s; }

        void Write(string title, string body)
        {
            text.Clear();
            text.SelectionFont = Theme.Title; text.SelectionColor = Theme.Gold;
            text.AppendText(title + "\n\n");
            text.SelectionFont = Theme.Body; text.SelectionColor = Theme.Text;
            text.AppendText(body);
            text.SelectionStart = 0;
        }

        // ---------------- play
        void Play()
        {
            if (!File.Exists(Paths.Engine)) { MessageBox.Show(this, "engine.exe was not found in\n" + Paths.Root, Text); return; }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Paths.Engine);
                psi.WorkingDirectory = Paths.Root;
                Process p = Process.Start(psi);
                WindowState = FormWindowState.Minimized;
                Status("Game running...");
                p.EnableRaisingEvents = true;
                p.Exited += (s, e) => BeginInvoke(new Action(() => { WindowState = FormWindowState.Normal; Status("Game closed."); }));
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not start the game:\n" + ex.Message, Text); }
        }

        // ---------------- news
        void ShowNews()
        {
            SelectTab(bNews);
            if (Updates.Repo == "")
            {
                Write("News", "Welcome to Corsairs of the Caribbean.\n\nNews and updates appear here once an update source is set up.\n" +
                    "To set it up, put your GitHub repository in launcher_cotc.ini:\n\n[Updates]\nRepo=your-name/your-repository\n\n" +
                    "See Docs_AND_Report\\LauncherSource\\PUBLISHING_UPDATES.md for how to publish news and patches.");
                return;
            }
            Write("News", "Loading news...");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string body;
                try
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (Dictionary<string, object> n in Updates.FetchNews())
                        sb.Append(Updates.Str(n, "date")).Append("  -  ").Append(Updates.Str(n, "title")).Append("\n")
                          .Append(Updates.Str(n, "text")).Append("\n\n");
                    body = sb.Length > 0 ? sb.ToString() : "No news yet.";
                }
                catch (Exception ex) { body = "Could not load news (" + ex.Message + ").\nYou can still play."; }
                BeginInvoke(new Action(() => { if (bNews.Selected) Write("News", body); }));
            });
        }

        // ---------------- updates
        void ShowUpdates()
        {
            SelectTab(bUpdates);
            if (Updates.Repo == "")
            {
                Write("Updates", "No update source is set up yet.\n\nAdd your GitHub repository to launcher_cotc.ini ([Updates] Repo=owner/name).\n" +
                    "Updates only install files that are inside a patch, and every file they replace is backed up to\nDocs_AND_Report\\UpdateBackups first. Your own changes are never deleted.");
                return;
            }
            CheckUpdates(true);
        }

        void CheckUpdates(bool show)
        {
            if (show) Write("Updates", "Checking for updates...");
            Status("Checking for updates...");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string body; List<Release> newer = new List<Release>();
                try
                {
                    string local = GameVersion.Local();
                    List<Release> all = Updates.FetchReleases();
                    StringBuilder sb = new StringBuilder();
                    foreach (Release r in all) if (GameVersion.Compare(r.Tag, local) > 0 && r.ZipUrl != null) newer.Add(r);
                    sb.Append("Installed version: ").Append(local).Append("\n");
                    sb.Append(newer.Count == 0 ? "You have the latest version.\n\n" : newer.Count + " update(s) available.\n\n");
                    for (int i = all.Count - 1; i >= 0; i--)
                    {
                        Release r = all[i];
                        sb.Append(r.Tag).Append("   ").Append(r.Date).Append(GameVersion.Compare(r.Tag, local) > 0 ? "   (new)" : "").Append("\n");
                        if (r.Name != "" && r.Name != r.Tag) sb.Append(r.Name).Append("\n");
                        sb.Append(r.Body.Replace("\r", "")).Append("\n\n");
                    }
                    body = sb.ToString();
                }
                catch (Exception ex) { body = "Could not check for updates (" + ex.Message + ").\nYou can still play."; }
                BeginInvoke(new Action(() =>
                {
                    pending = newer;
                    Status(newer.Count > 0 ? newer.Count + " update(s) available - open Updates to install." : "Up to date.");
                    if (bUpdates.Selected) { Write("Updates", body); bInstall.Visible = pending.Count > 0; }
                }));
            });
        }

        void InstallPending()
        {
            if (pending.Count == 0) return;
            if (MessageBox.Show(this, "Install " + pending.Count + " update(s)?\nEvery file that gets replaced is backed up first.", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            bInstall.Enabled = false; bPlay.Enabled = false; bar.Visible = true;
            List<Release> todo = new List<Release>(pending);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                StringBuilder log = new StringBuilder();
                try
                {
                    foreach (Release r in todo)
                    {
                        string tmp = Path.Combine(Path.GetTempPath(), "cotc_patch_" + r.Tag + ".zip");
                        Status("Downloading " + r.Tag + "...");
                        Net.Download(r.ZipUrl, tmp, (got, total) =>
                        {
                            if (total > 0) BeginInvoke(new Action(() => { bar.Value = (int)Math.Min(100, got * 100 / total); }));
                        });
                        Status("Installing " + r.Tag + "...");
                        int n = Updates.Install(tmp, r.Tag, s => log.Append(s).Append("\n"));
                        GameVersion.Save(r.Tag.TrimStart('v', 'V'));
                        log.Append(r.Tag).Append(": ").Append(n).Append(" files installed\n");
                        try { File.Delete(tmp); } catch { }
                    }
                    log.Append("\nDone. Replaced files were backed up to Docs_AND_Report\\UpdateBackups.");
                }
                catch (Exception ex) { log.Append("\nUpdate stopped: ").Append(ex.Message).Append("\nNothing after this point was changed."); }
                BeginInvoke(new Action(() =>
                {
                    bar.Visible = false; bPlay.Enabled = true; bInstall.Enabled = true;
                    RefreshVersion(); Status("Update finished.");
                    Write("Updates", log.ToString());
                    pending.Clear(); bInstall.Visible = false;
                }));
            });
        }

        // ---------------- settings
        void BuildSettings()
        {
            settingsPanel = new Panel();
            settingsPanel.BackColor = Color.FromArgb(20, 17, 14);
            settingsPanel.Bounds = new Rectangle(18, 16, 804, 350);
            settingsPanel.Visible = false;
            content.Controls.Add(settingsPanel);

            Label h = new Label(); h.Text = "Display settings (engine.ini)"; h.Font = Theme.Title; h.ForeColor = Theme.Gold;
            h.Bounds = new Rectangle(10, 8, 600, 32); settingsPanel.Controls.Add(h);

            Label lr = new Label(); lr.Text = "Resolution"; lr.ForeColor = Theme.Text; lr.Font = Theme.Body; lr.Bounds = new Rectangle(12, 60, 140, 24);
            settingsPanel.Controls.Add(lr);
            cRes = new ComboBox(); cRes.DropDownStyle = ComboBoxStyle.DropDownList; cRes.Font = Theme.Body; cRes.Bounds = new Rectangle(160, 58, 200, 26);
            foreach (string r in new string[] { "1280x720", "1366x768", "1600x900", "1920x1080", "2560x1080", "2560x1440", "3440x1440", "3840x2160" }) cRes.Items.Add(r);
            Rectangle scr = Screen.PrimaryScreen.Bounds;
            string native = scr.Width + "x" + scr.Height;
            if (!cRes.Items.Contains(native)) cRes.Items.Add(native);
            settingsPanel.Controls.Add(cRes);

            cFull = Check("Full screen", 12, 100);
            cVsync = Check("Vertical sync", 12, 132);
            cFps = Check("Show frames per second", 12, 164);

            GoldButton save = new GoldButton("Save settings");
            save.Bounds = new Rectangle(12, 220, 200, 42);
            save.Click += (s, e) => SaveSettings();
            settingsPanel.Controls.Add(save);

            Label note = new Label();
            note.Text = "Everything else (sound, controls, graphics details) is set inside the game's own Options menu.\nA copy of the old file is kept as engine.ini.bak.";
            note.ForeColor = Theme.Text; note.Font = Theme.Small; note.Bounds = new Rectangle(12, 275, 760, 60);
            settingsPanel.Controls.Add(note);
        }

        CheckBox Check(string t, int x, int y)
        {
            CheckBox c = new CheckBox(); c.Text = t; c.ForeColor = Theme.Text; c.Font = Theme.Body; c.Bounds = new Rectangle(x, y, 400, 26);
            settingsPanel.Controls.Add(c); return c;
        }

        void ShowSettings()
        {
            SelectTab(bSettings);
            string res = EngineIni.Get("screen_x", "1920") + "x" + EngineIni.Get("screen_y", "1080");
            if (!cRes.Items.Contains(res)) cRes.Items.Add(res);
            cRes.SelectedItem = res;
            cFull.Checked = EngineIni.Get("full_screen", "0") == "1";
            cVsync.Checked = EngineIni.Get("vsync", "1") == "1";
            cFps.Checked = EngineIni.Get("show_fps", "0") == "1";
        }

        void SaveSettings()
        {
            try
            {
                string[] wh = ((string)cRes.SelectedItem).Split('x');
                Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                v["screen_x"] = wh[0]; v["screen_y"] = wh[1];
                v["full_screen"] = cFull.Checked ? "1" : "0";
                v["vsync"] = cVsync.Checked ? "1" : "0";
                v["show_fps"] = cFps.Checked ? "1" : "0";
                EngineIni.Set(v);
                Status("Settings saved.");
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not save settings:\n" + ex.Message, Text); }
        }

        // ---------------- credits
        void ShowCredits()
        {
            SelectTab(bCredits);
            string credits = "";
            try { credits = File.ReadAllText(Paths.Asset("credits.txt")); } catch { credits = "Corsairs of the Caribbean by DIRERAVEN."; }
            Write("Credits", credits);
        }
    }
}
