using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsPro.Models;
using LyricsPro.Services;
using System.Collections.ObjectModel;

namespace LyricsPro.ViewModels;

public partial class LyricsViewModel : ObservableObject
{
    public event Action<LyricEntry>? PresentRequested;

    // ── Library tab ─────────────────────────────────────────────
    [ObservableProperty] private string _librarySearch = string.Empty;
    [ObservableProperty] private LyricEntry? _selectedLyric;
    [ObservableProperty] private bool _isEditing;

    // ── Edit form ────────────────────────────────────────────────
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editArtist = string.Empty;
    [ObservableProperty] private string _editText = string.Empty;
    [ObservableProperty] private string? _editBackgroundPath;

    // ── Internet search tab ──────────────────────────────────────
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _searchStatus = string.Empty;
    [ObservableProperty] private LyricsSearchResult? _selectedResult;
    [ObservableProperty] private string _fetchedTitle = string.Empty;
    [ObservableProperty] private string _fetchedArtist = string.Empty;
    [ObservableProperty] private string _fetchedText = string.Empty;
    [ObservableProperty] private bool _hasFetched;

    public ObservableCollection<LyricEntry> Lyrics { get; } = [];
    public ObservableCollection<LyricsSearchResult> SearchResults { get; } = [];

    public LyricsViewModel() => LoadLibrary();

    public void LoadLibrary()
    {
        Lyrics.Clear();
        var query = LibrarySearch.Trim();
        var entries = string.IsNullOrEmpty(query)
            ? AppServices.Database.GetAllLyrics()
            : AppServices.Database.SearchLyrics(query);
        foreach (var e in entries) Lyrics.Add(e);
    }

    partial void OnLibrarySearchChanged(string value) => LoadLibrary();

    [RelayCommand]
    private void SelectLyric(LyricEntry entry)
    {
        SelectedLyric = entry;
        IsEditing = false;
    }

    [RelayCommand]
    private void PresentSelected()
    {
        if (SelectedLyric is null) return;
        PresentRequested?.Invoke(SelectedLyric);
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (SelectedLyric is null) return;
        EditTitle          = SelectedLyric.Title;
        EditArtist         = SelectedLyric.Artist;
        EditText           = SelectedLyric.LyricText;
        EditBackgroundPath = SelectedLyric.BackgroundImagePath;
        IsEditing = true;
    }

    [RelayCommand]
    private void NewLyric()
    {
        SelectedLyric      = null;
        EditTitle          = string.Empty;
        EditArtist         = string.Empty;
        EditText           = string.Empty;
        EditBackgroundPath = null;
        IsEditing          = true;
    }

    [RelayCommand]
    public void SetBackgroundPath(string? path) => EditBackgroundPath = path;

    [RelayCommand]
    private void SaveEdit()
    {
        var entry = SelectedLyric ?? new LyricEntry();
        entry.Title               = EditTitle;
        entry.Artist              = EditArtist;
        entry.LyricText           = EditText;
        entry.BackgroundImagePath = EditBackgroundPath;
        AppServices.Database.SaveLyric(entry);
        IsEditing = false;
        LoadLibrary();
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedLyric is null) return;
        AppServices.Database.DeleteLyric(SelectedLyric.Id);
        SelectedLyric = null;
        LoadLibrary();
    }

    // ── Internet search ──────────────────────────────────────────

    [RelayCommand]
    private async Task SearchInternetAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;
        IsSearching = true;
        SearchStatus = "Buscando...";
        SearchResults.Clear();
        HasFetched = false;

        try
        {
            var results = await AppServices.LyricsSearch.SearchAsync(SearchQuery);
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var r in results) SearchResults.Add(r);
                SearchStatus = results.Count > 0
                    ? $"{results.Count} resultado(s) encontrado(s)"
                    : "Nenhum resultado encontrado";
            });
        }
        catch
        {
            SearchStatus = "Erro ao conectar. Verifique sua internet.";
        }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private async Task FetchResultAsync(LyricsSearchResult result)
    {
        SelectedResult = result;
        IsSearching = true;
        SearchStatus = "Carregando letra...";

        try
        {
            var (title, artist, lyrics) = await AppServices.LyricsSearch.FetchLyricsAsync(result);
            FetchedTitle = string.IsNullOrWhiteSpace(title) ? result.Title : title;
            FetchedArtist = string.IsNullOrWhiteSpace(artist) ? result.Artist : artist;
            FetchedText = lyrics;
            HasFetched = true;
            SearchStatus = "Letra carregada!";
        }
        catch
        {
            SearchStatus = "Erro ao carregar a letra.";
        }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private void ImportFetched()
    {
        if (!HasFetched) return;
        var entry = new LyricEntry
        {
            Title = FetchedTitle,
            Artist = FetchedArtist,
            LyricText = FetchedText
        };
        AppServices.Database.SaveLyric(entry);
        LoadLibrary();
        SearchStatus = "Letra importada para a biblioteca!";
    }
}
