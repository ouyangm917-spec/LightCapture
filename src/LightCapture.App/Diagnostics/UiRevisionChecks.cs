using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.App.Native;
using LightCapture.App.Services;
using LightCapture.App.UI;
using LightCapture.Core.Annotations;
using LightCapture.Core.Geometry;

namespace LightCapture.App.Diagnostics;

internal static class UiRevisionChecks
{
    internal static async Task RunAsync(string output, Func<string, Func<Task<string>>, Task> check)
    {
        await check("工具栏拖动命中与屏幕边界", () =>
        {
            var toolbar = new CaptureToolbar();
            toolbar.Update(AnnotationKind.Step, 0xFFED805D, 3, true, true, 3, true, false, false);
            toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            toolbar.Arrange(new Rect(toolbar.DesiredSize)); toolbar.UpdateLayout();
            Require(DragSurface.CanDrag((DependencyObject)toolbar.FindName("DragHandle"), toolbar), "拖动柄未命中");
            Require(DragSurface.CanDrag(toolbar, toolbar), "空白处不能拖动");
            var buttons = Descendants(toolbar).OfType<ButtonBase>().ToArray();
            Require(buttons.Length > 15, "未覆盖工具按钮和标注样式");
            foreach (var button in buttons)
            {
                Require(!DragSurface.CanDrag(button, toolbar), "按钮被当作拖动入口");
                foreach (var child in Descendants(button)) Require(!DragSurface.CanDrag(child, toolbar), "按钮内容被当作拖动入口");
            }
            var area = new PixelRect(-1920, -1040, 1920, 1040);
            var inside = new PixelRect(-1500, -800, 640, 120);
            Require(NativeMethods.ConstrainToArea(inside, area) == inside, "有效的手动位置被重置");
            Require(NativeMethods.ConstrainToArea(new(-2000, -1100, 640, 120), area) == new PixelRect(-1920, -1040, 640, 120), "负坐标屏幕左上边界错误");
            Require(NativeMethods.ConstrainToArea(new(-400, -20, 640, 120), area) == new PixelRect(-640, -120, 640, 120), "展开后的工具栏超出屏幕");
            Require(NativeMethods.ConstrainToArea(new(-1000, -200, 2300, 1200), area).TopLeft == area.TopLeft, "超大窗口的拖动柄不可见");
            return Task.FromResult("拖动柄/空白可拖动；按钮及其图标文字均排除；负坐标、边缘展开和超大窗口约束通过。未调用系统 DragMove 或操作鼠标。");
        });

        await check("GIF 预览布局、参数与导出状态", () =>
        {
            var view = new GifPreviewView();
            view.Configure(1280, 800, 8.6);
            Require(view.OutputWidth == 1280 && view.Speed == 1, "原尺寸/正常速度默认值错误");
            view.PreviewImage.Source = SampleFrame();
            view.StartSlider.Value = 2; view.EndSlider.Value = 5.6;
            view.WidthBox.SelectedValue = 960; view.SpeedBox.SelectedIndex = 3;
            view.UpdateSummary();
            Require(view.OutputWidth == 960 && view.Speed == 2 && view.OutputSummary.Text.Contains("960 × 600") && view.OutputSummary.Text.Contains("1.8"), "修剪、尺寸或速度摘要错误");
            view.SpeedBox.SelectedIndex = 1; view.UpdateSummary();
            view.SeekSlider.Minimum = 2; view.SeekSlider.Maximum = 5.6; view.SeekSlider.Value = 3.8;
            view.PlaybackTime.Text = "01.8 / 03.6s"; view.SetPlaying(true);
            File.WriteAllBytes(Path.Combine(output, "gif-preview.png"), BitmapIO.Png(Render(view, 904, 600, 1)));
            Require(view.PreviewImage.ActualWidth > 560 && view.PreviewImage.ActualHeight > 300, "默认窗口未充分利用预览区");
            Require(Descendants(view.WidthBox).OfType<TextBlock>().Any(text => text.Text == "960 px"), "尺寸下拉框未显示用户可读标签");
            File.WriteAllBytes(Path.Combine(output, "gif-preview-125.png"), BitmapIO.Png(Render(view, 904, 600, 1.25)));
            var compact = Render(view, 784, 480, 1);
            File.WriteAllBytes(Path.Combine(output, "gif-preview-small.png"), BitmapIO.Png(compact));
            var saveOrigin = view.SaveButton.TranslatePoint(new Point(0, 0), view);
            Require(saveOrigin.Y >= 0 && saveOrigin.Y + view.SaveButton.ActualHeight <= 480 && saveOrigin.X + view.SaveButton.ActualWidth <= 784, "小窗口导出按钮被挤出");
            Require(view.PreviewImage.ActualWidth > 350 && view.PreviewImage.ActualHeight > 210 && Math.Abs(view.PreviewImage.ActualWidth / view.PreviewImage.ActualHeight - 1.6) < .02, "小窗口预览比例或可用面积异常");
            var track = (Track)view.StartSlider.Template.FindName("PART_Track", view.StartSlider);
            var beforeDrag = view.StartSlider.Value;
            // 只向离屏控件发送路由事件，不调用系统输入或捕获鼠标。
            track.Thumb.RaiseEvent(new DragDeltaEventArgs(12, 0) { RoutedEvent = Thumb.DragDeltaEvent });
            Require(view.StartSlider.Value > beforeDrag, "新滑块模板不能改变修剪时间");
            view.StartSlider.Value = beforeDrag;
            view.SetExporting(true);
            Require(!view.StartSlider.IsEnabled && !view.EndSlider.IsEnabled && !view.WidthBox.IsEnabled && !view.SpeedBox.IsEnabled && !view.SeekSlider.IsEnabled && !view.PlayButton.IsEnabled && !view.SaveButton.IsEnabled, "导出中仍可修改参数");
            Require(view.CloseButton.IsEnabled && view.ExportProgress.Visibility == Visibility.Visible, "导出中不能取消关闭或没有进度");
            view.ExportProgress.Value = 48; view.SetStatus("正在导出 48%…");
            File.WriteAllBytes(Path.Combine(output, "gif-preview-exporting.png"), BitmapIO.Png(Render(view, 904, 600, 1)));
            view.SetExporting(false);
            Require(view.SaveButton.IsEnabled && view.WidthBox.IsEnabled && view.SeekSlider.IsEnabled, "导出完成后控件未恢复");
            view.Configure(320, 240, .1);
            Require(view.WidthBox.Items.Count == 1 && view.OutputWidth == 320 && view.EndSlider.Value == .1, "短录制或小尺寸被放大");
            return Task.FromResult("实际控件离屏渲染 904×600 / 784×480 和 125% 比例；尺寸、速度、修剪摘要、播放位置及导出锁定/恢复通过，没有创建预览窗口。");
        });
    }

    private static BitmapSource Render(FrameworkElement element, double width, double height, double scale)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element); bitmap.Freeze(); return bitmap;
    }
    private static BitmapSource SampleFrame()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(248, 250, 249)), null, new Rect(0, 0, 1280, 800));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(42, 63, 58)), null, new Rect(0, 0, 230, 800));
            dc.DrawText(AnnotationRenderer.Format("工作空间", 30, Brushes.White, true), new Point(35, 35));
            dc.DrawText(AnnotationRenderer.Format("项目概览\n\n我的任务\n\n团队文档", 24, new SolidColorBrush(Color.FromRgb(174, 198, 186))), new Point(35, 140));
            dc.DrawText(AnnotationRenderer.Format("项目看板", 38, new SolidColorBrush(Color.FromRgb(49, 70, 61)), true), new Point(280, 40));
            dc.DrawText(AnnotationRenderer.Format("把简单的事情做好", 22, new SolidColorBrush(Color.FromRgb(144, 162, 152))), new Point(282, 102));
            var titles = new[] { "待开始", "进行中", "已完成" };
            var tasks = new[] { "整理操作步骤", "调整预览布局", "截图与标注" };
            for (var i = 0; i < 3; i++)
            {
                var x = 280 + i * 310;
                dc.DrawText(AnnotationRenderer.Format(titles[i], 25, new SolidColorBrush(Color.FromRgb(102, 127, 113)), true), new Point(x, 174));
                dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(221, 230, 225)), 2), new Rect(x, 232, 280, 260), 12, 12);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(230, 245, 237)), null, new Rect(x + 22, 256, 100, 32), 6, 6);
                dc.DrawText(AnnotationRenderer.Format("设计", 18, new SolidColorBrush(Color.FromRgb(19, 168, 107))), new Point(x + 52, 260));
                dc.DrawText(AnnotationRenderer.Format(tasks[i], 26, new SolidColorBrush(Color.FromRgb(49, 70, 61)), true), new Point(x + 22, 322));
                dc.DrawText(AnnotationRenderer.Format("轻截 / 界面优化", 20, new SolidColorBrush(Color.FromRgb(151, 168, 158))), new Point(x + 22, 375));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(237, 242, 239)), 2), new Point(x + 22, 426), new Point(x + 258, 426));
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(196, 222, 208)), null, new Point(x + 36, 458), 12, 12);
                dc.DrawText(AnnotationRenderer.Format("今天", 18, new SolidColorBrush(Color.FromRgb(151, 168, 158))), new Point(x + 200, 447));
            }
        }
        var bitmap = new RenderTargetBitmap(1280, 800, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
