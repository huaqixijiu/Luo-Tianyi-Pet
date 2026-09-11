namespace LuoTianyiPet.Core;

public sealed class MusicPlaybackIndicatorTracker
{
    public static readonly TimeSpan DefaultSilenceDelay = TimeSpan.FromMilliseconds(750);

    private readonly float _audiblePeakThreshold;
    private readonly TimeSpan _silenceDelay;
    private DateTimeOffset? _silenceStartedAt;
    private bool? _expectedPlaying;
    private DateTimeOffset? _expectationExpiresAt;

    public MusicPlaybackIndicatorTracker(
        float audiblePeakThreshold,
        TimeSpan silenceDelay,
        bool initiallyPlaying = false)
    {
        if (!Numeric.IsFinite(audiblePeakThreshold) || audiblePeakThreshold is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(audiblePeakThreshold));
        }

        if (silenceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(silenceDelay));
        }

        _audiblePeakThreshold = audiblePeakThreshold;
        _silenceDelay = silenceDelay;
        IsPlaying = initiallyPlaying;
    }

    public bool IsPlaying { get; private set; }

    public bool Observe(AudioSessionSnapshot snapshot, DateTimeOffset now)
    {
        Guard.NotNull(snapshot, nameof(snapshot));
        if (!Numeric.IsFinite(snapshot.PeakLevel) || snapshot.PeakLevel is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                "Peak level must be between 0 and 1.");
        }

        if (!snapshot.ProbeSucceeded)
        {
            return false;
        }

        bool audible = snapshot.TargetSessionFound &&
            snapshot.PeakLevel >= _audiblePeakThreshold;
        if (_expectedPlaying is bool expectedPlaying &&
            _expectationExpiresAt is DateTimeOffset expectationExpiresAt)
        {
            bool expectationConfirmed = expectedPlaying == audible;
            if (expectationConfirmed)
            {
                ClearExpectation();
                _silenceStartedAt = null;
                return false;
            }

            if (now < expectationExpiresAt)
            {
                return false;
            }

            ClearExpectation();
            _silenceStartedAt = null;
            return ApplyPlayingState(audible);
        }

        if (audible)
        {
            _silenceStartedAt = null;
            return SetPlaying(true);
        }

        if (!IsPlaying)
        {
            _silenceStartedAt = null;
            return false;
        }

        _silenceStartedAt ??= now;
        if (now - _silenceStartedAt.Value < _silenceDelay)
        {
            return false;
        }

        _silenceStartedAt = null;
        return SetPlaying(false);
    }

    public bool Expect(bool isPlaying, DateTimeOffset now, TimeSpan confirmationWindow)
    {
        if (confirmationWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmationWindow));
        }

        _expectedPlaying = isPlaying;
        _expectationExpiresAt = now + confirmationWindow;
        _silenceStartedAt = null;
        return ApplyPlayingState(isPlaying);
    }

    public bool SetPlaying(bool isPlaying)
    {
        _silenceStartedAt = null;
        ClearExpectation();
        return ApplyPlayingState(isPlaying);
    }

    private bool ApplyPlayingState(bool isPlaying)
    {
        if (IsPlaying == isPlaying)
        {
            return false;
        }

        IsPlaying = isPlaying;
        return true;
    }

    private void ClearExpectation()
    {
        _expectedPlaying = null;
        _expectationExpiresAt = null;
    }
}
