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

    // A plain text box. NumericUpDown's spinner draws with the system colours
    // and clips the navy field, so the threshold boxes were unreadable.
    internal sealed class NumberField : TextBox
    {
        internal int Minimum;
        internal int Maximum = 100;
        internal int Value;
        internal event EventHandler ValueChanged;

        internal NumberField()
        {
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = KavrosTheme.Input;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            TextAlign = HorizontalAlignment.Right;
            Leave += delegate { Commit(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                Commit();
                e.SuppressKeyPress = true;
            };
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
            Text = value.ToString();
            if (notify && value != before && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        private void Commit()
        {
            int parsed;
            if (!int.TryParse((Text ?? "").Trim(), out parsed)) parsed = Value;
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
