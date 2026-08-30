namespace LuoTianyiPet.Core;

public enum ReactionPriority
{
    TimeGreeting = 100,
    MediaOrVolume = 200,
    Notification = 300,
    Genshin = 400,
    System = 500,
    UserInteraction = 600,
    Exit = 700,
}

public enum ReactionStartResult
{
    Started,
    Replaced,
    Merged,
    RejectedByPriority,
    SuppressedByCooldown,
    Expired,
}

public enum PlaybackPlanSource
{
    Continuous,
    Reaction,
}

public sealed record ReactionRequest(
    string AnimationId,
    ReactionPriority Priority,
    DateTimeOffset ExpiresAt,
    string? MergeKey = null,
    TimeSpan Cooldown = default,
    bool InterruptibleByDrag = true);

public sealed record ReactionStartOutcome(ReactionStartResult Result, Guid? Token);

public sealed record PetPlaybackPlan(
    bool IsVisible,
    string? AnimationId,
    PlaybackPlanSource Source,
    bool BodyRegionInteractionsEnabled);

public sealed class PetStateMachine
{
    private readonly Dictionary<string, DateTimeOffset> _cooldownEnds = new(StringComparer.Ordinal);
    private ActiveReaction? _activeReaction;
    private PetContinuousState _stateBeforeDrag = PetContinuousState.Idle;
    private DateTimeOffset _bodyInteractionsSuppressedUntil = DateTimeOffset.MinValue;

    public PetStateMachine(PetVisualState? initialState = null)
    {
        VisualState = initialState ?? new PetVisualState();
        if (VisualState.ContinuousState == PetContinuousState.Dragging)
        {
            _stateBeforeDrag = PetContinuousState.Idle;
        }
    }

    public PetVisualState VisualState { get; private set; }

    public void SetDisplayMode(PetDisplayMode mode) =>
        VisualState = VisualState with { SelectedDisplayMode = mode };

    public void SetMusicAnimation(string animationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(animationId);
        VisualState = VisualState with { MusicAnimationId = animationId };
    }

    public void SetContinuousState(PetContinuousState state)
    {
        if (state == PetContinuousState.Dragging)
        {
            throw new ArgumentException("Use BeginDrag to enter the dragging state.", nameof(state));
        }

        if (VisualState.ContinuousState == PetContinuousState.Dragging)
        {
            if (state == PetContinuousState.HiddenForSafety)
            {
                _stateBeforeDrag = state;
                VisualState = VisualState with { ContinuousState = state };
                _activeReaction = null;
                return;
            }

            _stateBeforeDrag = state;
            return;
        }

        VisualState = VisualState with { ContinuousState = state };
    }

    public ReactionStartOutcome TryStartReaction(ReactionRequest request, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AnimationId);
        if (request.Cooldown < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Cooldown cannot be negative.");
        }

        ExpireReaction(now);
        if (request.ExpiresAt <= now)
        {
            return new ReactionStartOutcome(ReactionStartResult.Expired, null);
        }

        if (request.MergeKey is not null &&
            _cooldownEnds.TryGetValue(request.MergeKey, out DateTimeOffset cooldownEnd) &&
            cooldownEnd > now)
        {
            return new ReactionStartOutcome(ReactionStartResult.SuppressedByCooldown, null);
        }

        if (_activeReaction is not null)
        {
            if (request.MergeKey is not null && request.MergeKey == _activeReaction.Request.MergeKey)
            {
                _activeReaction = _activeReaction with { Request = request };
                return new ReactionStartOutcome(ReactionStartResult.Merged, _activeReaction.Token);
            }

            if (request.Priority < _activeReaction.Request.Priority)
            {
                return new ReactionStartOutcome(ReactionStartResult.RejectedByPriority, null);
            }
        }

        ReactionStartResult result = _activeReaction is null
            ? ReactionStartResult.Started
            : ReactionStartResult.Replaced;
        Guid token = Guid.NewGuid();
        _activeReaction = new ActiveReaction(token, request);
        return new ReactionStartOutcome(result, token);
    }

    public bool CompleteReaction(Guid token, DateTimeOffset now)
    {
        ExpireReaction(now);
        if (_activeReaction?.Token != token)
        {
            return false;
        }

        StartCooldown(_activeReaction.Request, now);
        _activeReaction = null;
        return true;
    }

    public void CancelActiveReaction() => _activeReaction = null;

    public void SuppressBodyInteractions(DateTimeOffset now, TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        DateTimeOffset suppressedUntil = now + duration;
        if (suppressedUntil > _bodyInteractionsSuppressedUntil)
        {
            _bodyInteractionsSuppressedUntil = suppressedUntil;
        }
    }

    public bool BeginDrag()
    {
        if (VisualState.ContinuousState == PetContinuousState.HiddenForSafety)
        {
            return false;
        }

        if (_activeReaction is { Request.InterruptibleByDrag: false })
        {
            return false;
        }

        _activeReaction = null;
        if (VisualState.ContinuousState == PetContinuousState.Dragging)
        {
            return true;
        }

        _stateBeforeDrag = VisualState.ContinuousState;
        VisualState = VisualState with { ContinuousState = PetContinuousState.Dragging };
        return true;
    }

    public bool EndDrag()
    {
        if (VisualState.ContinuousState != PetContinuousState.Dragging)
        {
            return false;
        }

        VisualState = VisualState with { ContinuousState = _stateBeforeDrag };
        return true;
    }

    public PetPlaybackPlan Resolve(DateTimeOffset now)
    {
        ExpireReaction(now);
        if (VisualState.ContinuousState == PetContinuousState.HiddenForSafety)
        {
            return new PetPlaybackPlan(false, null, PlaybackPlanSource.Continuous, false);
        }

        if (VisualState.ContinuousState == PetContinuousState.Dragging)
        {
            if (_stateBeforeDrag == PetContinuousState.MusicPlaying)
            {
                return new PetPlaybackPlan(
                    true,
                    VisualState.MusicAnimationId,
                    PlaybackPlanSource.Continuous,
                    false);
            }

            return ContinuousPlan(bodyRegionsEnabled: false);
        }

        if (_activeReaction is not null)
        {
            return new PetPlaybackPlan(
                true,
                _activeReaction.Request.AnimationId,
                PlaybackPlanSource.Reaction,
                false);
        }

        bool bodyRegionsEnabled = VisualState.ContinuousState == PetContinuousState.Idle &&
            VisualState.SelectedDisplayMode == PetDisplayMode.FullBodyInteractive &&
            now >= _bodyInteractionsSuppressedUntil;
        return ContinuousPlan(bodyRegionsEnabled);
    }

    private PetPlaybackPlan ContinuousPlan(bool bodyRegionsEnabled) => new(
        true,
        VisualState.ResolveContinuousAnimation(),
        PlaybackPlanSource.Continuous,
        bodyRegionsEnabled);

    private void ExpireReaction(DateTimeOffset now)
    {
        if (_activeReaction?.Request.ExpiresAt <= now)
        {
            _activeReaction = null;
        }
    }

    private void StartCooldown(ReactionRequest request, DateTimeOffset now)
    {
        if (request.MergeKey is not null && request.Cooldown > TimeSpan.Zero)
        {
            _cooldownEnds[request.MergeKey] = now + request.Cooldown;
        }
    }

    private sealed record ActiveReaction(Guid Token, ReactionRequest Request);
}
