using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;
public partial class MainWindow
{
    private async Task RunPreserveDragQaAsync()
    {
        if (_persistSettings || _animationCatalog is null || _animationPlayer is null) return;
        string directory=Path.Combine(AppContext.BaseDirectory,"PreserveDragQa"); Directory.CreateDirectory(directory);
        List<string> checks=[];
        void Check(bool ok,string name) { if(!ok)throw new InvalidOperationException(name);checks.Add("PASS "+name); }
        async Task Reset(string style)
        {
            if(_isWindowDragging) EndWindowDrag();
            StopClassicSpinDance(false,"qa.spin.reset"); CancelCrystalLongIdle();
            _timeGreetingPresentationInFlight=false; _stateMachine.CancelActiveReaction();
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            _stateMachine.SetDisplayMode(PetDisplayMode.FullBodyInteractive);
            ApplyAppearancePreferences(_settings.Appearance with { FullBodyStyle=style });
            await Task.Delay(300);
            _idleSceneTimer.Stop(); _timeSceneTimer.Stop(); _musicDetectionTimer.Stop();
            CancelVisualTransition();
            _stateMachine.CancelActiveReaction();
            _stateMachine.SetContinuousState(PetContinuousState.Idle);
            PlayResolvedContinuousAnimation();
            DesktopRectangle work=GetCurrentWorkArea(); Left=work.Left+work.Width/2-Width/2; Top=work.Top+work.Height/2-Height/2;
        }
        void Drag(string name)
        {
            string? id=_animationPlayer.CurrentAnimationId; int frame=_animationPlayer.CurrentFrameIndex;
            Guid? token=_stateMachine.ActiveReactionToken; var state=_stateMachine.CurrentContinuousState;
            double mirror=PetDirectionTransform.ScaleX;
            OnMouseLeftButtonDown(this,new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) { RoutedEvent=MouseLeftButtonDownEvent });
            if(state==PetContinuousState.Sleeping) ApplyIdleScene(TimeSpan.Zero);
            Check(_animationPlayer.CurrentAnimationId==id && _animationPlayer.CurrentFrameIndex==frame,name+": mouse press preserves frame");
            _dragPressScreenPoint=new Point(600,450); BeginWindowDrag();
            Check(_isWindowDragging && _animationPlayer.CurrentAnimationId==id && _animationPlayer.CurrentFrameIndex==frame,name+": drag begins without replay");
            MoveWindowWithPointer(new Point(630,470),DateTimeOffset.Now);
            Check(PetDirectionTransform.ScaleX==mirror && _stateMachine.ActiveReactionToken==token,name+": mirror and reaction token preserved");
            EndWindowDrag(); _pointerGesture.Cancel(); if(IsMouseCaptured)ReleaseMouseCapture();
            Check(_animationPlayer.CurrentAnimationId==id && _animationPlayer.CurrentFrameIndex==frame && _stateMachine.CurrentContinuousState==state,name+": drop preserves frame and state");
        }
        try
        {
            await Task.Delay(500);
            foreach(var variant in new[]{CrystalLongIdleVariant.Sleep,CrystalLongIdleVariant.DuckSit})
            {
                await Reset(AppearanceOptionIds.FullBodyCrystalDress);
                _stateMachine.SetContinuousState(PetContinuousState.Sleeping);
                BeginCrystalLongIdle(variant);
                Drag(variant+" entering");
                HoldCrystalLongIdle(variant);
                await Task.Delay(1800);
                Drag(variant.ToString());
                Check(_crystalLongIdleHolding && CrystalLongIdleDecorationLayer.IsVisible,variant+": pose and dream decoration retained");
                ApplyIdleScene(TimeSpan.Zero);
                Check(_stateMachine.CurrentContinuousState==PetContinuousState.Sleeping,variant+": drag input cannot wake on idle tick");
                CaptureQuickActionsQa(this,Path.Combine(directory,variant+"-drag.png"));
                WakeCrystalLongIdle();
                await Task.Delay(250);
                Drag(variant+" waking");
            }
            await Reset(AppearanceOptionIds.FullBodyClassicCatEars);
            foreach(var state in new[]{PetContinuousState.Sleeping,PetContinuousState.MediumIdleCountdown,PetContinuousState.MediumIdle,PetContinuousState.MusicPlaying})
            {
                _stateMachine.SetContinuousState(state); PlayResolvedContinuousAnimation();
                Drag(state.ToString());
                if(state==PetContinuousState.Sleeping)
                {
                    ApplyIdleScene(TimeSpan.Zero);Check(_stateMachine.CurrentContinuousState==state,"Classic sleep survives idle reset");
                    HandleSingleClick(new PointerPoint(-10,-10));
                    Check(_stateMachine.CurrentContinuousState==PetContinuousState.Idle,"Confirmed click wakes classic sleep");
                }
            }
            foreach(string id in new[]{"resonance-soft-heart","resonance-kiss","twelfth-anniversary-hug","crystal-yawn","startup-afternoon-hurry","resonance-loading-sway","resonance-no-playing","resonance-big-success"})
            {
                await Reset(id.StartsWith("crystal")?AppearanceOptionIds.FullBodyCrystalDress:AppearanceOptionIds.FullBodyClassicCatEars);
                _stateMachine.TryStartReaction(new(id,ReactionPriority.UserInteraction,DateTimeOffset.Now.AddMinutes(1)),DateTimeOffset.Now);
                PlayAnimation(id); ShowAnimationFrame(id,Math.Min(2,_animationCatalog.GetRequired(id).FrameDurationsMilliseconds.Count-1));
                ApplyBodyReactionMirror(id=="resonance-soft-heart");
                _timeGreetingPresentationInFlight=id=="startup-afternoon-hurry";
                Drag(id);
                if(_timeGreetingPresentationInFlight)Check(_stateMachine.ActiveReactionToken is not null,"Greeting survives press and movement");
            }
            await Reset(AppearanceOptionIds.FullBodyClassicCatEars);
            _dragPressScreenPoint=new Point(600,450); BeginWindowDrag();
            Check(_classicDragExpansionStarted && _animationPlayer.CurrentAnimationId==PetVisualState.CompactDraggingAnimation,$"Classic idle still expands (state={_stateMachine.CurrentContinuousState}, animation={_animationPlayer.CurrentAnimationId}, drag={_isWindowDragging})");
            DateTimeOffset now=DateTimeOffset.Now;
            for(int i=0;i<8;i++)MoveWindowWithPointer(new Point(i%2==0?720:480,450),now.AddMilliseconds((i+1)*80));
            Check(_classicSpinDanceActive,"Rapid idle drag still triggers spin");
            EndWindowDrag(); Drag("Existing spin"); Check(_classicSpinDanceActive,"Spin remains active after drop");
            HandleSingleClick(new PointerPoint(-10,-10)); Check(!_classicSpinDanceActive,"Confirmed click still stops spin");
            await Reset(AppearanceOptionIds.FullBodyClassicCatEars);
            bool completed=false; string shortId="startup-afternoon-hurry";
            var reaction=_stateMachine.TryStartReaction(new(shortId,ReactionPriority.TimeGreeting,DateTimeOffset.Now.AddMinutes(1)),DateTimeOffset.Now);
            PlayAnimationRange(shortId,0,2,()=>{completed=true;CompleteReaction(reaction.Token!.Value,false);});
            _dragPressScreenPoint=new Point(600,450); BeginWindowDrag(); await Task.Delay(900);
            Check(completed && _stateMachine.ActiveReactionToken is null,"Natural animation completion runs during drag"); EndWindowDrag();
            await Task.Delay(400);
            foreach(string blocked in new[]{"resonance-cute-bun-request","resonance-give-me"})
            {
                _stateMachine.TryStartReaction(new(blocked,ReactionPriority.UserInteraction,DateTimeOffset.Now.AddMinutes(1),InterruptibleByDrag:false),DateTimeOffset.Now);
                PlayAnimation(blocked); BeginWindowDrag(); Check(!_isWindowDragging && _stateMachine.ActiveReactionToken is not null,blocked+": automatic/drag-drop workflow still blocks pet movement");
                _stateMachine.CancelActiveReaction();
            }
            await Reset(AppearanceOptionIds.FullBodyClassicCatEars); StartClassicSpinDance();
            _dragPressScreenPoint=new Point(600,450); BeginWindowDrag();
            var bounds=GetPetImageDesktopBounds();var area=GetCurrentWorkArea();
            MoveWindowWithPointer(new Point(600+area.Right-bounds.Right+bounds.Width*.5,450),DateTimeOffset.Now); EndWindowDrag();
            Check(_edgeDockSide!=EdgeDockSide.None && !_classicSpinDanceActive,"Explicit overscan still docks a spinning pet");
            _edgeDockAnimationGeneration++; _edgeDockSide=EdgeDockSide.None; _edgeDockRevealed=false; PetImage.Clip=null; SetEdgeMirror(false);
            await Reset(AppearanceOptionIds.FullBodyClassicCatEars);
            _bunSubpixelX=_bunSubpixelY=0; double start=Left;
            for(int i=0;i<60;i++)SetBunWindowPosition(Left+.2,Top);
            Check(Math.Abs(Left-start-12)<1,"Subpixel displacement accumulates instead of vanishing each frame");
            _bunChaseActive=true; _bunReturning=true; _bunReturnPosition=new Point(Left+500,Top);
            _bunLastSafetyCheckAt=DateTimeOffset.Now; _bunMotionStageElapsed=TimeSpan.Zero;
            _bunLastMotionTimestamp=Stopwatch.GetTimestamp()-Stopwatch.Frequency;
            OnBunChaseRendering(null,EventArgs.Empty);
            Check(_bunMotionStageElapsed<=TimeSpan.FromMilliseconds(34),"One-second render stall cannot skip acceleration ahead of motion");
            _bunChaseActive=false;_bunReturning=false;_bunReturnPosition=null;
        }
        catch(Exception error){checks.Add("FAIL "+error);}
        File.WriteAllLines(Path.Combine(directory,"result.txt"),checks);
        Application.Current.Shutdown(checks.Any(x=>x.StartsWith("FAIL"))?1:0);
    }
}
