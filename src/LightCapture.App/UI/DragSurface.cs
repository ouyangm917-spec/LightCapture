using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace LightCapture.App.UI;

internal static class DragSurface
{
    internal static bool CanDrag(DependencyObject? source, DependencyObject root)
    {
        // Preview 事件先于按钮执行，必须检查祖先，不能把图标/文字点击误当作窗口拖动。
        for (var element = source; element != null; element = Parent(element))
        {
            if (element is ButtonBase or Selector or RangeBase or TextBoxBase) return false;
            if (ReferenceEquals(element, root)) return true;
        }
        return false;
    }
    private static DependencyObject? Parent(DependencyObject element) => element is Visual or Visual3D
        ? VisualTreeHelper.GetParent(element)
        : element is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(element);
}
