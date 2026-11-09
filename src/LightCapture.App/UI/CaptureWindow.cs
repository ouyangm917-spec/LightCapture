using System.Collections.Immutable;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.App.Native;
using LightCapture.App.Services;
using LightCapture.Core.Annotations;
using LightCapture.Core.Geometry;

namespace LightCapture.App.UI;

internal sealed class CaptureWindow : Window
{
    private readonly PixelRect _desktop;
    private readonly BitmapSource _snapshot;
    private readonly AnnotationDocument _document = new();
    private readonly CaptureCanvas _canvas;
    private readonly Action<PixelRect> _record;
    private Window? _toolbar;
    private CaptureToolbar? _toolbarContent;
    private AnnotationRenderer? _renderer;
    private BitmapSource? _originalSelection;
    private BitmapSource? _translatedSelection;
    private BitmapSource? _activeImage;
    private CancellationTokenSource? _translationCancellation;
    private PixelRect _selection;
    private PixelPoint _anchor;
    private Annotation? _draft;
    private Annotation? _moving;
    private Guid? _selectedId;
    private AnnotationKind _tool = AnnotationKind.Select;
    private uint _color = 0xFFED805D;
    private double _thickness = 3;
    private bool _dragging;
    private bool _choosing = true;
    private bool _busy;
    private bool _closed;
    private bool _showingTranslation;

    internal CaptureWindow(BitmapSource snapshot, PixelRect desktop, Action<PixelRect> record)
    {
        _snapshot = snapshot; _desktop = desktop; _record = record;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        Left = desktop.X; Top = desktop.Y; Width = desktop.Width; Height = desktop.Height;
        Background = Brushes.Black;
        _canvas = new CaptureCanvas(this) { Cursor = Cursors.Cross };
        Content = _canvas;
        Loaded += (_, _) => { NativeMethods.Place(this, desktop); Activate(); _canvas.Focus(); };
        _canvas.MouseLeftButtonDown += PointerDown;
        _canvas.MouseMove += PointerMove;
        _canvas.MouseLeftButtonUp += PointerUp;
        _canvas.MouseRightButtonDown += (_, _) => Close();
        KeyDown += HandleKey;
        _document.Changed += (_, _) => { if (!_document.Items.Any(a => a.Id == _selectedId)) _selectedId = null; Refresh(); };
        Closed += (_, _) => { _closed = true; _translationCancellation?.Cancel(); _toolbar?.Close(); };
    }

    private PixelPoint Position(MouseEventArgs e)
    {
        var p = e.GetPosition(_canvas);
        return new(Math.Clamp(p.X / Math.Max(1, _canvas.ActualWidth) * _snapshot.PixelWidth, 0, _snapshot.PixelWidth), Math.Clamp(p.Y / Math.Max(1, _canvas.ActualHeight) * _snapshot.PixelHeight, 0, _snapshot.PixelHeight));
    }
    private PixelPoint Local(PixelPoint p) => new(Math.Clamp(p.X - _selection.X, 0, _selection.Width), Math.Clamp(p.Y - _selection.Y, 0, _selection.Height));
    private Annotation? Selected => _document.Items.FirstOrDefault(a => a.Id == _selectedId);
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_busy) return;
        Activate(); _canvas.Focus();
        var point = Position(e);
        if (_choosing)
        {
            _anchor = point; _selection = new(point.X, point.Y, 0, 0); _dragging = true; _canvas.CaptureMouse(); return;
        }
        if (!_selection.Contains(point)) return;
        point = Local(point); _anchor = point;
        if (_tool == AnnotationKind.Select)
        {
            _moving = _document.Items.LastOrDefault(a => a.HitTest(point)); _selectedId = _moving?.Id;
            if (e.ClickCount == 2 && _moving?.Kind == AnnotationKind.Text) { EditText(_moving); _moving = null; Refresh(); return; }
            _dragging = _moving != null;
        }
        else if (_tool == AnnotationKind.Text) { EditText(new Annotation { Kind = _tool, Start = point, End = point, ColorArgb = _color, Thickness = _thickness }); return; }
        else if (_tool == AnnotationKind.Step)
        {
            var step = new Annotation { Kind = _tool, Start = point, End = point, ColorArgb = _color, Thickness = _thickness };
            _document.Add(step); _selectedId = step.Id; Refresh(); return;
        }
        else
        {
            _draft = new Annotation { Kind = _tool, Start = point, End = point, ColorArgb = _color, Thickness = _thickness, Points = _tool == AnnotationKind.Pen ? [point] : [] };
            _selectedId = null; _dragging = true;
        }
        if (_dragging) _canvas.CaptureMouse();
        Refresh();
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var point = Position(e);
        if (_choosing) _selection = PixelRect.FromPoints(_anchor, point).RoundOut();
        else
        {
            point = Local(point);
            if (_moving != null) _draft = _moving.Move(point.X - _anchor.X, point.Y - _anchor.Y);
            else if (_draft != null)
            {
                var points = _draft.Points;
                if (_tool == AnnotationKind.Pen && (points.Length == 0 || points[^1].DistanceTo(point) >= 1)) points = points.Add(point);
                _draft = _draft with { End = point, Points = points };
            }
        }
        Refresh();
    }
    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        PointerMove(sender, e); _dragging = false; _canvas.ReleaseMouseCapture();
        if (_choosing)
        {
            if (_selection.Width < 4 || _selection.Height < 4) { _selection = default; Refresh(); return; }
            _choosing = false;
            _originalSelection = BitmapIO.Crop(_snapshot, _selection);
            SetImage(_originalSelection);
            _canvas.Cursor = Cursors.Arrow;
            ShowToolbar();
        }
        else if (_draft != null)
        {
            if (_moving != null) _document.Update(_draft);
            else if (_draft.Kind == AnnotationKind.Pen || _draft.Start.DistanceTo(_draft.End) >= 2) _document.Add(_draft);
            _selectedId = _draft.Id;
        }
        _draft = null; _moving = null; Refresh();
    }
    private void EditText(Annotation item)
    {
        var dialog = new TextEntryWindow(this, item.Text);
        if (dialog.ShowDialog() != true) return;
        item = item with { Text = dialog.Value.Trim() };
        if (_document.Items.Any(a => a.Id == item.Id)) _document.Update(item); else _document.Add(item);
        _selectedId = item.Id; Refresh();
    }
    private void Refresh()
    {
        _canvas.InvalidateVisual();
        _toolbarContent?.Update(_tool, Selected?.ColorArgb ?? _color, Selected?.Thickness ?? _thickness,
            _document.CanUndo, _document.CanRedo, _document.NextStepNumber, Selected != null, _busy, _showingTranslation);
    }
    private void Choose(AnnotationKind kind) { if (_busy) return; _tool = kind; _selectedId = null; _canvas.Cursor = kind == AnnotationKind.Select ? Cursors.Arrow : Cursors.Cross; Refresh(); }
    private void ChangeStyle(uint? color = null, double? thickness = null)
    {
        if (_busy) return;
        if (color.HasValue) _color = color.Value;
        if (thickness.HasValue) _thickness = thickness.Value;
        if (Selected is { } item) _document.Update(item with { ColorArgb = color ?? item.ColorArgb, Thickness = thickness ?? item.Thickness });
        Refresh();
    }
    private void ShowToolbar()
    {
        _toolbarContent = new CaptureToolbar();
        _toolbarContent.ToolChosen += Choose;
        _toolbarContent.ColorChosen += color => ChangeStyle(color: color);
        _toolbarContent.ThicknessChosen += thickness => ChangeStyle(thickness: thickness);
        _toolbarContent.Undo += () => _document.Undo();
        _toolbarContent.Redo += () => _document.Redo();
        _toolbarContent.Delete += () => { if (_selectedId is { } id) _document.Delete(id); };
        _toolbarContent.Translate += async () => await TranslateAsync();
        _toolbarContent.CancelTranslation += () => { _translationCancellation?.Cancel(); _toolbarContent.ShowStatus("正在取消翻译…"); };
        _toolbarContent.Record += () => CompleteAction(() => _record(_selection.Offset(_desktop.X, _desktop.Y)));
        _toolbarContent.Save += async () => await SaveAsync();
        _toolbarContent.Complete += async () => await CopyAsync();
        _toolbarContent.Cancel += Close;
        _toolbar = new Window { Owner = this, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, ShowActivated = false, Topmost = true, AllowsTransparency = true, Background = Brushes.Transparent, Content = _toolbarContent };
        _toolbar.KeyDown += HandleKey;
        _toolbar.Loaded += (_, _) => NativeMethods.PlaceNear(_toolbar, _selection.Offset(_desktop.X, _desktop.Y));
        _toolbar.SizeChanged += (_, _) => { if (_toolbar.IsLoaded) NativeMethods.PlaceNear(_toolbar, _selection.Offset(_desktop.X, _desktop.Y)); };
        Refresh(); _toolbar.Show();
    }
    private void SetImage(BitmapSource image)
    {
        _activeImage = image;
        _renderer = new AnnotationRenderer(image);
    }
    private async Task TranslateAsync()
    {
        if (_choosing || _busy || _dragging || _originalSelection == null) return;
        if (_translatedSelection != null)
        {
            _showingTranslation = !_showingTranslation;
            SetImage(_showingTranslation ? _translatedSelection : _originalSelection);
            _toolbarContent?.ShowStatus(_showingTranslation ? "已译图 · 再点翻译可查看原图" : "原图 · 再点翻译可恢复译图");
            Refresh(); return;
        }
        using var cancellation = new CancellationTokenSource();
        _translationCancellation = cancellation;
        _busy = true; Refresh();
        try
        {
            var progress = new Progress<string>(text =>
            {
                if (!_closed && ReferenceEquals(_translationCancellation, cancellation) && !cancellation.IsCancellationRequested)
                    _toolbarContent?.ShowStatus(text, cancellable: true);
            });
            var translated = await ImageTranslationService.TranslateAsync(_originalSelection, cancellation.Token, progress);
            if (_closed || cancellation.IsCancellationRequested) return;
            _translatedSelection = translated; _showingTranslation = true;
            SetImage(translated);
            _toolbarContent?.ShowStatus("已译图 · 再点翻译可查看原图");
        }
        catch (OperationCanceledException) { if (!_closed) _toolbarContent?.ShowStatus("已取消翻译"); }
        catch (Exception e) { if (!_closed) _toolbarContent?.ShowStatus($"翻译未完成：{e.Message}", error: true); }
        finally
        {
            _translationCancellation = null; _busy = false;
            if (!_closed)
            {
                if (cancellation.IsCancellationRequested) _toolbarContent?.ShowStatus("已取消翻译");
                Refresh();
            }
        }
    }
    private BitmapSource Render() => _renderer!.Render(_document.Items);
    private void CompleteAction(Action action) { if (_busy) return; try { action(); Close(); } catch (Exception e) { Ui.Error(e, this); } }
    private async Task CopyAsync()
    {
        if (_choosing || _busy) return; _busy = true; Refresh();
        try { await BitmapIO.CopyAsync(Render()); Close(); } catch (Exception e) { if (!_closed) Ui.Error(e, this); } finally { _busy = false; if (!_closed) Refresh(); }
    }
    private async Task SaveAsync()
    {
        if (_choosing || _busy) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "PNG 图片|*.png", FileName = $"轻截-{DateTime.Now:yyyyMMdd-HHmmss}.png" };
        if (dialog.ShowDialog(this) != true) return;
        _busy = true; Refresh();
        try { await File.WriteAllBytesAsync(dialog.FileName, BitmapIO.Png(Render())); Close(); } catch (Exception e) { if (!_closed) Ui.Error(e, this); } finally { _busy = false; if (!_closed) Refresh(); }
    }
    private async void HandleKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        if (_choosing || _busy) return;
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (control && e.Key == Key.Z) { if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) _document.Redo(); else _document.Undo(); }
        else if (control && e.Key == Key.Y) _document.Redo();
        else if (control && e.Key == Key.S) await SaveAsync();
        else if (e.Key == Key.Enter || (control && e.Key == Key.C)) await CopyAsync();
        else if (e.Key == Key.Delete || e.Key == Key.Back) { if (_selectedId is { } id) _document.Delete(id); }
        else if (!control)
        {
            var kind = e.Key switch { Key.V => AnnotationKind.Select, Key.R => AnnotationKind.Rectangle, Key.O => AnnotationKind.Ellipse, Key.A => AnnotationKind.Arrow, Key.P => AnnotationKind.Pen, Key.T => AnnotationKind.Text, Key.M => AnnotationKind.Mosaic, Key.S => AnnotationKind.Step, _ => (AnnotationKind?)null };
            if (kind.HasValue) Choose(kind.Value); else return;
        }
        else return;
        e.Handled = true;
    }

    private sealed class CaptureCanvas(CaptureWindow owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            dc.PushTransform(new ScaleTransform(ActualWidth / owner._snapshot.PixelWidth, ActualHeight / owner._snapshot.PixelHeight));
            var width = owner._snapshot.PixelWidth; var height = owner._snapshot.PixelHeight;
            dc.DrawImage(owner._snapshot, new Rect(0, 0, width, height));
            var shade = new SolidColorBrush(Color.FromArgb(130, 0, 0, 0));
            var r = owner._selection;
            if (r.IsEmpty) dc.DrawRectangle(shade, null, new Rect(0, 0, width, height));
            else
            {
                dc.DrawRectangle(shade, null, new Rect(0, 0, width, r.Y));
                dc.DrawRectangle(shade, null, new Rect(0, r.Bottom, width, Math.Max(0, height - r.Bottom)));
                dc.DrawRectangle(shade, null, new Rect(0, r.Y, r.X, r.Height));
                dc.DrawRectangle(shade, null, new Rect(r.Right, r.Y, Math.Max(0, width - r.Right), r.Height));
                if (owner._renderer != null)
                {
                    dc.PushTransform(new TranslateTransform(r.X, r.Y));
                    dc.PushClip(new RectangleGeometry(new Rect(0, 0, r.Width, r.Height)));
                    dc.DrawImage(owner._activeImage, new Rect(0, 0, r.Width, r.Height));
                    foreach (var item in owner._document.Items)
                        if (item.Id != owner._draft?.Id) owner._renderer.Draw(dc, item);
                    if (owner._draft != null) owner._renderer.Draw(dc, owner._draft);
                    if (owner.Selected is { } selected)
                    {
                        var bounds = (owner._draft?.Id == selected.Id ? owner._draft : selected).Bounds.Inflate(5);
                        dc.DrawRectangle(null, new Pen(Brushes.White, 1) { DashStyle = DashStyles.Dash }, BitmapIO.Rect(bounds));
                    }
                    dc.Pop(); dc.Pop();
                }
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(32, 177, 125)), 1), BitmapIO.Rect(r));
                var size = AnnotationRenderer.Format($"{r.Width:0} × {r.Height:0}", 13, Brushes.White);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 29, 35, 42)), null, new Rect(r.X, Math.Max(0, r.Y - 27), size.Width + 16, 24));
                dc.DrawText(size, new Point(r.X + 8, Math.Max(0, r.Y - 27) + 3));
            }
            if (owner._choosing && !owner._dragging)
            {
                var hint = AnnotationRenderer.Format("拖动选择截图区域 · Esc 退出", 20, Brushes.White);
                dc.DrawText(hint, new Point(Math.Max(20, (width - hint.Width) / 2), Math.Max(20, height / 2 - 30)));
            }
            dc.Pop();
        }
    }
}
