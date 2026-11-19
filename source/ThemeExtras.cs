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
}
