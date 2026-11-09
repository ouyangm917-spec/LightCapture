using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace LightCapture.App.Native;

internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source = new(new HwndSourceParameters("LightCapture.Hotkey") { Width = 0, Height = 0, WindowStyle = 0 });
    public event Action? Pressed;
    public int LastError { get; private set; }
    public HotkeyService() => _source.AddHook(OnMessage);
    public bool Register(bool control)
    {
        NativeMethods.UnregisterHotKey(_source.Handle, 1);
        var registered = NativeMethods.RegisterHotKey(_source.Handle, 1, 0x4001u | (control ? 2u : 0u), (uint)KeyInterop.VirtualKeyFromKey(Key.A));
        LastError = registered ? 0 : Marshal.GetLastWin32Error();
        return registered;
    }
    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == 1) { handled = true; Pressed?.Invoke(); }
        return IntPtr.Zero;
    }
    public void Dispose() { NativeMethods.UnregisterHotKey(_source.Handle, 1); _source.Dispose(); }
}
