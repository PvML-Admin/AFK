using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace KavrosAfkWarden
{
    internal static class Program
    {
        private static System.Threading.Mutex singleInstance;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-test")
                return WardenTests.Run(args.Length > 1 ? args[1] : "test-results.txt", FindRoot());
            NetSecurity.EnableModernTls();
            try
            {
                AppUpdates.Start();
                try { NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
                catch (EntryPointNotFoundException) { NativeMethods.SetProcessDPIAware(); }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool first;
                singleInstance = new System.Threading.Mutex(true, AppInfo.MutexName, out first);
                if (!first)
                {
                    MessageBox.Show("AFK Warden is already running.", AppInfo.Name);
                    return 0;
                }
                Application.Run(new WardenForm());
                GC.KeepAlive(singleInstance);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static string FindRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "companion.module.json"))) return dir.FullName;
                dir = dir.Parent;
            }
            return "";
        }
    }

    internal sealed class StatusRow : PaintedLabel
    {
        internal string Pill = "";
        internal string Value = "";
        internal PillTone Tone = PillTone.Example;

        internal StatusRow()
        {
            Renderer = Draw;
            Height = 28;
            BackColor = KavrosTheme.Bg;
        }

        private void Draw(Graphics g, PaintedLabel label)
        {
            using (Brush brush = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(brush, ClientRectangle);
            int width = KavrosTheme.DrawPill(g, 0, Height / 2, Pill, Tone, KavrosTheme.Bg);
            TextRenderer.DrawText(g, Value, KavrosTheme.Ui(9F, false), new Rectangle(width + 10, 0, Math.Max(1, Width - width - 12), Height), KavrosTheme.Text, KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class WatchPanel : Form
    {
        private readonly PaintedLabel title = new PaintedLabel();
        private readonly Label note = new Label();
        internal readonly StatusRow Idle = new StatusRow();
        internal readonly StatusRow Hp = new StatusRow();
        internal readonly StatusRow Prayer = new StatusRow();
        internal readonly StatusRow Afk = new StatusRow();
        private readonly OpaqueButton pause = new OpaqueButton();
        private readonly OpaqueButton exit = new OpaqueButton();
        private readonly OpaqueButton settings = new OpaqueButton();
        private readonly OpaqueButton collapse = new OpaqueButton();
        private bool collapsed;
        private Point dragStart, windowStart;
        private bool dragging;
        internal event Action SettingsRequested;
        internal event Action ExitRequested;
        internal event Action PauseRequested;
        internal event Action Moved;

        internal WatchPanel()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            ClientSize = new Size(400, 230);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            title.SetBounds(0, 0, 292, 32);
            title.Renderer = DrawTitle;
            title.Text = "AFK WARDEN";
            title.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                dragging = true;
                dragStart = Cursor.Position;
                windowStart = Location;
            };
            title.MouseMove += delegate
            {
                if (!dragging) return;
                Location = new Point(windowStart.X + Cursor.Position.X - dragStart.X, windowStart.Y + Cursor.Position.Y - dragStart.Y);
            };
            title.MouseUp += delegate
            {
                if (!dragging) return;
                dragging = false;
                if (Moved != null) Moved();
            };
            Controls.Add(title);
            settings.Text = "Settings";
            settings.Tone = ButtonTone.Ghost;
            settings.Surface = KavrosTheme.BgRaise;
            settings.SetBounds(298, 4, 65, 24);
            settings.Click += delegate { if (SettingsRequested != null) SettingsRequested(); };
            Controls.Add(settings);
            collapse.Text = "-";
            collapse.Tone = ButtonTone.Ghost;
            collapse.Surface = KavrosTheme.BgRaise;
            collapse.SetBounds(369, 4, 25, 24);
            collapse.Click += delegate { SetCollapsed(!collapsed); };
            Controls.Add(collapse);
            note.SetBounds(12, 40, 376, 22);
            note.ForeColor = KavrosTheme.TextSoft;
            note.Font = KavrosTheme.Ui(8.5F, false);
            note.BackColor = KavrosTheme.Bg;
            Controls.Add(note);
            Place(Idle, 66);
            Place(Hp, 98);
            Place(Prayer, 130);
            Place(Afk, 162);
            pause.Text = "Pause";
            pause.Tone = ButtonTone.Primary;
            pause.SetBounds(12, 196, 184, 24);
            pause.Click += delegate { if (PauseRequested != null) PauseRequested(); };
            Controls.Add(pause);
            exit.Text = "Exit";
            exit.Tone = ButtonTone.Secondary;
            exit.SetBounds(204, 196, 184, 24);
            exit.Click += delegate { if (ExitRequested != null) ExitRequested(); };
            Controls.Add(exit);
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
        }

        internal void SetPaused(bool paused) { pause.Text = paused ? "Resume" : "Pause"; }
        internal void SetNote(string text) { note.Text = text; }

        private void Place(StatusRow row, int y)
        {
            row.SetBounds(12, y, 376, 28);
            Controls.Add(row);
        }

        private void SetCollapsed(bool value)
        {
            collapsed = value;
            collapse.Text = collapsed ? "+" : "-";
            note.Visible = !collapsed;
            Idle.Visible = Hp.Visible = Prayer.Visible = Afk.Visible = !collapsed;
            pause.Visible = exit.Visible = !collapsed;
            ClientSize = new Size(400, collapsed ? 32 : 230);
        }

        private void DrawTitle(Graphics g, PaintedLabel label)
        {
            using (Brush brush = new SolidBrush(KavrosTheme.BgRaise)) g.FillRectangle(brush, label.ClientRectangle);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) g.FillRectangle(gold, 0, 0, label.Width, 2);
            KavrosTheme.DrawTracked(g, "AFK WARDEN", KavrosTheme.Display(8F), 12, 8, KavrosTheme.Text, KavrosTheme.BgRaise, 1.4F, label.Width - 8);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen rule = new Pen(KavrosTheme.LineSoft)) e.Graphics.DrawLine(rule, 0, title.Height - 1, Width, title.Height - 1);
            using (Pen frame = new Pen(KavrosTheme.Frame)) e.Graphics.DrawRectangle(frame, 0, 0, Width - 1, Height - 1);
        }
    }

    internal sealed class WardenForm : Form
    {
        private readonly ComboBox windows = new ComboBox();
        private readonly PaintedLabel connection = new PaintedLabel();
        private KavrosCheckBox pause = new KavrosCheckBox();
        private KavrosCheckBox topMost = new KavrosCheckBox();
        private KavrosCheckBox idleOn = new KavrosCheckBox();
        private KavrosCheckBox hpOn = new KavrosCheckBox();
        private KavrosCheckBox prayerOn = new KavrosCheckBox();
        private KavrosCheckBox afkOn = new KavrosCheckBox();
        private KavrosCheckBox hpPercent = new KavrosCheckBox();
        private KavrosCheckBox prayerPercent = new KavrosCheckBox();
        private KavrosCheckBox soundMaster = new KavrosCheckBox();
        private KavrosCheckBox notifyMaster = new KavrosCheckBox();
        private KavrosCheckBox idleSound = new KavrosCheckBox();
        private KavrosCheckBox idleNotify = new KavrosCheckBox();
        private KavrosCheckBox hpSound = new KavrosCheckBox();
        private KavrosCheckBox hpNotify = new KavrosCheckBox();
        private KavrosCheckBox prayerSound = new KavrosCheckBox();
        private KavrosCheckBox prayerNotify = new KavrosCheckBox();
        private KavrosCheckBox afkSound = new KavrosCheckBox();
        private KavrosCheckBox afkNotify = new KavrosCheckBox();
        private KavrosCheckBox updates = new KavrosCheckBox();
        private readonly NumericUpDown idleSeconds = new NumericUpDown();
        private readonly NumericUpDown hpThreshold = new NumericUpDown();
        private readonly NumericUpDown prayerThreshold = new NumericUpDown();
        private readonly NumericUpDown afkSeconds = new NumericUpDown();
        private readonly NumericUpDown afkLead = new NumericUpDown();
        private readonly NumericUpDown cooldown = new NumericUpDown();
        private readonly NumericUpDown banner = new NumericUpDown();
        private readonly Label status = new Label();
        private readonly WatchPanel panel = new WatchPanel();
        private readonly AlertPopup popup = new AlertPopup();
        private readonly AlertSound sound = new AlertSound();
        private readonly SystemToast toast = new SystemToast();
        private readonly FrameGrab grab = new FrameGrab();
        private readonly MotionDetector motion = new MotionDetector();
        private readonly StableValue hpStable = new StableValue();
        private readonly StableValue hpMaxStable = new StableValue();
        private readonly StableValue prayerStable = new StableValue();
        private readonly StableValue prayerMaxStable = new StableValue();
        private readonly WardenEngine engine = new WardenEngine();
        private readonly Timer timer = new Timer();
        private readonly NotifyIcon tray;
        private readonly string settingsPath;
        private WardenSettings settings = new WardenSettings();
        private List<GameWindow> found = new List<GameWindow>();
        private IntPtr selected = IntPtr.Zero;
        private bool loading = true;
        private bool closing;
        private HudReading lastHud = new HudReading();

        internal WardenForm()
        {
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden", "settings.txt");
            settings = SettingsText.Load(settingsPath);
            Text = "AFK Warden";
            Font = KavrosTheme.Ui(9F, false);
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(516, 860);
            Icon = LoadIcon();
            BuildControls();
            ApplySettingsToControls();
            loading = false;
            ArrangeSettings();
            panel.SettingsRequested += delegate { ShowSettings(); };
            panel.ExitRequested += Exit;
            panel.PauseRequested += delegate { pause.Checked = !pause.Checked; };
            panel.Moved += delegate { settings.PanelX = panel.Left; settings.PanelY = panel.Top; Save(); };
            panel.SetPaused(settings.Paused);
            if (settings.PanelX != int.MinValue) panel.Location = new Point(settings.PanelX, settings.PanelY);
            else
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                panel.Location = new Point(work.Right - panel.Width - 24, work.Top + 72);
            }
            panel.TopMost = settings.PanelTopMost;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open panel", null, delegate { ShowPanel(); });
            menu.Items.Add("Settings", null, delegate { ShowSettings(); });
            menu.Items.Add("Exit", null, delegate { Exit(); });
            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = "AFK Warden " + AppInfo.DisplayVersion;
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowPanel(); };
            HandleCreated += delegate { KavrosTheme.ApplyWindowChrome(Handle); NativeMethods.ExcludeFromCapture(Handle); };
            panel.HandleCreated += delegate { NativeMethods.ExcludeFromCapture(panel.Handle); };
            popup.HandleCreated += delegate { NativeMethods.ExcludeFromCapture(popup.Handle); };
            Shown += delegate { Hide(); ShowPanel(); if (settings.CheckForUpdates) AppUpdates.Check(this, false, SetStatus); CompanionStatus.Write("running", settings.Paused); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!closing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); ShowPanel(); }
            };
            FormClosed += delegate { closing = true; timer.Stop(); Save(); CompanionStatus.Write("stopped", settings.Paused); grab.Dispose(); tray.Visible = false; tray.Dispose(); panel.Dispose(); popup.Dispose(); };
            timer.Interval = 250;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            RefreshWindows();
        }

        private void BuildControls()
        {
            PaintedLabel subtitle = new PaintedLabel();
            subtitle.SetBounds(92, 36, 300, 28);
            subtitle.Renderer = delegate(Graphics g, PaintedLabel label)
            {
                using (Brush brush = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(brush, label.ClientRectangle);
                KavrosTheme.DrawTracked(g, "AFK WARDEN", KavrosTheme.Display(13F), 0, 2, KavrosTheme.Text, KavrosTheme.Bg, 1.6F, label.Width);
            };
            Controls.Add(subtitle);

            windows.SetBounds(28, 128, 340, 28);
            windows.DropDownStyle = ComboBoxStyle.DropDownList;
            KavrosTheme.StyleCombo(windows);
            windows.SelectedIndexChanged += delegate { if (!loading) AttachSelected(); };
            Controls.Add(windows);
            AddButton("Refresh", 376, 128, 108, ButtonTone.Secondary, delegate { RefreshWindows(); });
            connection.SetBounds(28, 164, 456, 28);
            connection.Renderer = DrawConnection;
            connection.Text = "Waiting for Kavros";
            Controls.Add(connection);
            pause = Check("Pause watching", 28, 200, 160, settings.Paused);
            topMost = Check("Keep panel on top", 210, 200, 180, settings.PanelTopMost);

            idleOn = Check("Idle alert", 28, 292, 120, settings.IdleEnabled);
            Caption("After", 28, 318);
            Number(idleSeconds, 78, 314, 3, 600, settings.IdleSeconds);
            Caption("seconds", 150, 318);
            idleSound = Check("Sound", 250, 316, 90, settings.IdleSound);
            idleNotify = Check("Notify", 350, 316, 100, settings.IdleNotify);

            hpOn = Check("Low hitpoints", 28, 356, 140, settings.HpEnabled);
            Caption("Below", 28, 382);
            Number(hpThreshold, 78, 378, 1, settings.HpPercent ? 99 : 99999, settings.HpThreshold);
            hpPercent = Check("Percent", 160, 380, 90, settings.HpPercent);
            hpSound = Check("Sound", 250, 380, 90, settings.HpSound);
            hpNotify = Check("Notify", 350, 380, 100, settings.HpNotify);

            prayerOn = Check("Low prayer", 28, 420, 140, settings.PrayerEnabled);
            Caption("Below", 28, 446);
            Number(prayerThreshold, 78, 442, 1, settings.PrayerPercent ? 99 : 99999, settings.PrayerThreshold);
            prayerPercent = Check("Percent", 160, 444, 90, settings.PrayerPercent);
            prayerSound = Check("Sound", 250, 444, 90, settings.PrayerSound);
            prayerNotify = Check("Notify", 350, 444, 100, settings.PrayerNotify);

            afkOn = Check("AFK teleport warning", 28, 484, 200, settings.AfkEnabled);
            Caption("Timer", 28, 510);
            Number(afkSeconds, 78, 506, 60, 1800, settings.AfkSeconds);
            Caption("lead", 160, 510);
            Number(afkLead, 200, 506, 5, Math.Max(5, settings.AfkSeconds - 5), settings.AfkLeadSeconds);
            Caption("sec", 272, 510);
            afkSound = Check("Sound", 320, 508, 80, settings.AfkSound);
            afkNotify = Check("Notify", 400, 508, 90, settings.AfkNotify);

            soundMaster = Check("Play sounds", 28, 596, 140, settings.SoundMaster);
            notifyMaster = Check("Show notifications", 190, 596, 180, settings.NotifyMaster);
            Caption("Cooldown", 28, 630);
            Number(cooldown, 100, 626, 0, 600, settings.CooldownSeconds);
            Caption("sec", 172, 630);
            Caption("Stays", 230, 630);
            Number(banner, 276, 626, 2, 120, settings.BannerSeconds);
            Caption("sec", 348, 630);
            updates = Check("Check for updates at startup", 28, 664, 250, settings.CheckForUpdates);
            AddButton("Pin hitpoints", 28, 700, 140, ButtonTone.Secondary, delegate { Pin(true); });
            AddButton("Pin prayer", 176, 700, 140, ButtonTone.Secondary, delegate { Pin(false); });
            AddButton("Clear pins", 324, 700, 140, ButtonTone.Secondary, delegate { settings.HpPinW = 0; settings.PrayerPinW = 0; Save(); SetStatus("Pins cleared. Automatic search is on."); });

            AddButton("Test notification", 28, 760, 150, ButtonTone.Primary, delegate { popup.Present(SampleAlert(), (int)banner.Value); });
            AddButton("Test sound", 186, 760, 110, ButtonTone.Secondary, delegate { sound.Play("hp"); });
            AddButton("Check for updates", 304, 760, 160, ButtonTone.Secondary, delegate { AppUpdates.Check(this, true, SetStatus); });
            status.SetBounds(28, 800, 456, 36);
            status.ForeColor = KavrosTheme.TextSoft;
            status.BackColor = KavrosTheme.Bg;
            status.Font = KavrosTheme.Ui(8.5F, false);
            status.Text = "Live alerts obey the switches above. Test buttons always play.";
            Controls.Add(status);

            hpPercent.CheckedChanged += delegate { if (!loading) hpThreshold.Maximum = hpPercent.Checked ? 99 : 99999; };
            prayerPercent.CheckedChanged += delegate { if (!loading) prayerThreshold.Maximum = prayerPercent.Checked ? 99 : 99999; };
            Wire(pause, idleOn, hpOn, prayerOn, afkOn, hpPercent, prayerPercent, soundMaster, notifyMaster, idleSound, idleNotify, hpSound, hpNotify, prayerSound, prayerNotify, afkSound, afkNotify, updates, topMost);
            idleSeconds.ValueChanged += delegate { if (!loading) Save(); };
            hpThreshold.ValueChanged += delegate { if (!loading) Save(); };
            prayerThreshold.ValueChanged += delegate { if (!loading) Save(); };
            afkSeconds.ValueChanged += delegate { if (!loading) { afkLead.Maximum = Math.Max(5, (int)afkSeconds.Value - 5); Save(); } };
            afkLead.ValueChanged += delegate { if (!loading) Save(); };
            cooldown.ValueChanged += delegate { if (!loading) Save(); };
            banner.ValueChanged += delegate { if (!loading) Save(); };
        }

        private void ApplySettingsToControls()
        {
            pause.Checked = settings.Paused;
            topMost.Checked = settings.PanelTopMost;
            idleOn.Checked = settings.IdleEnabled;
            hpOn.Checked = settings.HpEnabled;
            prayerOn.Checked = settings.PrayerEnabled;
            afkOn.Checked = settings.AfkEnabled;
            hpPercent.Checked = settings.HpPercent;
            prayerPercent.Checked = settings.PrayerPercent;
            soundMaster.Checked = settings.SoundMaster;
            notifyMaster.Checked = settings.NotifyMaster;
            idleSound.Checked = settings.IdleSound;
            idleNotify.Checked = settings.IdleNotify;
            hpSound.Checked = settings.HpSound;
            hpNotify.Checked = settings.HpNotify;
            prayerSound.Checked = settings.PrayerSound;
            prayerNotify.Checked = settings.PrayerNotify;
            afkSound.Checked = settings.AfkSound;
            afkNotify.Checked = settings.AfkNotify;
            updates.Checked = settings.CheckForUpdates;
            idleSeconds.Value = Clamp(settings.IdleSeconds, idleSeconds);
            hpThreshold.Maximum = settings.HpPercent ? 99 : 99999;
            hpThreshold.Value = Clamp(settings.HpThreshold, hpThreshold);
            prayerThreshold.Maximum = settings.PrayerPercent ? 99 : 99999;
            prayerThreshold.Value = Clamp(settings.PrayerThreshold, prayerThreshold);
            afkSeconds.Value = Clamp(settings.AfkSeconds, afkSeconds);
            afkLead.Maximum = Math.Max(5, (int)afkSeconds.Value - 5);
            afkLead.Value = Clamp(settings.AfkLeadSeconds, afkLead);
            cooldown.Value = Clamp(settings.CooldownSeconds, cooldown);
            banner.Value = Clamp(settings.BannerSeconds, banner);
        }

        private static decimal Clamp(int value, NumericUpDown number)
        {
            if (value < number.Minimum) return number.Minimum;
            if (value > number.Maximum) return number.Maximum;
            return value;
        }

        private void Wire(params CheckBox[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                CheckBox box = boxes[i];
                box.CheckedChanged += delegate { if (!loading) Save(); };
            }
        }

        private KavrosCheckBox Check(string text, int x, int y, int width, bool value)
        {
            KavrosCheckBox box = new KavrosCheckBox();
            box.Text = text;
            box.SetBounds(x, y, width, 22);
            box.Checked = value;
            box.Font = KavrosTheme.Ui(9F, false);
            box.BackColor = KavrosTheme.Panel;
            Controls.Add(box);
            return box;
        }

        private void Caption(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(x, y, 70, 20);
            label.ForeColor = KavrosTheme.TextSoft;
            label.BackColor = KavrosTheme.Panel;
            label.Font = KavrosTheme.Ui(8.5F, false);
            Controls.Add(label);
        }

        private void Number(NumericUpDown number, int x, int y, int min, int max, int value)
        {
            number.SetBounds(x, y, 64, 24);
            number.Minimum = min;
            number.Maximum = max;
            number.Value = Clamp(value, number);
            KavrosTheme.StyleNumber(number);
            Controls.Add(number);
        }

        private void AddButton(string text, int x, int y, int width, ButtonTone tone, Action click)
        {
            OpaqueButton button = new OpaqueButton();
            button.Text = text;
            button.Tone = tone;
            button.SetBounds(x, y, width, 28);
            button.Font = KavrosTheme.Ui(9F, tone == ButtonTone.Primary);
            button.Click += delegate { click(); };
            Controls.Add(button);
        }

        private void DrawConnection(Graphics g, PaintedLabel label)
        {
            using (Brush brush = new SolidBrush(KavrosTheme.Panel)) g.FillRectangle(brush, label.ClientRectangle);
            string text = label.Text ?? "";
            PillTone tone = text.StartsWith("Game found", StringComparison.Ordinal) ? PillTone.Ok : text.StartsWith("Waiting", StringComparison.Ordinal) ? PillTone.Warn : PillTone.Bad;
            string pill = tone == PillTone.Ok ? "Game found" : tone == PillTone.Warn ? "Waiting" : "Not found";
            int width = KavrosTheme.DrawPill(g, 0, label.Height / 2, pill, tone, KavrosTheme.Panel);
            string rest = text.StartsWith("Game found: ", StringComparison.Ordinal) ? text.Substring(12) : text;
            TextRenderer.DrawText(g, rest, KavrosTheme.Ui(8.5F, false), new Rectangle(width + 8, 0, Math.Max(1, label.Width - width - 8), label.Height), KavrosTheme.TextSoft, KavrosTheme.Panel, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private void ArrangeSettings()
        {
            PictureBox crest = null;
            Stream stream = typeof(KavrosTheme).Assembly.GetManifestResourceStream("Kavros.Branding.kavros-crest.png");
            if (stream != null)
            {
                crest = new PictureBox();
                crest.Name = "KavrosCrest";
                crest.AccessibleName = "Kavros";
                crest.TabStop = false;
                crest.Bounds = new Rectangle(16, 9, 65, 65);
                crest.SizeMode = PictureBoxSizeMode.Zoom;
                Bitmap bitmap;
                using (stream)
                using (Image image = Image.FromStream(stream)) bitmap = new Bitmap(image);
                crest.Image = bitmap;
                crest.Disposed += delegate { bitmap.Dispose(); };
            }
            Label footer = new Label();
            footer.Name = "BuildCredit";
            footer.Text = "Kavros  ·  " + AppInfo.DisplayVersion;
            footer.Dock = DockStyle.Bottom;
            footer.Height = 28;
            footer.TextAlign = ContentAlignment.MiddleCenter;
            footer.ForeColor = KavrosTheme.TextFaint;
            footer.BackColor = KavrosTheme.Bg;
            footer.Font = KavrosTheme.Ui(8.5F, false);
            Size original = ClientSize;
            MainSurface body = new MainSurface();
            body.Name = "SettingsBody";
            body.Dock = DockStyle.Fill;
            body.AutoScroll = true;
            body.AutoScrollMinSize = new Size(0, original.Height);
            body.Cards.Add(new MainSurface.Card { Label = "WATCH", Bounds = new Rectangle(16, 96, 484, 136) });
            body.Cards.Add(new MainSurface.Card { Label = "ALERTS", Bounds = new Rectangle(16, 248, 484, 300) });
            body.Cards.Add(new MainSurface.Card { Label = "CHANNELS", Bounds = new Rectangle(16, 564, 484, 280) });
            Control[] controls = new Control[Controls.Count];
            Controls.CopyTo(controls, 0);
            Controls.Clear();
            body.Controls.AddRange(controls);
            if (crest != null) body.Controls.Add(crest);
            foreach (Control control in body.Controls)
            {
                if (control is CheckBox || control is Label || control is PaintedLabel)
                {
                    if (control.Top >= 96) control.BackColor = KavrosTheme.Panel;
                }
            }
            Controls.Add(body);
            Controls.Add(footer);
            ClientSize = new Size(original.Width, original.Height + footer.Height);
        }

        private void Save()
        {
            if (loading) return;
            settings.Paused = pause.Checked;
            settings.PanelTopMost = topMost.Checked;
            settings.IdleEnabled = idleOn.Checked;
            settings.IdleSeconds = (int)idleSeconds.Value;
            settings.HpEnabled = hpOn.Checked;
            settings.HpThreshold = (int)hpThreshold.Value;
            settings.HpPercent = hpPercent.Checked;
            settings.PrayerEnabled = prayerOn.Checked;
            settings.PrayerThreshold = (int)prayerThreshold.Value;
            settings.PrayerPercent = prayerPercent.Checked;
            settings.AfkEnabled = afkOn.Checked;
            settings.AfkSeconds = (int)afkSeconds.Value;
            settings.AfkLeadSeconds = (int)afkLead.Value;
            settings.SoundMaster = soundMaster.Checked;
            settings.NotifyMaster = notifyMaster.Checked;
            settings.IdleSound = idleSound.Checked;
            settings.IdleNotify = idleNotify.Checked;
            settings.HpSound = hpSound.Checked;
            settings.HpNotify = hpNotify.Checked;
            settings.PrayerSound = prayerSound.Checked;
            settings.PrayerNotify = prayerNotify.Checked;
            settings.AfkSound = afkSound.Checked;
            settings.AfkNotify = afkNotify.Checked;
            settings.CooldownSeconds = (int)cooldown.Value;
            settings.BannerSeconds = (int)banner.Value;
            settings.CheckForUpdates = updates.Checked;
            settings.Clamp();
            try { SettingsText.Save(settingsPath, settings); } catch (IOException) { }
            panel.TopMost = settings.PanelTopMost;
            panel.SetPaused(settings.Paused);
        }

        private void SetStatus(string text) { if (!status.IsDisposed) status.Text = text; }

        private void ShowSettings()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Activate();
        }

        private void ShowPanel()
        {
            panel.TopMost = settings.PanelTopMost;
            if (!panel.Visible) panel.Show();
        }

        private void Exit()
        {
            closing = true;
            CompanionStatus.Write("stopped", settings.Paused);
            Application.Exit();
        }

        private void RefreshWindows()
        {
            found = WindowLocator.Enumerate(System.Diagnostics.Process.GetCurrentProcess().Id);
            loading = true;
            windows.Items.Clear();
            for (int i = 0; i < found.Count; i++) if (WindowLocator.Score(found[i]) >= 100) windows.Items.Add(found[i]);
            GameWindow choice = WindowLocator.Choose(found, selected);
            if (choice != null)
            {
                selected = choice.Handle;
                windows.SelectedItem = choice;
            }
            loading = false;
            if (choice == null) connection.Text = "Waiting for Kavros";
            else connection.Text = "Game found: " + choice.Title;
            connection.Invalidate();
        }

        private void AttachSelected()
        {
            GameWindow window = windows.SelectedItem as GameWindow;
            selected = window == null ? IntPtr.Zero : window.Handle;
            connection.Text = window == null ? "Waiting for Kavros" : "Game found: " + window.Title;
            connection.Invalidate();
        }

        private void Tick()
        {
            if (selected == IntPtr.Zero || !NativeMethods.IsWindow(selected)) RefreshWindows();
            grab.Tick(selected);
            bool pixels = grab.Image != null;
            bool moving = pixels && motion.Moved(grab.Image);
            if (pixels) lastHud = HudReader.Read(grab.Image, settings);
            int? hp = pixels ? hpStable.Push(lastHud.Hp) : hpStable.Push(null);
            int? hpMax = pixels ? hpMaxStable.Push(lastHud.HpMax) : hpMaxStable.Push(null);
            int? prayer = pixels ? prayerStable.Push(lastHud.Prayer) : prayerStable.Push(null);
            int? prayerMax = pixels ? prayerMaxStable.Push(lastHud.PrayerMax) : prayerMaxStable.Push(null);
            bool hpComparable = hp != null && WardenSettings.Comparable(settings.HpPercent, lastHud.HpFromOrb, hpMax != null);
            bool prayerComparable = prayer != null && WardenSettings.Comparable(settings.PrayerPercent, lastHud.PrayerFromOrb, prayerMax != null);
            WatchSample sample = new WatchSample();
            sample.Time = DateTime.UtcNow;
            sample.GameAttached = selected != IntPtr.Zero && NativeMethods.IsWindow(selected);
            sample.PixelsFresh = pixels;
            sample.CharacterMoving = moving;
            sample.InputActivity = grab.Input;
            sample.Hp = hpComparable ? hp : (int?)null;
            sample.HpMax = hpMax;
            sample.Prayer = prayerComparable ? prayer : (int?)null;
            sample.PrayerMax = prayerMax;
            List<AlertEvent> events = engine.Tick(sample, settings);
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Notify)
                {
                    popup.Present(events[i], settings.BannerSeconds);
                    toast.Show(events[i].Title, events[i].Detail);
                }
                if (events[i].Sound) sound.Play(events[i].Kind);
            }
            UpdatePanel(pixels);
            CompanionStatus.Write("running", settings.Paused);
        }

        private void UpdatePanel(bool pixels)
        {
            panel.SetNote(grab.Note);
            panel.Idle.Pill = engine.IdleLatched ? "Idle" : "Moving";
            panel.Idle.Tone = engine.IdleLatched ? PillTone.Bad : PillTone.Ok;
            panel.Idle.Value = pixels ? ((int)engine.StillFor.TotalSeconds).ToString() + "s still" : "No view";
            panel.Hp.Pill = engine.HpLatched ? "Low HP" : "Hitpoints";
            panel.Hp.Tone = engine.HpLatched ? PillTone.Bad : !pixels || lastHud.Hp == null ? PillTone.Example : PillTone.Ok;
            panel.Hp.Value = pixels ? Format(lastHud.Hp, lastHud.HpMax, lastHud.HpFromOrb) : "No view";
            panel.Prayer.Pill = engine.PrayerLatched ? "Low prayer" : "Prayer";
            panel.Prayer.Tone = engine.PrayerLatched ? PillTone.Warn : !pixels || lastHud.Prayer == null ? PillTone.Example : PillTone.Ok;
            panel.Prayer.Value = pixels ? Format(lastHud.Prayer, lastHud.PrayerMax, lastHud.PrayerFromOrb) : "No view";
            int left = Math.Max(0, (int)engine.AfkRemaining.TotalSeconds);
            panel.Afk.Pill = engine.AfkLatched ? "AFK soon" : "AFK timer";
            panel.Afk.Tone = engine.AfkLatched ? PillTone.Bad : PillTone.Ok;
            panel.Afk.Value = (left / 60).ToString() + ":" + (left % 60).ToString("00") + " left";
            panel.Idle.Invalidate();
            panel.Hp.Invalidate();
            panel.Prayer.Invalidate();
            panel.Afk.Invalidate();
        }

        private static string Format(int? current, int? max, bool orb)
        {
            if (current == null) return "Unread";
            if (orb) return current.Value.ToString() + "% orb";
            if (max != null) return current.Value.ToString() + " / " + max.Value.ToString();
            return current.Value.ToString();
        }

        private void Pin(bool hp)
        {
            if (selected == IntPtr.Zero || NativeMethods.IsIconic(selected))
            {
                SetStatus("Restore the Kavros window, then pin the number.");
                return;
            }
            Rectangle client = NativeMethods.ClientBounds(selected);
            using (RegionPicker picker = new RegionPicker(client))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.Selection.Width < 8) return;
                Rectangle picked = picker.Selection;
                if (hp)
                {
                    settings.HpPinX = picked.X / (double)client.Width;
                    settings.HpPinY = picked.Y / (double)client.Height;
                    settings.HpPinW = picked.Width / (double)client.Width;
                    settings.HpPinH = picked.Height / (double)client.Height;
                }
                else
                {
                    settings.PrayerPinX = picked.X / (double)client.Width;
                    settings.PrayerPinY = picked.Y / (double)client.Height;
                    settings.PrayerPinW = picked.Width / (double)client.Width;
                    settings.PrayerPinH = picked.Height / (double)client.Height;
                }
                Save();
                SetStatus(hp ? "Hitpoint area pinned." : "Prayer area pinned.");
            }
        }

        private static AlertEvent SampleAlert()
        {
            AlertEvent alert = new AlertEvent();
            alert.Kind = "hp";
            alert.Title = "Hitpoints are low";
            alert.Detail = "This is a test notification. It stays above other windows.";
            return alert;
        }

        private static Icon LoadIcon()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "branding", "afk-warden.ico");
            if (File.Exists(path)) return new Icon(path);
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(KavrosTheme.Bg);
                using (Pen pen = new Pen(KavrosTheme.Gold, 2)) g.DrawEllipse(pen, 5, 5, 21, 21);
            }
            IntPtr handle = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
    }

    internal sealed class RegionPicker : Form
    {
        private Point origin;
        private Rectangle selection;
        private bool drag;
        internal Rectangle Selection;

        internal RegionPicker(Rectangle client)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = client;
            Opacity = 0.35;
            BackColor = KavrosTheme.BgDeep;
            Cursor = Cursors.Cross;
            TopMost = true;
            KeyPreview = true;
            MouseDown += delegate(object sender, MouseEventArgs e) { drag = true; origin = e.Location; selection = new Rectangle(e.Location, Size.Empty); };
            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!drag) return;
                selection = Rectangle.FromLTRB(Math.Min(origin.X, e.X), Math.Min(origin.Y, e.Y), Math.Max(origin.X, e.X), Math.Max(origin.Y, e.Y));
                Invalidate();
            };
            MouseUp += delegate { drag = false; Selection = selection; DialogResult = DialogResult.OK; Close(); };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) e.Graphics.FillRectangle(gold, 0, 0, Width, 2);
            TextRenderer.DrawText(e.Graphics, "DRAG THE HITPOINT OR PRAYER NUMBER", KavrosTheme.Display(8F), new Rectangle(12, 10, Width - 24, 24), KavrosTheme.Gold, KavrosTheme.BgDeep, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (selection.Width > 0) using (Pen pen = new Pen(KavrosTheme.Gold, 2)) e.Graphics.DrawRectangle(pen, selection);
        }
    }
}
