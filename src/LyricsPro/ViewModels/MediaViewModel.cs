using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsPro.Models;
using LyricsPro.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace LyricsPro.ViewModels;

public partial class MediaViewModel : ObservableObject
{
    public event Action<MediaEntry>?   RenameRequested;
    public event Action<string>?       ImagePresentRequested;  // filePath to show on projector

    [ObservableProperty] private int         _tabIndex;
    [ObservableProperty] private MediaEntry? _selectedMedia;
    [ObservableProperty] private string      _editTitle      = string.Empty;
    [ObservableProperty] private bool        _isEditingTitle;

    // ── Player state ─────────────────────────────────────────────
    [ObservableProperty] private bool   _isPlaying;
    [ObservableProperty] private string _nowPlaying = string.Empty;
    [ObservableProperty] private float  _volume     = 1f;

    public ObservableCollection<MediaEntry> AudioItems  { get; } = [];
    public ObservableCollection<MediaEntry> VideoItems  { get; } = [];
    public ObservableCollection<MediaEntry> ImageItems  { get; } = [];

    public MediaViewModel()
    {
        LoadAll();
        var svc = AppServices.MediaPlayer;
        svc.PlayingChanged += v => { IsPlaying = v; };
        svc.StatusChanged  += s => { NowPlaying = s; };
    }

    public void LoadAll()
    {
        AudioItems.Clear();
        VideoItems.Clear();
        ImageItems.Clear();
        foreach (var m in AppServices.Database.GetMedia(MediaKind.Audio)) AudioItems.Add(m);
        foreach (var m in AppServices.Database.GetMedia(MediaKind.Video)) VideoItems.Add(m);
        foreach (var m in AppServices.Database.GetMedia(MediaKind.Image)) ImageItems.Add(m);
    }

    // ── Playback commands ─────────────────────────────────────────

    [RelayCommand]
    private void PlayAudio(MediaEntry entry)
    {
        if (!File.Exists(entry.FilePath)) return;
        AppServices.MediaPlayer.PlayAudio(entry.FilePath, entry.Title);
    }

    public event Action<MediaEntry>? VideoProjectorRequested;

    [RelayCommand]
    private void PlayVideo(MediaEntry entry)
    {
        if (!File.Exists(entry.FilePath)) return;
        // Open in built-in video projector window
        VideoProjectorRequested?.Invoke(entry);
        NowPlaying = $"▶ {entry.Title}";
    }

    [RelayCommand]
    private void PresentImage(MediaEntry entry)
    {
        if (!File.Exists(entry.FilePath)) return;
        ImagePresentRequested?.Invoke(entry.FilePath);
    }

    [RelayCommand]
    private void TogglePlayPause() => AppServices.MediaPlayer.TogglePlayPause();

    [RelayCommand]
    private void StopAudio()
    {
        AppServices.MediaPlayer.Stop();
        NowPlaying = string.Empty;
    }

    partial void OnVolumeChanged(float value) => AppServices.MediaPlayer.Volume = value;

    // ── Library management ────────────────────────────────────────

    [RelayCommand]
    private void SelectTab(string indexStr)
    {
        if (int.TryParse(indexStr, out var i)) TabIndex = i;
    }

    [RelayCommand]
    private void SelectMedia(MediaEntry e)
    {
        SelectedMedia  = e;
        EditTitle      = e.Title;
        IsEditingTitle = false;
    }

    [RelayCommand]
    private void StartRenaming(MediaEntry e)
    {
        SelectedMedia = e;
        EditTitle     = e.Title;
        RenameRequested?.Invoke(e);
    }

    public void ApplyRename(MediaEntry entry, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        entry.Title = newName;
        AppServices.Database.SaveMedia(entry);
        LoadAll();
    }

    [RelayCommand]
    private void SaveRename()
    {
        if (SelectedMedia is null) return;
        SelectedMedia.Title = EditTitle;
        AppServices.Database.SaveMedia(SelectedMedia);
        IsEditingTitle = false;
        LoadAll();
    }

    [RelayCommand]
    private void CancelRename() => IsEditingTitle = false;

    [RelayCommand]
    private void DeleteMedia(MediaEntry entry)
    {
        AppServices.Database.DeleteMedia(entry.Id);
        if (SelectedMedia?.Id == entry.Id) SelectedMedia = null;
        LoadAll();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedMedia is null) return;
        AppServices.Database.DeleteMedia(SelectedMedia.Id);
        SelectedMedia = null;
        LoadAll();
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var ext  = Path.GetExtension(path).ToLowerInvariant();
            var kind = ext switch
            {
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" => MediaKind.Audio,
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".m4v"  => MediaKind.Video,
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".gif" => MediaKind.Image,
                _ => (MediaKind?)null
            };
            if (kind is null) continue;
            var info  = new FileInfo(path);
            var entry = new MediaEntry
            {
                Title         = Path.GetFileNameWithoutExtension(path),
                FilePath      = path,
                Kind          = kind.Value,
                FileSizeBytes = info.Exists ? info.Length : 0
            };
            AppServices.Database.SaveMedia(entry);
        }
        LoadAll();
    }
}
