using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace KavrosAfkWarden
{
    // Reads the window's composition surface. Unlike CopyFromScreen, this still
    // returns frames when the client is covered or another application is focused.
    // Windows stops composing a minimized window, so this class is not used then.
    internal sealed class WindowSurfaceCapture : IDisposable
    {
        private static readonly Guid ItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        private static readonly Guid InteropGuid = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
        private static readonly Guid TextureGuid = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        private static readonly Guid DxgiDeviceGuid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        private static readonly Guid AccessGuid = new Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

        private IDirect3DDevice device;
        private IntPtr d3dDevice = IntPtr.Zero;
        private IntPtr d3dContext = IntPtr.Zero;
        private GraphicsCaptureItem item;
        private Direct3D11CaptureFramePool pool;
        private GraphicsCaptureSession session;
        private IntPtr target = IntPtr.Zero;
        private readonly object gate = new object();
        private ImageBuffer latest;
        private DateTime latestAt = DateTime.MinValue;
        private string failure = "";
        private bool unsupported;

        internal bool Available { get { return !unsupported && failure.Length == 0; } }
        internal string Failure { get { return unsupported ? "Window capture is not supported on this Windows build." : failure; } }

        internal void Ensure(IntPtr hwnd)
        {
            if (unsupported || hwnd == IntPtr.Zero) return;
            if (hwnd == target && session != null) return;
            Stop();
            try
            {
                if (!GraphicsCaptureSession.IsSupported()) { unsupported = true; return; }
                // The yellow border is a Graphics Capture system border. The
                // Windows 10 2004 contract this program is built against has no
                // IsBorderRequired property, so do not create a session at all.
                if (typeof(GraphicsCaptureSession).GetProperty("IsBorderRequired") == null)
                {
                    unsupported = true;
                    return;
                }
                EnsureDevice();
                item = CreateItem(hwnd);
                // Do not subscribe to Closed or FrameArrived. The .NET Framework
                // C# 5 compiler cannot use those WinRT events, and EventRegistrationToken
                // is defined in both mscorlib and System.Runtime.WindowsRuntime.
                // Frames are pulled with TryGetNextFrame instead.
                SizeInt32 itemSize = item.Size;
                pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, itemSize);
                session = pool.CreateCaptureSession(item);
                try { session.IsCursorCaptureEnabled = false; } catch (Exception) { }
                // Hide the yellow border when this Windows build allows it.
                // Capture still starts when the property is missing, so a covered
                // client can be read. PrintWindow is not used.
                TryDisableBorder(session);
                session.StartCapture();
                target = hwnd;
                failure = "";
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                Stop();
            }
        }

        internal ImageBuffer Copy(int cropX, int cropY, int cropW, int cropH)
        {
            Pump();
            lock (gate)
            {
                if (latest == null) return null;
                if ((DateTime.UtcNow - latestAt).TotalSeconds > 1.5) return null;
                if (cropW < 8 || cropH < 8) return latest;
                if (cropX == 0 && cropY == 0 && cropW >= latest.Width && cropH >= latest.Height) return latest;
                int w = Math.Min(cropW, latest.Width - cropX);
                int h = Math.Min(cropH, latest.Height - cropY);
                if (w < 8 || h < 8) return null;
                return latest.Crop(cropX, cropY, w, h);
            }
        }

        public void Dispose() { Stop(); ReleaseDevice(); }

        // Drain every buffered frame and keep the newest. A closed item or a
        // lost device throws, which drops back to the screen path.
        private void Pump()
        {
            if (pool == null || session == null) return;
            if (latest != null && (DateTime.UtcNow - latestAt).TotalMilliseconds < 400) return;
            Direct3D11CaptureFrame frame = null;
            try
            {
                while (true)
                {
                    Direct3D11CaptureFrame next = pool.TryGetNextFrame();
                    if (next == null) break;
                    if (frame != null) frame.Dispose();
                    frame = next;
                }
                if (frame == null) return;
                SizeInt32 content = frame.ContentSize;
                ImageBuffer image = ReadFrame(frame.Surface, content.Width, content.Height);
                if (image != null)
                {
                    lock (gate)
                    {
                        latest = image;
                        latestAt = DateTime.UtcNow;
                    }
                }
                SizeInt32 size = content;
                if (item != null) size = item.Size;
                if (size.Width != content.Width || size.Height != content.Height)
                    pool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                Stop();
            }
            finally
            {
                if (frame != null) frame.Dispose();
            }
        }

        private static bool TryDisableBorder(GraphicsCaptureSession session)
        {
            try
            {
                System.Reflection.PropertyInfo property = session.GetType().GetProperty("IsBorderRequired");
                if (property == null || !property.CanWrite) return false;
                property.SetValue(session, false, null);
                object value = property.GetValue(session, null);
                return value is bool && (bool)value == false;
            }
            catch (Exception) { return false; }
        }

        private void Stop()
        {
            target = IntPtr.Zero;
            lock (gate) { latest = null; latestAt = DateTime.MinValue; }
            try { if (session != null) session.Dispose(); } catch (Exception) { }
            try { if (pool != null) pool.Dispose(); } catch (Exception) { }
            session = null;
            pool = null;
            item = null;
        }

        private void EnsureDevice()
        {
            if (d3dDevice != IntPtr.Zero) return;
            IntPtr devicePtr, contextPtr;
            int level;
            int hr = D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out devicePtr, out level, out contextPtr);
            if (hr < 0) hr = D3D11CreateDevice(IntPtr.Zero, 5, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out devicePtr, out level, out contextPtr);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            d3dDevice = devicePtr;
            d3dContext = contextPtr;
            Guid dxgi = DxgiDeviceGuid;
            IntPtr dxgiDevice;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, ref dxgi, out dxgiDevice));
            IntPtr inspectable;
            try { Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectable)); }
            finally { Marshal.Release(dxgiDevice); }
            try { device = (IDirect3DDevice)Marshal.GetObjectForIUnknown(inspectable); }
            finally { Marshal.Release(inspectable); }
        }

        private void ReleaseDevice()
        {
            device = null;
            if (d3dContext != IntPtr.Zero) { Marshal.Release(d3dContext); d3dContext = IntPtr.Zero; }
            if (d3dDevice != IntPtr.Zero) { Marshal.Release(d3dDevice); d3dDevice = IntPtr.Zero; }
        }

        private ImageBuffer ReadFrame(IDirect3DSurface surface, int width, int height)
        {
            if (surface == null || width < 8 || height < 8 || d3dDevice == IntPtr.Zero) return null;
            IntPtr unknown = Marshal.GetIUnknownForObject(surface);
            IntPtr access = IntPtr.Zero, texture = IntPtr.Zero, staging = IntPtr.Zero;
            try
            {
                Guid accessId = AccessGuid;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, ref accessId, out access));
                Guid textureId = TextureGuid;
                GetInterfaceDelegate getInterface = Vtable<GetInterfaceDelegate>(access, 3);
                Marshal.ThrowExceptionForHR(getInterface(access, ref textureId, out texture));
                Texture2DDesc desc = new Texture2DDesc();
                desc.Width = width;
                desc.Height = height;
                desc.MipLevels = 1;
                desc.ArraySize = 1;
                desc.Format = 87;
                desc.SampleCount = 1;
                desc.Usage = 3;
                desc.CpuAccess = 0x20000;
                CreateTextureDelegate create = Vtable<CreateTextureDelegate>(d3dDevice, 5);
                Marshal.ThrowExceptionForHR(create(d3dDevice, ref desc, IntPtr.Zero, out staging));
                CopyResourceDelegate copy = Vtable<CopyResourceDelegate>(d3dContext, 47);
                copy(d3dContext, staging, texture);
                MappedSubresource mapped;
                MapDelegate map = Vtable<MapDelegate>(d3dContext, 14);
                Marshal.ThrowExceptionForHR(map(d3dContext, staging, 0, 1, 0, out mapped));
                try
                {
                    int[] pixels = new int[width * height];
                    byte[] row = new byte[width * 4];
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(mapped.Data, y * mapped.RowPitch), row, 0, row.Length);
                        for (int x = 0; x < width; x++)
                        {
                            int b = row[x * 4], g = row[x * 4 + 1], r = row[x * 4 + 2];
                            pixels[y * width + x] = ImageBuffer.Pack(r, g, b);
                        }
                    }
                    return new ImageBuffer(width, height, pixels);
                }
                finally
                {
                    UnmapDelegate unmap = Vtable<UnmapDelegate>(d3dContext, 15);
                    unmap(d3dContext, staging, 0);
                }
            }
            finally
            {
                if (staging != IntPtr.Zero) Marshal.Release(staging);
                if (texture != IntPtr.Zero) Marshal.Release(texture);
                if (access != IntPtr.Zero) Marshal.Release(access);
                if (unknown != IntPtr.Zero) Marshal.Release(unknown);
            }
        }

        private static GraphicsCaptureItem CreateItem(IntPtr hwnd)
        {
            IntPtr name;
            WindowsCreateString("Windows.Graphics.Capture.GraphicsCaptureItem", "Windows.Graphics.Capture.GraphicsCaptureItem".Length, out name);
            Guid interop = InteropGuid;
            IntPtr factory;
            int hr = RoGetActivationFactory(name, ref interop, out factory);
            WindowsDeleteString(name);
            Marshal.ThrowExceptionForHR(hr);
            try
            {
                CreateForWindowDelegate create = Vtable<CreateForWindowDelegate>(factory, 3);
                Guid itemId = ItemGuid;
                IntPtr itemPtr;
                Marshal.ThrowExceptionForHR(create(factory, hwnd, ref itemId, out itemPtr));
                try { return (GraphicsCaptureItem)Marshal.GetObjectForIUnknown(itemPtr); }
                finally { Marshal.Release(itemPtr); }
            }
            finally { Marshal.Release(factory); }
        }

        private static T Vtable<T>(IntPtr com, int slot) where T : class
        {
            IntPtr table = Marshal.ReadIntPtr(com);
            IntPtr method = Marshal.ReadIntPtr(table, slot * IntPtr.Size);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(method, typeof(T));
        }

        [DllImport("d3d11.dll")]
        private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr levels, uint levelCount, uint sdk, out IntPtr device, out int featureLevel, out IntPtr context);

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport("combase.dll", PreserveSig = true)]
        private static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);

        [DllImport("combase.dll")]
        private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string source, int length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateForWindowDelegate(IntPtr self, IntPtr hwnd, ref Guid iid, out IntPtr item);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetInterfaceDelegate(IntPtr self, ref Guid iid, out IntPtr ptr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateTextureDelegate(IntPtr self, ref Texture2DDesc desc, IntPtr initial, out IntPtr texture);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyResourceDelegate(IntPtr self, IntPtr destination, IntPtr source);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MapDelegate(IntPtr self, IntPtr resource, int subresource, int mapType, int mapFlags, out MappedSubresource mapped);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UnmapDelegate(IntPtr self, IntPtr resource, int subresource);

        [StructLayout(LayoutKind.Sequential)]
        private struct Texture2DDesc
        {
            public int Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CpuAccess, MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MappedSubresource
        {
            public IntPtr Data;
            public int RowPitch;
            public int DepthPitch;
        }
    }
}
