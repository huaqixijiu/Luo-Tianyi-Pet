using System.IO;
using System.Windows;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunHeheDragChecksAsync(string directory, Action<bool, string> check)
    {
        foreach (int scale in new[] { 100, 150, 200 })
        foreach (string edge in new[] { "left", "right", "top", "bottom" })
        {
            SetDisplayScalePercent(scale, false);
            PlayResolvedContinuousAnimation();
            DesktopRectangle area = GetCurrentWorkArea();
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
            _dragPressScreenPoint = new Point(600, 500);
            BeginWindowDrag();
            check(_stateMachine.IsDraggingHehe && !_classicDragExpansionStarted &&
                _animationPlayer?.CurrentAnimationId == PetVisualState.MediumIdleAnimation,
                $"Hehe {scale}% {edge}: dragging keeps Hehe without expansion");
            DateTimeOffset start = DateTimeOffset.Now;
            for (int index = 0; index < 8; index++)
                MoveWindowWithPointer(new Point(index % 2 == 0 ? 720 : 480, 500),
                    start.AddMilliseconds((index + 1) * 80));
            check(!_classicSpinDanceActive &&
                _animationPlayer?.CurrentAnimationId == PetVisualState.MediumIdleAnimation,
                $"Hehe {scale}% {edge}: rapid dragging keeps Hehe");
            MoveWindowWithPointer(new Point(600, 500), start.AddSeconds(1));
            DesktopRectangle before = GetPetImageDesktopBounds();
            double dx = edge == "right" ? area.Right - before.Right : edge == "left" ? area.Left - before.Left : 0;
            double dy = edge == "bottom" ? area.Bottom - before.Bottom : edge == "top" ? area.Top - before.Top : 0;
            MoveWindowWithPointer(new Point(600 + dx, 500 + dy), start.AddSeconds(2));
            check(_animationPlayer?.CurrentAnimationId == PetVisualState.MediumIdleAnimation,
                $"Hehe {scale}% {edge}: edge contact keeps Hehe");
            if (edge == "right")
                CaptureQuickActionsQa(this, Path.Combine(directory, $"hehe-drag-{scale}.png"));
            EndWindowDrag();
            await Task.Delay(120);
            DesktopRectangle after = GetPetImageDesktopBounds();
            double gap = edge == "right" ? area.Right - after.Right : edge == "left" ? after.Left - area.Left :
                edge == "bottom" ? area.Bottom - after.Bottom : after.Top - area.Top;
            check(Math.Abs(gap) < 1 && _stateMachine.VisualState.ContinuousState == PetContinuousState.MediumIdle &&
                _animationPlayer?.CurrentAnimationId == PetVisualState.MediumIdleAnimation,
                $"Hehe {scale}% {edge}: drop preserves artwork and contact; gap={gap:F2}");
        }
        foreach (EdgeDockSide side in new[] { EdgeDockSide.Left, EdgeDockSide.Right, EdgeDockSide.Bottom })
        {
            DesktopRectangle area = GetCurrentWorkArea();
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
            _dragPressScreenPoint = new Point(600, 500);
            BeginWindowDrag();
            DesktopRectangle intent = GetDragIntentPetDesktopBounds();
            double dx = side == EdgeDockSide.Right ? area.Right - intent.Right + intent.Width * .45 :
                side == EdgeDockSide.Left ? area.Left - intent.Left - intent.Width * .45 : 0;
            double dy = side == EdgeDockSide.Bottom ? area.Bottom - intent.Bottom + intent.Height * .45 : 0;
            MoveWindowWithPointer(new Point(600 + dx, 500 + dy), DateTimeOffset.Now);
            check(_dragEdgeCandidate == side && _animationPlayer?.CurrentAnimationId == PetVisualState.MediumIdleAnimation &&
                PetDirectionTransform.ScaleX == 1 && PetImage.RenderTransform.Value.M11 == 1,
                $"Hehe {side}: overscan keeps artwork and readable text");
            MoveWindowWithPointer(new Point(600, 500), DateTimeOffset.Now.AddSeconds(1));
            check(_dragEdgeCandidate == EdgeDockSide.None && !_classicDragExpansionStarted &&
                _animationPlayer?.CurrentAnimationId == PetVisualState.MediumIdleAnimation,
                $"Hehe {side}: cancelled overscan keeps Hehe");
            EndWindowDrag();
            await Task.Delay(100);
        }
        HandleSingleClick(new PointerPoint(-10, -10));
        check(_stateMachine.VisualState.ContinuousState == PetContinuousState.Idle,
            "Hehe single click still exits to idle");
    }
}
