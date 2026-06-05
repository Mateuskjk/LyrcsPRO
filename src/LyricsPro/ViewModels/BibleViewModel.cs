using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsPro.Models;
using LyricsPro.Services;
using System.Collections.ObjectModel;

namespace LyricsPro.ViewModels;

public partial class BibleViewModel : ObservableObject
{
    public event Action<IEnumerable<BibleVerse>, string, int>? PresentRequested;
    /// <summary>Fired when user changes chapter/verse and a presentation is active — jump to that slide.</summary>
    public event Action<int>? JumpToSlideRequested;   // slideIndex (0-based)

    // ── Reading state ──────────────────────────────────────────
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private BibleBook? _selectedBook;
    [ObservableProperty] private int _selectedChapter = 1;
    [ObservableProperty] private int _selectedChapterIndex = 0;  // 0-based for ComboBox
    [ObservableProperty] private int _totalChapters = 1;
    [ObservableProperty] private int _selectedVerseIndex = 0;   // 0-based for ComboBox
    [ObservableProperty] private bool _hasData;

    // ── Download state ─────────────────────────────────────────
    [ObservableProperty] private bool _isVersionPanelOpen = false;  // collapsed by default
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private bool _isCheckingAvailable;
    [ObservableProperty] private string _downloadStatus = string.Empty;
    [ObservableProperty] private int _downloadProgress;
    [ObservableProperty] private int _downloadTotal = 1;
    [ObservableProperty] private BibleTranslation? _selectedAvailable;

    public ObservableCollection<BibleBook>         Books              { get; } = [];
    public ObservableCollection<BibleVerse>        Verses             { get; } = [];
    public ObservableCollection<string>            Chapters           { get; } = [];
    public ObservableCollection<string>            VerseItems         { get; } = [];  // "Ver. 1", "Ver. 2"…
    public ObservableCollection<string>            DownloadedVersions { get; } = [];
    public ObservableCollection<BibleTranslation>  AvailableVersions  { get; } = [];

    public BibleViewModel()
    {
        RefreshDownloadedVersions();
        if (DownloadedVersions.Count > 0)
        {
            Version = DownloadedVersions[0];
            LoadBooksForVersion();
        }
    }

    // ── Version selection ──────────────────────────────────────
    partial void OnVersionChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        HasData = AppServices.Database.BibleHasData(value);
        if (HasData) LoadBooksForVersion();
    }

    private void RefreshDownloadedVersions()
    {
        DownloadedVersions.Clear();
        foreach (var v in AppServices.Database.GetDownloadedVersions())
            DownloadedVersions.Add(v);
        HasData = DownloadedVersions.Count > 0;
    }

    private void LoadBooksForVersion()
    {
        Books.Clear();
        foreach (var b in AppServices.Database.GetBibleBooks()) Books.Add(b);
        if (Books.Count > 0)
        {
            SelectedBook    = Books[0];
            SelectedChapter = 1;
            LoadChapters();
            LoadVerses();
        }
    }

    partial void OnSelectedBookChanged(BibleBook? value)
    {
        SelectedChapter = 1;
        LoadChapters();
        LoadVerses();
    }

    partial void OnSelectedChapterChanged(int value)
    {
        LoadVerses();
        // Keep index in sync (avoid loop: index change → chapter change → index change)
        int expected = value - 1;
        if (SelectedChapterIndex != expected)
            SelectedChapterIndex = expected;
    }

    private void LoadChapters()
    {
        Chapters.Clear();
        if (SelectedBook is null) return;
        TotalChapters = AppServices.Database.GetChapterCount(SelectedBook.Id, Version);
        if (TotalChapters == 0) TotalChapters = SelectedBook.ChapterCount;
        for (int i = 1; i <= TotalChapters; i++) Chapters.Add($"Cap. {i}");
        // Sync index (keep current chapter if valid)
        int idx = Math.Min(SelectedChapter - 1, TotalChapters - 1);
        SelectedChapterIndex = Math.Max(0, idx);
    }

    partial void OnSelectedChapterIndexChanged(int value)
    {
        int ch = value + 1;
        if (ch != SelectedChapter)
        {
            SelectedChapter = ch;
            LoadVerses();
        }
    }

    private void LoadVerses()
    {
        Verses.Clear();
        VerseItems.Clear();
        if (SelectedBook is null || string.IsNullOrEmpty(Version)) return;
        foreach (var v in AppServices.Database.GetVerses(SelectedBook.Id, SelectedChapter, Version))
        {
            Verses.Add(v);
            VerseItems.Add($"Ver. {v.Verse}");
        }
        SelectedVerseIndex = 0;
    }

    partial void OnSelectedVerseIndexChanged(int value)
    {
        // When verse ComboBox changes, jump to that verse in the active presentation
        JumpToSlideRequested?.Invoke(value);
    }

    [RelayCommand] private void PrevVerse()
    {
        if (SelectedVerseIndex > 0) SelectedVerseIndex--;
        else if (SelectedChapter > 1) { SelectedChapter--; SelectedVerseIndex = VerseItems.Count - 1; }
    }

    [RelayCommand] private void NextVerse()
    {
        if (SelectedVerseIndex < VerseItems.Count - 1) SelectedVerseIndex++;
        else if (SelectedChapter < TotalChapters) { SelectedChapter++; SelectedVerseIndex = 0; }
    }

    // ── Navigation ─────────────────────────────────────────────
    [RelayCommand] private void PrevChapter() { if (SelectedChapter > 1) SelectedChapter--; }
    [RelayCommand] private void NextChapter() { if (SelectedChapter < TotalChapters) SelectedChapter++; }


    [RelayCommand]
    private void ToggleVersionPanel() => IsVersionPanelOpen = !IsVersionPanelOpen;

    [RelayCommand]
    private void SelectBook(BibleBook book)
    {
        SelectedBook = book;
    }

    [RelayCommand]
    private void PresentChapter()
    {
        if (SelectedBook is null || Verses.Count == 0) return;
        PresentRequested?.Invoke(Verses.ToList(), SelectedBook.Name, SelectedChapter);
    }

    // ── Check available versions on bolls.life ─────────────────
    [RelayCommand]
    private async Task CheckAvailableAsync()
    {
        IsCheckingAvailable = true;
        DownloadStatus = "Verificando versões disponíveis...";
        AvailableVersions.Clear();

        var progress = new Progress<string>(msg => DownloadStatus = msg);
        try
        {
            var list = await AppServices.BibleDownload.GetAvailableTranslationsAsync(progress);
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var t in list) AvailableVersions.Add(t);
                DownloadStatus = $"{list.Count} versões disponíveis";
                if (list.Count > 0 && SelectedAvailable is null)
                    SelectedAvailable = list[0];
            });
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Erro: {ex.Message}";
        }
        finally { IsCheckingAvailable = false; }
    }

    // ── Download selected version ──────────────────────────────
    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        if (SelectedAvailable is null) return;
        await DownloadTranslationAsync(SelectedAvailable);
    }

    public async Task DownloadTranslationAsync(BibleTranslation translation)
    {
        IsDownloading    = true;
        DownloadProgress = 0;
        DownloadStatus   = $"Baixando {translation.Name}...";

        var progress = new Progress<(int c, int t, string msg)>(p =>
        {
            DownloadProgress = p.c;
            DownloadTotal    = p.t;
            DownloadStatus   = p.msg;
        });

        try
        {
            await AppServices.BibleDownload.DownloadAsync(translation, progress);
            DownloadStatus = $"{translation.Name} baixada com sucesso!";
            Dispatcher.UIThread.Invoke(() =>
            {
                RefreshDownloadedVersions();
                if (string.IsNullOrEmpty(Version) && DownloadedVersions.Count > 0)
                {
                    Version = translation.Code;
                    LoadBooksForVersion();
                }
            });
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Erro: {ex.Message}";
        }
        finally { IsDownloading = false; }
    }

    // ── Delete version ─────────────────────────────────────────
    [RelayCommand]
    private void DeleteVersion(string version)
    {
        AppServices.Database.DeleteBibleVersion(version);
        RefreshDownloadedVersions();
        if (Version == version)
        {
            Version = DownloadedVersions.FirstOrDefault() ?? string.Empty;
            if (!string.IsNullOrEmpty(Version)) LoadBooksForVersion();
            else { Books.Clear(); Verses.Clear(); }
        }
    }

    // ── Manual JSON import ─────────────────────────────────────
    public async Task ImportJsonAsync(string filePath)
    {
        IsDownloading  = true;
        DownloadStatus = "Importando JSON...";
        try
        {
            await AppServices.BibleImport.ImportFromBollsJsonAsync(filePath, Version);
            DownloadStatus = "Importado com sucesso!";
            RefreshDownloadedVersions();
            LoadBooksForVersion();
        }
        catch (Exception ex) { DownloadStatus = $"Erro: {ex.Message}"; }
        finally { IsDownloading = false; }
    }
}
