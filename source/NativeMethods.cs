using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace KavrosAfkWarden
{
    internal static class NativeMethods
    {
        internal const uint WdaExcludeFromCapture = 0x11;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PointStruct { public int X, Y; }

        internal delegate bool EnumProc(IntPtr window, IntPtr param);

        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window, ref PointStruct point);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(PointStruct point);
        [StructLayout(LayoutKind.Sequential)]
        internal struct LastInput { public int Size; public uint Time; }

        [DllImport("user32.dll")] internal static extern bool GetLastInputInfo(ref LastInput info);
        [DllImport("kernel32.dll")] internal static extern uint GetTickCount();
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out PointStruct point);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
        [DllImport("user32.dll")] internal static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        internal static Rectangle ClientBounds(IntPtr window)
        {
            Rect rect;
            PointStruct point = new PointStruct();
            if (!GetClientRect(window, out rect) || !ClientToScreen(window, ref point)) return Rectangle.Empty;
            return new Rectangle(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        internal static Rectangle WindowBounds(IntPtr window)
        {
            Rect rect;
            if (!GetWindowRect(window, out rect)) return Rectangle.Empty;
            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        internal static void ApplyDarkCaption(IntPtr handle)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(handle, 19, ref on, 4);
                int caption = KavrosTheme.TitleBar.R | (KavrosTheme.TitleBar.G << 8) | (KavrosTheme.TitleBar.B << 16);
                int border = KavrosTheme.Frame.R | (KavrosTheme.Frame.G << 8) | (KavrosTheme.Frame.B << 16);
                int text = KavrosTheme.Text.R | (KavrosTheme.Text.G << 8) | (KavrosTheme.Text.B << 16);
                DwmSetWindowAttribute(handle, 35, ref caption, 4);
                DwmSetWindowAttribute(handle, 34, ref border, 4);
                DwmSetWindowAttribute(handle, 36, ref text, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr window, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

        // GWLP_HWNDPARENT. The floating card is owned
        // by the game window: it follows that window's z-order and is not a
        // topmost window over other applications.
        internal static void SetOwner(IntPtr child, IntPtr owner)
        {
            if (child == IntPtr.Zero) return;
            if (IntPtr.Size == 8) SetWindowLongPtr64(child, -8, owner);
            else SetWindowLong32(child, -8, owner.ToInt32());
        }

        internal static void ExcludeFromCapture(IntPtr window)
        {
            if (window == IntPtr.Zero) return;
            try { SetWindowDisplayAffinity(window, WdaExcludeFromCapture); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
