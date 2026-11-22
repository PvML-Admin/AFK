using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace KavrosAfkWarden
{
    // Windows.Media.Ocr, the reader built into Windows 10 and 11. One engine
    // is kept for the life of the process. A missing English language pack is
    // reported instead of guessed from glyph widths.
    internal static class WindowsChatOcr
    {
        private static OcrEngine engine;
        private static bool tried;
        private static string reason = "";
        public static string Trace = "";

        public static bool Available
        {
            get
            {
                Ensure();
                return engine != null;
            }
        }

        public static string Reason
        {
            get
            {
                Ensure();
                return reason ?? "";
            }
        }

        public static string Read(ImageBuffer image)
        {
            Ensure();
            if (engine == null) return "";
            ImageBuffer page = ChatPrep.ForOcr(image);
            if (page == null) return "";
            try
            {
                SoftwareBitmap bitmap = Bitmap(page);
                IAsyncOperation<OcrResult> operation = engine.RecognizeAsync(bitmap);
                int spins = 0;
                while (operation.Status == AsyncStatus.Started && spins < 80)
                {
                    Thread.Sleep(25);
                    spins++;
                }
                if (operation.Status != AsyncStatus.Completed)
                {
                    reason = operation.Status == AsyncStatus.Error ? "OCR failed" : "OCR timed out";
                    return "";
                }
                OcrResult result = operation.GetResults();
                StringBuilder text = new StringBuilder();
                if (result != null && result.Lines != null)
                {
                    List<OcrLine> rows = new List<OcrLine>();
                    for (int i = 0; i < result.Lines.Count; i++)
                    {
                        string line = result.Lines[i].Text ?? "";
                        if (line.Length == 0) continue;
                        OcrLine row = new OcrLine();
                        row.Text = line;
                        row.Order = i;
                        if (result.Lines[i].Words != null && result.Lines[i].Words.Count > 0)
                        {
                            row.X = (int)result.Lines[i].Words[0].BoundingRect.X;
                            row.Y = (int)result.Lines[i].Words[0].BoundingRect.Y;
                        }
                        else row.Y = i;
                        rows.Add(row);
                    }
                    rows.Sort(delegate(OcrLine a, OcrLine b)
                    {
                        int byY = a.Y.CompareTo(b.Y);
                        if (byY != 0) return byY;
                        int byX = a.X.CompareTo(b.X);
                        if (byX != 0) return byX;
                        return a.Order.CompareTo(b.Order);
                    });
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (text.Length > 0) text.Append('\n');
                        text.Append(rows[i].Text);
                    }
                }
                try { bitmap.Dispose(); } catch (Exception) { }
                return text.ToString();
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return "";
            }
        }

        private static void Ensure()
        {
            if (tried) return;
            tried = true;
            try
            {
                engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"));
                if (engine == null) engine = OcrEngine.TryCreateFromLanguage(new Language("en"));
                if (engine == null) engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine == null) reason = "no English OCR language pack";
            }
            catch (Exception ex)
            {
                engine = null;
                reason = ex.Message;
            }
        }

        private static SoftwareBitmap Bitmap(ImageBuffer image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[] bytes = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int color = image.Argb(x, y);
                    int i = (y * width + x) * 4;
                    bytes[i] = (byte)ImageBuffer.B(color);
                    bytes[i + 1] = (byte)ImageBuffer.G(color);
                    bytes[i + 2] = (byte)ImageBuffer.R(color);
                    bytes[i + 3] = 255;
                }
            }
            IBuffer buffer = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(bytes);
            return SoftwareBitmap.CreateCopyFromBuffer(buffer, BitmapPixelFormat.Bgra8, width, height);
        }
    }
}
