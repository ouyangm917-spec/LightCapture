using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LightCapture.App.Services;

namespace LightCapture.App.UI;

internal sealed class TranslationWindow : Window
{
    private readonly TextBox _english = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 110, MaxLength = 12000 };
    private readonly TextBox _chinese = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsReadOnly = true, MinHeight = 130 };
    private readonly TextBlock _status = Ui.Text("正在识别英文…", 12, Ui.Muted);
    private readonly Button _translate;
    private readonly Button _copy;
    private CancellationTokenSource? _active;
    private bool _closed;

    internal TranslationWindow(BitmapSource image)
    {
        Title = "轻截 · 离线英译中"; Width = 480; Height = 480; MinWidth = 400; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { Margin = new Thickness(18) };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(1.1, GridUnitType.Star), GridLength.Auto, GridLength.Auto }) root.RowDefinitions.Add(new RowDefinition { Height = height });
        void Add(UIElement control, int row) { Grid.SetRow(control, row); root.Children.Add(control); }
        Add(Ui.Text("英文 · 可修正识别结果", 12, Ui.Muted), 0); Add(_english, 1);
        Add(Ui.Text("简体中文", 12, Ui.Muted), 2); Add(_chinese, 3); Add(_status, 4);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _translate = Ui.Button("重新翻译", async () => await TranslateAsync());
        _copy = Ui.Button("复制译文", async () =>
        {
            try
            {
                for (var attempt = 0; ; attempt++)
                {
                    try { Clipboard.SetText(_chinese.Text); break; }
                    catch (System.Runtime.InteropServices.COMException) when (attempt < 3) { await Task.Delay(80); }
                }
                _status.Text = "译文已复制";
            }
            catch (Exception e) { Ui.Error(e, this); }
        });
        _copy.IsEnabled = false; _translate.IsEnabled = false;
        actions.Children.Add(_translate); actions.Children.Add(_copy); Add(actions, 5); Content = root;
        var png = BitmapIO.Png(image);
        Loaded += async (_, _) =>
        {
            _active = new CancellationTokenSource();
            try { _english.Text = await OcrService.RecognizeAsync(png, _active.Token); if (!_closed) await TranslateAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!_closed) { _status.Text = e.Message; _translate.IsEnabled = true; } }
        };
        Closed += (_, _) => { _closed = true; _active?.Cancel(); };
    }
    private async Task TranslateAsync()
    {
        _active?.Cancel(); _active?.Dispose(); _active = new CancellationTokenSource();
        _translate.IsEnabled = false; _copy.IsEnabled = false; _status.Text = "正在本机翻译…";
        try
        {
            var result = await OfflineTranslator.TranslateAsync(_english.Text, _active.Token);
            if (_closed) return;
            _chinese.Text = result; _copy.IsEnabled = true; _status.Text = "离线完成 · 文字未上传";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (!_closed) _status.Text = e.Message; }
        finally { if (!_closed) _translate.IsEnabled = true; }
    }
}
