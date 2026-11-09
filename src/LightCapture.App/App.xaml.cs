using System.Security.Principal;
using System.Windows;
using LightCapture.App.Native;
using LightCapture.App.Services;
using LightCapture.App.UI;
using Forms = System.Windows.Forms;

namespace LightCapture.App;

public partial class App : Application
{
    private Mutex? _instance;
    private EventWaitHandle? _captureSignal;
    private RegisteredWaitHandle? _signalRegistration;
    private HotkeyService? _hotkey;
    private Forms.NotifyIcon? _tray;
    private SettingsStore? _settings;
    private CaptureWindow? _capture;
    private RecordingWindow? _recording;
    private SettingsWindow? _settingsWindow;
    private bool _ownsInstance;
    private bool _starting;
    private bool _exiting;
    private string HotkeyLabel => _settings?.Value.ControlHotkey == true ? "Ctrl+Alt+A" : "Alt+A";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string? Argument(string name) { var i = Array.IndexOf(e.Args, name); return i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : null; }
        try
        {
            if (e.Args.Contains("--self-test"))
            {
                var code = await Diagnostics.NativeSelfTests.RunAsync(Argument("--output") ?? Path.Combine(AppContext.BaseDirectory, "self-test"));
                Shutdown(code); return;
            }
            var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            _instance = new Mutex(true, $"Local\\LightCapture.{identity}", out _ownsInstance);
            _captureSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\LightCapture.Capture.{identity}");
            if (!_ownsInstance) { _captureSignal.Set(); Shutdown(); return; }
            _settings = new SettingsStore(Argument("--data-dir"));
            _hotkey = new HotkeyService(); _hotkey.Pressed += CaptureOrStop;
            var registered = _hotkey.Register(_settings.Value.ControlHotkey);
            if (!registered)
            {
                _settings.Value.ControlHotkey = !_settings.Value.ControlHotkey;
                registered = _hotkey.Register(_settings.Value.ControlHotkey);
                if (registered) _settings.Save();
            }
            _signalRegistration = ThreadPool.RegisterWaitForSingleObject(_captureSignal, (_, _) => Dispatcher.BeginInvoke(CaptureOrStop), null, Timeout.Infinite, false);
            CreateTray();
            if (!registered) { _tray!.ShowBalloonTip(6000, "截图快捷键未注册", $"系统错误 {_hotkey.LastError}。可以从托盘截图，或在设置中调整快捷键。", Forms.ToolTipIcon.Warning); ShowSettings(); }
            else if (!_settings.Value.SeenWelcome)
            {
                _tray!.ShowBalloonTip(5000, "轻截已就绪", $"按 {HotkeyLabel} 截图，标注后按 Enter 复制。右击托盘图标可打开设置。", Forms.ToolTipIcon.Info);
                _settings.Value.SeenWelcome = true; _settings.Save();
            }
            if (e.Args.Contains("--demo")) new Diagnostics.DemoWindow().Show();
            if (e.Args.Contains("--capture")) CaptureOrStop();
        }
        catch (Exception exception)
        {
            if (e.Args.Contains("--self-test")) Console.Error.WriteLine($"Self-test failed: {exception.Message}");
            else Ui.Error(exception);
            Shutdown(1);
        }
    }
    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("截图", null, (_, _) => Dispatcher.BeginInvoke(CaptureOrStop));
        menu.Items.Add("设置", null, (_, _) => Dispatcher.BeginInvoke(ShowSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.BeginInvoke(ExitApp));
        _tray = new Forms.NotifyIcon { Text = $"轻截 · {HotkeyLabel}", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(CaptureOrStop);
    }
    private async void CaptureOrStop()
    {
        if (_exiting) return;
        if (_recording != null) { _recording.Stop(); return; }
        if (_capture != null) { _capture.Activate(); return; }
        if (_starting) return; _starting = true;
        try
        {
            _settingsWindow?.Hide();
            await Task.Delay(150);
            if (_exiting) return;
            var bounds = ScreenCaptureService.DesktopBounds;
            var image = await Task.Run(ScreenCaptureService.Desktop);
            if (_exiting) return;
            _capture = new CaptureWindow(image, bounds, StartRecording);
            _capture.Closed += (_, _) => _capture = null;
            _capture.Show();
        }
        catch (Exception e) { if (!_exiting) Ui.Error(e); }
        finally { _starting = false; TryFinishExit(); }
    }
    private void StartRecording(LightCapture.Core.Geometry.PixelRect region)
    {
        if (_exiting) return;
        _recording = new RecordingWindow(region, Path.Combine(_settings!.DataDirectory, "recordings"), HotkeyLabel);
        _recording.Ready += recording =>
        {
            if (_exiting) { recording.Dispose(); return; }
            try { new GifPreviewWindow(recording).Show(); }
            catch (Exception e) { recording.Dispose(); Ui.Error(e); }
        };
        _recording.Failed += error => { if (!_exiting) Ui.Error(error); };
        _recording.Closed += (_, _) => _recording = null;
        var window = _recording;
        try { window.Show(); }
        catch { _recording = null; window.Close(); throw; }
    }
    private void ShowSettings()
    {
        if (_exiting) return;
        if (_settingsWindow != null) { _settingsWindow.Show(); _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(_settings!, useControl =>
        {
            if (_hotkey!.Register(useControl)) { if (_tray != null) _tray.Text = $"轻截 · {(useControl ? "Ctrl+Alt+A" : "Alt+A")}"; return null; }
            var error = _hotkey.LastError;
            _hotkey.Register(_settings!.Value.ControlHotkey);
            return error == 1409 ? "这个快捷键已被占用，请换一个。" : $"快捷键注册失败（系统错误 {error}），请从托盘截图。";
        });
        _settingsWindow.Closed += (_, _) => _settingsWindow = null; _settingsWindow.Show();
    }
    private void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        foreach (var window in Windows.Cast<Window>().ToArray())
        {
            // 录制或导出可以异步取消关闭；最终 Closed 后再释放托盘与热键。
            window.Closed += (_, _) => Dispatcher.BeginInvoke(TryFinishExit);
            window.Close();
        }
        TryFinishExit();
    }
    private void TryFinishExit()
    {
        if (_exiting && !_starting && _recording == null && Windows.Count == 0) Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose(); _hotkey?.Dispose(); _signalRegistration?.Unregister(null); _captureSignal?.Dispose();
        if (_ownsInstance) _instance?.ReleaseMutex(); _instance?.Dispose(); base.OnExit(e);
    }
}
