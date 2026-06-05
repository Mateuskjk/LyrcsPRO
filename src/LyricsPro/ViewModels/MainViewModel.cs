using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsPro.Models;
using LyricsPro.Services;
using System.Collections.ObjectModel;

namespace LyricsPro.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private NavSection _activeSection = NavSection.Home;

    public LyricsViewModel  LyricsVM   { get; } = new();
    public MediaViewModel   MediaVM    { get; } = new();
    public BibleViewModel   BibleVM    { get; } = new();
    public ProjectorViewModel ProjectorVM { get; } = new();

    public ObservableCollection<LyricEntry> RecentLyrics { get; } = [];

    public MainViewModel()
    {
        LoadHome();

        LyricsVM.PresentRequested        += OnPresentLyric;
        BibleVM.PresentRequested         += OnPresentBible;
        BibleVM.JumpToSlideRequested     += idx => ProjectorVM.GoToSlide(idx);
        MediaVM.ImagePresentRequested    += OnPresentImage;

        // Remote server events
        var remote = Services.AppServices.RemoteControl;
        remote.PresentLyricRequested += OnPresentLyric;
        remote.PresentBibleRequested += OnPresentBible;
    }

    public void LoadHome()
    {
        RecentLyrics.Clear();
        foreach (var e in AppServices.Database.GetAllLyrics().Take(8))
            RecentLyrics.Add(e);
    }

    [RelayCommand]
    private void Navigate(NavSection section)
    {
        ActiveSection = section;
        if (section == NavSection.Home)   LoadHome();
        if (section == NavSection.Lyrics) LyricsVM.LoadLibrary();
        if (section == NavSection.Media)  MediaVM.LoadAll();
    }

    [RelayCommand]
    private void OpenLyric(LyricEntry entry)
    {
        ActiveSection = NavSection.Lyrics;
        LyricsVM.LoadLibrary();
        LyricsVM.SelectLyricCommand.Execute(entry);
    }

    // ── Presentation ────────────────────────────────────────────

    private void OnPresentLyric(LyricEntry lyric)
    {
        var slides = SlideEngine.FromLyric(lyric);
        ProjectorVM.LoadSlides(slides);
        ActiveSection = NavSection.Live;
        ProjectorVM.IsActive = true;
    }

    private void OnPresentImage(string filePath)
    {
        var slide = new Models.Slide(
            Text: "",
            Title: System.IO.Path.GetFileNameWithoutExtension(filePath),
            Subtitle: "",
            BackgroundImagePath: filePath,
            Index: 0, Total: 1,
            Source: Models.SlideSource.Blank);
        ProjectorVM.LoadSlides([slide]);
        ActiveSection = NavSection.Live;
        ProjectorVM.IsActive = true;
    }

    private void OnPresentBible(IEnumerable<BibleVerse> verses, string bookName, int chapter)
    {
        var slides = SlideEngine.FromBibleVerses(verses, bookName, chapter);
        ProjectorVM.LoadSlides(slides);
        ActiveSection = NavSection.Live;
        ProjectorVM.IsActive = true;
    }
}

public enum NavSection { Home, Search, Live, Lyrics, Media, Bible }
