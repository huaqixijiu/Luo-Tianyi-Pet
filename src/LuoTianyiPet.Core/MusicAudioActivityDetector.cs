namespace LuoTianyiPet.Core;

public enum MusicActivityTransition
{
    None,
    Started,
    Stopped,
}

public sealed class MusicAudioActivityDetector
{
    private readonly float _audiblePeakThreshold;
    private readonly TimeSpan _silenceGracePeriod;
    private DateTimeOffset? _lastAudibleAt;

    public MusicAudioActivityDetector(float audiblePeakThreshold, TimeSpan silenceGracePeriod)
    {
        if (!float.IsFinite(audiblePeakThreshold) || audiblePeakThreshold is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(audiblePeakThreshold));
        }

        if (silenceGracePeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(silenceGracePeriod));
        }

        _audiblePeakThreshold = audiblePeakThreshold;
        _silenceGracePeriod = silenceGracePeriod;
    }

    public bool IsPlaying { get; private set; }

    public MusicActivityTransition Update(AudioSessionSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!float.IsFinite(snapshot.PeakLevel) || snapshot.PeakLevel is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Peak level must be between 0 and 1.");
        }

        if (!snapshot.ProbeSucceeded)
        {
            return MusicActivityTransition.None;
        }

        if (!snapshot.TargetSessionFound)
        {
            _lastAudibleAt = null;
            return SetPlaying(false);
        }

        if (snapshot.PeakLevel >= _audiblePeakThreshold)
        {
            _lastAudibleAt = now;
            return SetPlaying(true);
        }

        if (IsPlaying &&
            _lastAudibleAt is DateTimeOffset lastAudibleAt &&
            now - lastAudibleAt >= _silenceGracePeriod)
        {
            _lastAudibleAt = null;
            return SetPlaying(false);
        }

        return MusicActivityTransition.None;
    }

    public MusicActivityTransition ConfirmStoppedAfterUserPause(AudioSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!float.IsFinite(snapshot.PeakLevel) || snapshot.PeakLevel is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Peak level must be between 0 and 1.");
        }

        if (!snapshot.ProbeSucceeded || !IsPlaying ||
            (snapshot.TargetSessionFound && snapshot.PeakLevel >= _audiblePeakThreshold))
        {
            return MusicActivityTransition.None;
        }

        _lastAudibleAt = null;
        return SetPlaying(false);
    }

    public void Reset()
    {
        IsPlaying = false;
        _lastAudibleAt = null;
    }

    private MusicActivityTransition SetPlaying(bool isPlaying)
    {
        if (IsPlaying == isPlaying)
        {
            return MusicActivityTransition.None;
        }

        IsPlaying = isPlaying;
        return isPlaying ? MusicActivityTransition.Started : MusicActivityTransition.Stopped;
    }
}
