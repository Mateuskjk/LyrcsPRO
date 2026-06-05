using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsPro.Models;
using System.Collections.ObjectModel;

namespace LyricsPro.ViewModels;

public partial class ProjectorViewModel : ObservableObject
{
    private int _current = 0;

    [ObservableProperty] private Slide? _currentSlide;
    [ObservableProperty] private bool _isBlank = true;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private double _fontSize = 42;
    [ObservableProperty] private bool _showTitle = true;
    [ObservableProperty] private bool _showProgress = true;
    [ObservableProperty] private string _progressText  = string.Empty;
    [ObservableProperty] private string _remoteUrl       = string.Empty;
    [ObservableProperty] private string _remoteUrlLyrics = string.Empty;
    [ObservableProperty] private string _remoteUrlBible  = string.Empty;
    [ObservableProperty] private bool   _remoteActive;

    // Observable so the slide grid updates reactively
    public ObservableCollection<Slide> AllSlides { get; } = [];

    partial void OnIsActiveChanged(bool value)
    {
        // Reset blank when presentation ends
        if (!value) IsBlank = true;
    }

    public void LoadSlides(List<Slide> slides)
    {
        AllSlides.Clear();
        foreach (var s in slides) AllSlides.Add(s);
        _current = 0;
        IsBlank  = false;
        ShowCurrentSlide();
        StartRemoteAsync();

        // Notify media state if presenting an image
        if (slides.Count == 1 && slides[0].Source == Models.SlideSource.Blank
            && !string.IsNullOrEmpty(slides[0].BackgroundImagePath))
            Services.AppServices.MediaState.SetImage(true, slides[0].Title);
        else
            Services.AppServices.MediaState.SetImage(false);
    }

    private async void StartRemoteAsync()
    {
        var svc = Services.AppServices.RemoteControl;
        svc.UrlChanged += url =>
        {
            RemoteUrl       = url;
            RemoteUrlLyrics = url + "/lyrics";
            RemoteUrlBible  = url + "/bible";
            RemoteActive    = true;
        };
        await svc.StartAsync(this);
    }

    public void ShowCurrentSlide()
    {
        if (AllSlides.Count == 0) { CurrentSlide = null; ProgressText = ""; return; }
        CurrentSlide = AllSlides[_current];
        ProgressText = $"{_current + 1} / {AllSlides.Count}";
    }

    [RelayCommand]
    public void Next()
    {
        if (AllSlides.Count == 0) return;
        _current = Math.Min(_current + 1, AllSlides.Count - 1);
        IsBlank  = false;
        ShowCurrentSlide();
    }

    [RelayCommand]
    public void Previous()
    {
        if (AllSlides.Count == 0) return;
        _current = Math.Max(_current - 1, 0);
        IsBlank  = false;
        ShowCurrentSlide();
    }

    [RelayCommand]
    public void GoToSlide(int index)
    {
        if (index < 0 || index >= AllSlides.Count) return;
        _current = index;
        IsBlank  = false;
        ShowCurrentSlide();
    }

    [RelayCommand]
    public void ToggleBlank() => IsBlank = !IsBlank;

    [RelayCommand]
    public void IncreaseFont() => FontSize = Math.Min(FontSize + 4, 120);

    [RelayCommand]
    public void DecreaseFont() => FontSize = Math.Max(FontSize - 4, 20);

    /// <summary>Close the projector window but keep slides loaded (can reopen).</summary>
    [RelayCommand]
    public void CloseProjector()
    {
        IsActive = false;
        IsBlank  = false; // resume from where we were when reopened
    }

    /// <summary>Reopen the projector window with the current slides.</summary>
    [RelayCommand]
    public void ReopenProjector()
    {
        if (AllSlides.Count == 0) return;
        IsBlank  = false;
        IsActive = true;
    }

    /// <summary>Stop the presentation and clear all slides.</summary>
    [RelayCommand]
    public void Clear()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            AllSlides.Clear();
            _current     = 0;
            CurrentSlide = null;
            ProgressText = "";
            IsBlank      = true;
            IsActive     = false;
            RemoteActive = false;
            RemoteUrl    = "";
            Services.AppServices.RemoteControl.Stop();
        });
    }

    public int CurrentIndex => _current;
    public int SlideCount   => AllSlides.Count;
}
