using System.Collections.Immutable;
using LightCapture.Core.Annotations;
using LightCapture.Core.Geometry;

namespace LightCapture.Core.Tests;

public class AnnotationDocumentTests
{
    private static Annotation Step(double x = 10) => new() { Kind = AnnotationKind.Step, Start = new(x, 20), End = new(x, 20) };

    [Fact]
    public void StepsContinueWithoutRenumberingExistingMarkers()
    {
        var document = new AnnotationDocument();
        document.Add(Step()); document.Add(Step()); document.Add(Step());
        document.Delete(document.Items[1].Id);
        document.Add(Step());
        Assert.Equal([1, 3, 4], document.Items.Select(a => a.StepNumber));
    }

    [Fact]
    public void UndoAndBranchRestoreNumberAndDiscardRedo()
    {
        var document = new AnnotationDocument();
        document.Add(Step()); document.Add(Step());
        document.Undo();
        Assert.Equal(2, document.NextStepNumber);
        document.Add(Step(30));
        Assert.False(document.CanRedo);
        Assert.Equal([1, 2], document.Items.Select(a => a.StepNumber));
    }

    [Fact]
    public void MovingAndChangingStyleCanBeUndoneIndependently()
    {
        var document = new AnnotationDocument();
        document.Add(Step());
        var original = document.Items[0];
        document.Update(original.Move(100, 50));
        document.Update(document.Items[0] with { ColorArgb = 0xFF0000FF, Thickness = 6 });
        document.Undo();
        Assert.Equal(new PixelPoint(110, 70), document.Items[0].Start);
        Assert.Equal(original.ColorArgb, document.Items[0].ColorArgb);
        document.Undo();
        Assert.Equal(original, document.Items[0]);
        document.Redo();
        Assert.Equal(1, document.Items[0].StepNumber);
    }

    [Fact]
    public void PenMoveDoesNotMutateUndoSnapshot()
    {
        var document = new AnnotationDocument();
        var pen = new Annotation { Kind = AnnotationKind.Pen, Start = new(1, 2), End = new(5, 8), Points = ImmutableArray.Create(new PixelPoint(1, 2), new PixelPoint(5, 8)) };
        document.Add(pen); document.Update(pen.Move(4, 6)); document.Undo();
        Assert.Equal(new PixelPoint(1, 2), document.Items[0].Points[0]);
    }

    [Fact]
    public void ThinArrowUsesLineHitTestingRatherThanWholeBoundingBox()
    {
        var arrow = new Annotation { Kind = AnnotationKind.Arrow, Start = new(0, 0), End = new(100, 100) };
        Assert.True(arrow.HitTest(new(50, 52)));
        Assert.False(arrow.HitTest(new(95, 5)));
    }

    [Fact]
    public void VirtualDesktopCoordinatesPreserveNegativeMonitorOrigins()
    {
        var selection = PixelRect.FromPoints(new(-1800, 100), new(200, 600));
        var screen = new PixelRect(-1920, 0, 1920, 1080);
        Assert.Equal(new PixelRect(-1800, 100, 1800, 500), selection.Intersect(screen));
    }
}
