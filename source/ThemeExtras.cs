using System;
using System.Drawing;
using System.Windows.Forms;

namespace KavrosAfkWarden
{
    internal static partial class KavrosTheme
    {
        public static void StyleCombo(ComboBox combo)
        {
            combo.BackColor = Input;
            combo.ForeColor = Text;
            combo.FlatStyle = FlatStyle.Flat;
        }

        public static void StyleNumber(NumericUpDown number)
        {
            number.BackColor = Input;
            number.ForeColor = Text;
            number.BorderStyle = BorderStyle.FixedSingle;
        }
    }

    // One navy field. A naked TextBox keeps a system border and a second
    // background, which showed up as a stray block beside the number.
    internal sealed class NumberField : UserControl
    {
        private readonly TextBox box = new TextBox();
        internal int Minimum;
        internal int Maximum = 100;
        internal int Value;
        internal event EventHandler ValueChanged;

        internal NumberField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KavrosTheme.Input;
            ForeColor = KavrosTheme.Text;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = KavrosTheme.Input;
            box.ForeColor = KavrosTheme.Text;
            box.Font = KavrosTheme.Ui(9F, false);
            box.TextAlign = HorizontalAlignment.Center;
            box.Leave += delegate { Commit(); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                Commit();
                e.SuppressKeyPress = true;
            };
            Controls.Add(box);
            Resize += delegate { PlaceBox(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (Brush fill = new SolidBrush(KavrosTheme.Input)) e.Graphics.FillRectangle(fill, ClientRectangle);
            using (Pen pen = new Pen(KavrosTheme.Line)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void PlaceBox()
        {
            int textH = Math.Max(14, box.PreferredHeight);
            int y = Math.Max(3, (Height - textH) / 2);
            box.SetBounds(8, y, Math.Max(8, Width - 16), textH);
        }

        internal void SetRange(int min, int max, int value)
        {
            Minimum = min;
            Maximum = max;
            SetValue(value, false);
        }

        internal void SetValue(int value, bool notify)
        {
            if (value < Minimum) value = Minimum;
            if (value > Maximum) value = Maximum;
            int before = Value;
            Value = value;
            box.Text = value.ToString();
            if (notify && value != before && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        private void Commit()
        {
            int parsed;
            if (!int.TryParse((box.Text ?? "").Trim(), out parsed)) parsed = Value;
            SetValue(parsed, true);
        }
    }

    internal sealed class VolumeSlider : Control
    {
        internal int Value = 100;
        internal event EventHandler ValueChanged;
        private bool drag;

        internal VolumeSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 22;
            BackColor = KavrosTheme.Panel;
            MouseDown += delegate(object sender, MouseEventArgs e) { drag = true; SetFrom(e.X); };
            MouseMove += delegate(object sender, MouseEventArgs e) { if (drag) SetFrom(e.X); };
            MouseUp += delegate { drag = false; };
        }

        internal void SetValue(int value)
        {
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            Value = value;
            Invalidate();
        }

        private void SetFrom(int x)
        {
            int span = Math.Max(1, Width - 12);
            int value = (x - 6) * 100 / span;
            int before = Value;
            SetValue(value);
            if (Value != before && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int y = Height / 2;
            using (Pen line = new Pen(KavrosTheme.Line)) e.Graphics.DrawLine(line, 6, y, Width - 6, y);
            int span = Math.Max(1, Width - 12);
            int x = 6 + Value * span / 100;
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) e.Graphics.FillEllipse(gold, x - 6, y - 6, 12, 12);
        }
    }
}
