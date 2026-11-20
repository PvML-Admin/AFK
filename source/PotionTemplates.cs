using System;

namespace KavrosAfkWarden
{
    // Identity for the three flasks cropped from Danni's client. Each stamp is
    // the flask interior, averaged to 8x8, so 100%, 150% and 200% land on the
    // same grid. Elder is the dark teal flask, aggression the purple flask,
    // overload the green flask with a dark core.
    public static class PotionTemplates
    {
        private const int N = 8;
        private static readonly string[] Names = new string[] { "elder", "aggression", "overload" };
        private static readonly byte[][] Grids = new byte[][]
        {
            new byte[] {
                88,90,42,87,62,50,86,63,48,88,76,54,68,54,46,84,62,50,85,64,50,89,86,44,
                89,91,43,88,62,50,87,62,49,55,76,81,66,59,55,86,63,51,87,64,52,90,87,44,
                90,92,43,87,63,50,87,62,49,43,58,62,46,58,62,88,63,52,87,63,50,90,87,44,
                91,93,43,89,64,51,88,63,49,41,65,72,46,57,59,89,63,51,88,62,49,90,87,44,
                92,95,44,91,65,51,89,64,49,51,77,85,68,56,49,89,63,51,88,62,49,91,87,44,
                93,95,43,91,66,51,89,65,51,25,60,72,58,67,67,90,63,50,88,62,49,91,87,43,
                93,95,43,92,66,51,55,66,67,19,60,74,29,66,79,89,62,48,88,62,49,92,86,43,
                93,95,43,131,118,110,99,110,113,74,99,108,21,62,78,38,42,40,89,62,49,92,86,43
            },
            new byte[] {
                90,71,55,89,70,55,85,67,53,155,139,63,73,65,32,89,71,55,88,70,55,86,69,53,
                90,71,55,89,71,55,98,90,86,136,129,97,93,92,89,76,62,48,88,71,55,87,69,54,
                91,72,55,90,71,56,100,90,86,120,122,132,66,67,73,80,64,50,89,71,56,87,69,54,
                91,71,55,91,71,55,102,90,83,123,125,135,52,53,58,89,72,56,89,71,56,87,70,53,
                91,70,54,91,70,54,99,92,91,116,116,126,85,86,93,76,62,48,89,72,56,88,71,53,
                91,69,54,91,70,54,106,69,88,123,86,113,104,71,96,72,56,49,90,73,56,90,72,53,
                91,69,54,87,68,53,107,2,68,109,3,71,98,0,64,69,29,43,91,73,55,91,73,52,
                200,193,188,89,51,66,120,85,107,124,56,99,93,0,59,66,6,42,88,71,52,91,72,51
            },
            new byte[] {
                71,109,10,80,119,9,85,127,11,62,60,19,65,53,27,74,96,8,80,119,9,73,112,9,
                79,117,9,90,132,12,99,142,16,107,92,47,130,111,55,70,98,12,91,134,13,80,119,9,
                81,120,9,97,139,15,70,98,12,82,93,87,74,78,81,55,68,28,102,144,17,85,127,11,
                84,124,10,105,147,18,87,122,15,54,58,56,46,48,47,60,81,17,105,147,18,96,126,13,
                89,132,12,105,147,18,105,147,18,9,8,9,14,13,13,70,98,12,105,147,18,94,137,14,
                89,132,12,105,147,18,54,75,11,14,13,13,14,13,13,54,75,11,105,147,18,99,142,16,
                89,132,12,105,147,18,24,31,9,14,13,13,14,13,13,24,31,9,105,147,18,99,142,16,
                213,224,194,112,140,54,92,91,91,14,13,13,14,13,13,11,10,11,87,122,15,97,139,15
            }
        };

        // Empty when the icon is not one of the three flasks. A hit is strong
        // enough to trust on the first read.
        public static string Match(ImageBuffer image, IntRect icon)
        {
            if (image == null || icon.Width < 8 || icon.Height < 8) return "";
            int[] sample = Sample(image, icon);
            int best = 9999;
            int second = 9999;
            int bestAt = -1;
            for (int i = 0; i < Grids.Length; i++)
            {
                int score = Distance(sample, Grids[i]);
                if (score < best)
                {
                    second = best;
                    best = score;
                    bestAt = i;
                }
                else if (score < second) second = score;
            }
            if (bestAt < 0 || best > 36) return "";
            if (second - best < 18) return "";
            return Names[bestAt];
        }

        private static int Distance(int[] sample, byte[] grid)
        {
            int sum = 0;
            int n = N * N;
            for (int i = 0; i < n; i++)
            {
                int o = i * 3;
                sum += Math.Abs(sample[o] - grid[o]);
                sum += Math.Abs(sample[o + 1] - grid[o + 1]);
                sum += Math.Abs(sample[o + 2] - grid[o + 2]);
            }
            return sum / n;
        }

        private static int[] Sample(ImageBuffer image, IntRect icon)
        {
            int x0 = icon.X + icon.Width / 10;
            int x1 = icon.X + icon.Width - icon.Width / 10;
            int y0 = icon.Y + icon.Height / 12;
            int y1 = icon.Y + icon.Height * 72 / 100;
            if (x1 <= x0) x1 = x0 + 1;
            if (y1 <= y0) y1 = y0 + 1;
            int[] cells = new int[N * N * 3];
            for (int gy = 0; gy < N; gy++)
            {
                int ya = y0 + (y1 - y0) * gy / N;
                int yb = y0 + (y1 - y0) * (gy + 1) / N;
                if (yb <= ya) yb = ya + 1;
                for (int gx = 0; gx < N; gx++)
                {
                    int xa = x0 + (x1 - x0) * gx / N;
                    int xb = x0 + (x1 - x0) * (gx + 1) / N;
                    if (xb <= xa) xb = xa + 1;
                    int r = 0, g = 0, b = 0, count = 0;
                    for (int y = ya; y < yb; y++)
                    {
                        for (int x = xa; x < xb; x++)
                        {
                            if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) continue;
                            int color = image.Argb(x, y);
                            r += ImageBuffer.R(color);
                            g += ImageBuffer.G(color);
                            b += ImageBuffer.B(color);
                            count++;
                        }
                    }
                    int at = (gy * N + gx) * 3;
                    if (count > 0)
                    {
                        cells[at] = r / count;
                        cells[at + 1] = g / count;
                        cells[at + 2] = b / count;
                    }
                }
            }
            return cells;
        }
    }
}
