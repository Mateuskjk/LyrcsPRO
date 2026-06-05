using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LibVLCSharp.Shared;

namespace LyricsPro.Views;

public partial class VideoProjectorWindow : Window
{
    private LibVLC?      _vlc;
    private MediaPlayer? _player;
    private DispatcherTimer? _hideTimer;

    public VideoProjectorWindow() { InitializeComponent(); }

    public VideoProjectorWindow(string filePath, string title)
    {
        InitializeComponent();
        _title = title;
        this.FindControl<TextBlock>("TxtTitle")!.Text = title;
        KeyDown    += OnKey;
        PointerMoved += OnPointerMoved;

        Opened += (_, _) => StartVideo(filePath);
        Closed += (_, _) => Cleanup();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideTimer.Tick += (_, _) => HideControls();
        _hideTimer.Start();

        MoveToSecondScreen();
    }

    private string _title = "";

    private void StartVideo(string filePath)
    {
        LibVLCSharp.Shared.Core.Initialize();
        _vlc    = new LibVLC(enableDebugLogs: false);
        _player = new MediaPlayer(_vlc);

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero) _player.Hwnd = handle;

        using var media = new Media(_vlc, new Uri(filePath));
        _player.Media = media;
        _player.Play();

        // Register with MediaStateService so remote server can control it
        var state = Services.AppServices.MediaState;
        state.SetVideo(true, _title);
        state.VideoPlayPause = () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_player.IsPlaying) _player.Pause();
            else _player.Play();
            state.SetVideo(_player.IsPlaying, _title);
        });
        state.VideoStop = () => Avalonia.Threading.Dispatcher.UIThread.Post(Close);

        _player.Playing   += (_, _) => state.SetVideo(true,  _title);
        _player.Paused    += (_, _) => state.SetVideo(false, _title);
        _player.EndReached += (_, _) => { Avalonia.Threading.Dispatcher.UIThread.Post(Close); };

        this.FindControl<Button>("BtnPlayPause")!.Click += (_, _) =>
        {
            if (_player.IsPlaying) _player.Pause(); else _player.Play();
        };
        this.FindControl<Button>("BtnStop")!.Click += (_, _) => Close();
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)  Close();
        if (e.Key == Key.Space)
        {
            if (_player?.IsPlaying == true) _player.Pause();
            else _player?.Play();
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        ShowControls();
        _hideTimer?.Stop();
        _hideTimer?.Start();
    }

    private void ShowControls() =>
        this.FindControl<Border>("ControlsOverlay")!.IsVisible = true;

    private void HideControls() =>
        this.FindControl<Border>("ControlsOverlay")!.IsVisible = false;

    private void Cleanup()
    {
        _hideTimer?.Stop();
        _player?.Stop();
        _player?.Dispose();
        _vlc?.Dispose();
        Services.AppServices.MediaState.SetVideo(false);
    }

    private void MoveToSecondScreen()
    {
        var screens = Screens.All;
        if (screens.Count > 1)
        {
            var secondary = screens.FirstOrDefault(s => !s.IsPrimary) ?? screens[1];
            Position = new Avalonia.PixelPoint(secondary.Bounds.X, secondary.Bounds.Y);
        }
    }
}
