using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace KavrosAfkWarden
{
    internal sealed class AlertSound
    {
        internal int Volume = 100;

        internal void Play(string kind)
        {
            byte[] wav = Build(kind, Volume);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (MemoryStream stream = new MemoryStream(wav, false))
                    using (SoundPlayer player = new SoundPlayer(stream))
                        player.PlaySync();
                }
                catch (Exception) { }
            });
        }

        private static byte[] Build(string kind, int volume)
        {
            int rate = 22050;
            double[] tones = kind == "hp" ? new double[] { 880, 660 } : kind == "afk" ? new double[] { 988, 740 } : kind == "prayer" ? new double[] { 660, 520 } : kind == "potion" ? new double[] { 740, 520 } : new double[] { 520, 780 };
            int samples = (int)(rate * 0.42);
            byte[] data = new byte[samples * 2];
            int level = volume < 0 ? 0 : volume > 100 ? 100 : volume;
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)rate;
                int which = t < 0.18 ? 0 : 1;
                double local = which == 0 ? t : t - 0.2;
                double envelope = local < 0 || local > 0.18 ? 0 : Math.Sin(Math.PI * Math.Min(1, local / 0.18));
                short sample = (short)(Math.Sin(2 * Math.PI * tones[which] * t) * envelope * 14000 * level / 100);
                data[i * 2] = (byte)(sample & 255);
                data[i * 2 + 1] = (byte)((sample >> 8) & 255);
            }
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + data.Length);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(data.Length);
                writer.Write(data);
                return stream.ToArray();
            }
        }
    }

    internal sealed class SystemToast
    {
        private bool failed;

        internal void Show(string title, string detail)
        {
            if (failed) return;
            try
            {
                XmlDocument xml = new XmlDocument();
                xml.LoadXml("<toast><visual><binding template=\"ToastGeneric\"><text></text><text></text></binding></visual><audio silent=\"true\"/></toast>");
                XmlNodeList texts = xml.GetElementsByTagName("text");
                texts.Item(0).AppendChild(xml.CreateTextNode(title));
                texts.Item(1).AppendChild(xml.CreateTextNode(detail));
                ToastNotificationManager.CreateToastNotifier("Kavros.AfkWarden").Show(new ToastNotification(xml));
            }
            catch (Exception) { failed = true; }
        }
    }

    internal sealed class AlertPopup : System.Windows.Forms.Form
    {
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Queue<AlertEvent> waiting = new Queue<AlertEvent>();
        private string pill = "Alert";
        private string heading = "";
        private string body = "";
        private PillTone tone = PillTone.Warn;
        private int seconds = 8;

        internal AlertPopup()
        {
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            ClientSize = new System.Drawing.Size(400, 104);
            SetStyle(System.Windows.Forms.ControlStyles.UserPaint | System.Windows.Forms.ControlStyles.AllPaintingInWmPaint | System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer | System.Windows.Forms.ControlStyles.ResizeRedraw, true);
            timer.Tick += delegate { timer.Stop(); ShowNext(); };
            Click += delegate { Dismiss(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override System.Windows.Forms.CreateParams CreateParams
        {
            get
            {
                System.Windows.Forms.CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000;
                parameters.ExStyle |= 0x00000080;
                return parameters;
            }
        }

        internal void Present(AlertEvent alert, int bannerSeconds)
        {
            if (alert == null) return;
            seconds = bannerSeconds;
            if (Visible) waiting.Enqueue(alert);
            else ShowAlert(alert);
        }

        private void ShowAlert(AlertEvent alert)
        {
            heading = alert.Title ?? "";
            body = alert.Detail ?? "";
            if (alert.Kind == "hp") { pill = "Low HP"; tone = PillTone.Bad; }
            else if (alert.Kind == "prayer") { pill = "Low prayer"; tone = PillTone.Warn; }
            else if (alert.Kind == "afk") { pill = "AFK teleport"; tone = PillTone.Bad; }
            else if (alert.Kind == "potion") { pill = "Potion"; tone = PillTone.Warn; }
            else { pill = "Idle"; tone = PillTone.Warn; }
            Invalidate();
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            System.Drawing.Rectangle work = screen.WorkingArea;
            IntRect place = AlertPlacement.Place(work.Left, work.Top, work.Width, work.Height, Width, Height, 0);
            Bounds = new System.Drawing.Rectangle(place.X, place.Y, place.Width, place.Height);
            if (!Visible) Show();
            timer.Interval = Math.Max(2, seconds) * 1000;
            timer.Stop();
            timer.Start();
        }

        private void Dismiss()
        {
            timer.Stop();
            ShowNext();
        }

        private void ShowNext()
        {
            if (waiting.Count == 0) { Hide(); return; }
            ShowAlert(waiting.Dequeue());
        }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            System.Drawing.Graphics g = e.Graphics;
            g.Clear(KavrosTheme.Bg);
            using (System.Drawing.Brush gold = new System.Drawing.SolidBrush(KavrosTheme.Gold)) g.FillRectangle(gold, 0, 0, Width, 2);
            using (System.Drawing.Pen frame = new System.Drawing.Pen(KavrosTheme.Frame)) g.DrawRectangle(frame, 0, 0, Width - 1, Height - 1);
            KavrosTheme.DrawPill(g, 14, 24, pill, tone, KavrosTheme.Bg);
            KavrosTheme.DrawTracked(g, heading.ToUpperInvariant(), KavrosTheme.Display(11F), 14, 40, KavrosTheme.Text, KavrosTheme.Bg, 1.1F, Width - 18);
            System.Windows.Forms.TextRenderer.DrawText(g, body, KavrosTheme.Ui(9F, false), new System.Drawing.Rectangle(14, 64, Width - 28, 28), KavrosTheme.TextSoft, KavrosTheme.Bg, System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.EndEllipsis | System.Windows.Forms.TextFormatFlags.NoPadding);
        }
    }
}
