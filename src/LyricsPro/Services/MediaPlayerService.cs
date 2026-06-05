using LibVLCSharp.Shared;

namespace LyricsPro.Services;

public class MediaPlayerService : IDisposable
{
    private LibVLC?       _vlc;
    private MediaPlayer?  _player;
    private bool          _ready;

    public event Action<string>? StatusChanged;
    public event Action<bool>?   PlayingChanged;   // true = playing

    public bool  IsPlaying   => _player?.IsPlaying ?? false;
    public float Volume
    {
        get => (_player?.Volume ?? 100) / 100f;
        set { if (_player != null) _player.Volume = (int)(value * 100); }
    }

    public string NowPlaying { get; private set; } = "";

    // ── Init (call once before any playback) ─────────────────────

    private void EnsureInit()
    {
        if (_ready) return;
        Core.Initialize();
        _vlc    = new LibVLC(enableDebugLogs: false);
        _player = new MediaPlayer(_vlc);

        _player.Playing  += (_, _) => { PlayingChanged?.Invoke(true);  AppServices.MediaState.SetAudio(true,  NowPlaying); };
        _player.Paused   += (_, _) => { PlayingChanged?.Invoke(false); AppServices.MediaState.SetAudio(false, NowPlaying); };
        _player.Stopped  += (_, _) => { PlayingChanged?.Invoke(false); NowPlaying = ""; StatusChanged?.Invoke(""); AppServices.MediaState.SetAudio(false); };
        _player.EndReached += (_, _) => { NowPlaying = ""; PlayingChanged?.Invoke(false); AppServices.MediaState.SetAudio(false); };
        _ready = true;
    }

    // ── Audio ────────────────────────────────────────────────────

    public void PlayAudio(string filePath, string title)
    {
        EnsureInit();
        _player!.Stop();
        using var media = new Media(_vlc!, new Uri(filePath));
        _player.Media = media;
        _player.Play();
        NowPlaying = title;
        StatusChanged?.Invoke($"♪  {title}");
        AppServices.MediaState.SetAudio(true, title);
    }

    public void Pause()
    {
        if (_player?.CanPause == true) _player.Pause();
    }

    public void Resume()
    {
        if (_player is not null && !_player.IsPlaying)
            _player.Play();
    }

    public void Stop()
    {
        _player?.Stop();
        NowPlaying = "";
        StatusChanged?.Invoke("");
    }

    public void TogglePlayPause()
    {
        if (_player is null) return;
        if (_player.IsPlaying) Pause();
        else Resume();
    }

    // ── Video (audio track only — video display via VideoPlayerWindow) ──

    public void PlayVideo(string filePath, string title, nint windowHandle = 0)
    {
        EnsureInit();
        _player!.Stop();
        using var media = new Media(_vlc!, new Uri(filePath));
        _player.Media = media;
        if (windowHandle != 0)
        {
#if WINDOWS
            _player.Hwnd = windowHandle;
#endif
        }
        _player.Play();
        NowPlaying = title;
        StatusChanged?.Invoke($"▶  {title}");
    }

    public void Dispose()
    {
        _player?.Stop();
        _player?.Dispose();
        _vlc?.Dispose();
    }
}
