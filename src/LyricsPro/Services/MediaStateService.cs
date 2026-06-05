namespace LyricsPro.Services;

/// <summary>
/// Central hub for what's currently playing — audio, video or image slide.
/// The remote server reads from here to show unified controls on the phone.
/// </summary>
public class MediaStateService
{
    // ── Audio ─────────────────────────────────────────────────────
    public bool   AudioIsPlaying { get; private set; }
    public string AudioTitle     { get; private set; } = "";
    public float  AudioVolume
    {
        get => AppServices.MediaPlayer.Volume;
        set => AppServices.MediaPlayer.Volume = value;
    }

    // ── Video ─────────────────────────────────────────────────────
    public bool   VideoIsPlaying { get; private set; }
    public string VideoTitle     { get; private set; } = "";

    // Delegates wired by VideoProjectorWindow when it opens/closes
    public Action? VideoPlayPause { get; set; }
    public Action? VideoStop      { get; set; }

    // ── Image ─────────────────────────────────────────────────────
    public bool   ImageIsShowing { get; private set; }
    public string ImageTitle     { get; private set; } = "";

    // ── Update methods called by the app ─────────────────────────

    public void SetAudio(bool playing, string title = "")
    {
        AudioIsPlaying = playing;
        if (!string.IsNullOrEmpty(title)) AudioTitle = title;
        if (!playing && string.IsNullOrEmpty(title)) AudioTitle = "";
    }

    public void SetVideo(bool playing, string title = "")
    {
        VideoIsPlaying = playing;
        if (!string.IsNullOrEmpty(title)) VideoTitle = title;
        if (!playing) { VideoTitle = ""; VideoPlayPause = null; VideoStop = null; }
    }

    public void SetImage(bool showing, string title = "")
    {
        ImageIsShowing = showing;
        ImageTitle     = showing ? title : "";
    }

    // ── Controls ──────────────────────────────────────────────────

    public void AudioTogglePlayPause() => AppServices.MediaPlayer.TogglePlayPause();
    public void AudioStopAction()      => AppServices.MediaPlayer.Stop();
    public void AudioVolumeUp()        => AppServices.MediaPlayer.Volume = Math.Min(1f, AppServices.MediaPlayer.Volume + 0.1f);
    public void AudioVolumeDown()      => AppServices.MediaPlayer.Volume = Math.Max(0f, AppServices.MediaPlayer.Volume - 0.1f);

    public bool HasAnything => AudioIsPlaying || VideoIsPlaying || ImageIsShowing;
}
