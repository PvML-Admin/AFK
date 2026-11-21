using System;

namespace KavrosAfkWarden
{
    // Turns a full client frame into the chat crop Windows OCR should read:
    // the stacked coloured lines, tripled, as dark ink on a light page.
    public static class ChatPrep
    {
        public static bool Coloured(int color)
        {
            int r = ImageBuffer.R(color), g = ImageBuffer.G(color), b = ImageBuffer.B(color);
            // Dark red game text is about (135, 2, 3). A luminance cut drops it.
            if (r > 70 && r > g + 30 && r > b + 30) return true;
            if (r > 170 && g > 80 && g < r - 10 && b < 110) return true;
            if (g > 135 && g > r + 18 && g > b + 12) return true;
            if (r > 200 && g > 200 && b > 180) return true;
            return false;
        }

        public static IntRect FindBox(ImageBuffer image)
        {
            if (image == null || image.Width < 80 || image.Height < 80) return new IntRect();
            int yStart = image.Height * 68 / 100;
            int xLimit = image.Width * 45 / 100;
            int top = image.Height;
            int bottom = 0;
            int left = image.Width;
            int right = 0;
            int lines = 0;
            for (int y = yStart; y < image.Height - 1; y++)
            {
                int runLeft = -1;
                int runRight = -1;
                int count = 0;
                for (int x = 2; x < xLimit; x++)
                {
                    if (!Coloured(image.Argb(x, y))) continue;
                    count++;
                    if (runLeft < 0) runLeft = x;
                    runRight = x;
                }
                if (count < 8 || runLeft < 0 || runLeft > 160) continue;
                int width = runRight - runLeft + 1;
                if (width < 50 || width > 480) continue;
                if (count > width * 9 / 10) continue;
                lines++;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
                if (runLeft < left) left = runLeft;
                if (runRight > right) right = runRight;
            }
            if (lines < 3 || right <= left || bottom <= top) return new IntRect();
            int x0 = Math.Max(0, left - 8);
            int y0 = Math.Max(0, top - 6);
            int x1 = Math.Min(image.Width, right + 12);
            int y1 = Math.Min(image.Height, BottomOfChat(image, left, right, bottom));
            return new IntRect(x0, y0, x1 - x0, y1 - y0);
        }

        // The newest line can sit on the bottom row, just above the input bar.
        // Follow the dark chat background down until that bar, or the action bar.
        private static int BottomOfChat(ImageBuffer image, int left, int right, int bottom)
        {
            int span = Math.Max(40, (right - left) / 2);
            int xBegin = Math.Max(0, left);
            int xEnd = Math.Min(image.Width, xBegin + span);
            int limit = Math.Min(image.Height - 1, bottom + 72);
            int y1 = Math.Min(image.Height, bottom + 8);
            for (int y = bottom + 1; y <= limit; y++)
            {
                int dark = 0;
                int bright = 0;
                int width = Math.Max(1, xEnd - xBegin);
                for (int x = xBegin; x < xEnd; x++)
                {
                    int c = image.Argb(x, y);
                    int sum = ImageBuffer.R(c) + ImageBuffer.G(c) + ImageBuffer.B(c);
                    if (sum < 140) dark++;
                    if (sum > 480) bright++;
                }
                if (bright > width / 6) break;
                if (dark * 2 < width) break;
                y1 = y + 1;
            }
            return y1;
        }

        // Cheap signature so an unchanged chat box is not sent to OCR again.
        public static int Stamp(ImageBuffer image)
        {
            if (image == null) return 0;
            int step = Math.Max(2, image.Width / 64);
            int hash = 17;
            for (int y = 0; y < image.Height; y += step)
            {
                for (int x = 0; x < image.Width; x += step)
                {
                    if (!Coloured(image.Argb(x, y))) continue;
                    hash = unchecked(hash * 31 + x);
                    hash = unchecked(hash * 17 + y);
                }
            }
            return hash;
        }

        // A line torn by scrolling: a potion sentence that did not match, with a
        // mark the settled game text does not use. Prayer, antifire and poison
        // stay unmatched on purpose and are not torn.
        public static bool TornPotion(string text)
        {
            if (text == null || text.Length == 0) return false;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? "";
                if (line.Length < 12) continue;
                string kind;
                string state;
                string phrase;
                if (ChatPhrase.MatchLine(line, out kind, out state, out phrase)) continue;
                string folded = ChatPhrase.Fold(line);
                // Fold turns i into l, so the cue has to be folded the same way.
                bool cue = folded.IndexOf(ChatPhrase.Fold("potion")) >= 0
                    || folded.IndexOf(ChatPhrase.Fold("overload")) >= 0
                    || folded.IndexOf(ChatPhrase.Fold("expire")) >= 0;
                if (!cue) continue;
                if (TornMark(line)) return true;
            }
            return false;
        }

        private static bool TornMark(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c < 32 || c > 126) return true;
                if (c == '*' || c == '<' || c == '>' || c == '=' || c == '|') return true;
            }
            return false;
        }

        public static ImageBuffer ForOcr(ImageBuffer image)
        {
            if (image == null) return null;
            int pad = 32;
            int w = image.Width * 3;
            int h = (image.Height + pad) * 3;
            if (w < 2 || h < 2) return null;
            int[] pixels = new int[w * h];
            int white = ImageBuffer.Pack(255, 255, 255);
            int black = ImageBuffer.Pack(0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = white;
            for (int y = 0; y < image.Height * 3; y++)
            {
                int sy = y / 3;
                if (sy >= image.Height) sy = image.Height - 1;
                for (int x = 0; x < w; x++)
                {
                    int sx = x / 3;
                    if (sx >= image.Width) sx = image.Width - 1;
                    pixels[y * w + x] = Coloured(image.Argb(sx, sy)) ? black : white;
                }
            }
            return new ImageBuffer(w, h, pixels);
        }
    }

    // After the chat crop changes, read it again on the next ticks even if the
    // colour hash matches, so a line that was mid-scroll is not left there.
    // A torn potion line asks for that same short repeat, once per crop.
    public sealed class ChatOcrGate
    {
        private bool have;
        private int last;
        private int left;
        private bool rearmed;

        public bool Due(int stamp, bool follow)
        {
            if (!have || stamp != last)
            {
                have = true;
                last = stamp;
                left = 2;
                rearmed = false;
                return true;
            }
            if (follow && !rearmed)
            {
                rearmed = true;
                if (left < 2) left = 2;
            }
            if (left > 0)
            {
                left--;
                return true;
            }
            return false;
        }
    }
}
