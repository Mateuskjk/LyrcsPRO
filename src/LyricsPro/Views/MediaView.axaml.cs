using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LyricsPro.Models;
using LyricsPro.ViewModels;

namespace LyricsPro.Views;

public partial class MediaView : UserControl
{
    private int _tab = 0;

    public MediaView()
    {
        InitializeComponent();

        this.FindControl<Button>("BtnAudio")!.Click  += (_, _) => SwitchTab(0);
        this.FindControl<Button>("BtnVideo")!.Click  += (_, _) => SwitchTab(1);
        this.FindControl<Button>("BtnImages")!.Click += (_, _) => SwitchTab(2);
        this.FindControl<Button>("BtnImport")!.Click += OnImportClick;

        SwitchTab(0);

        DataContextChanged += (_, _) => WireRenameCommand();
    }

    private void WireRenameCommand()
    {
        if (DataContext is not MediaViewModel vm) return;
        // Subscribe once; StaleHandlers don't matter since DC won't change again
        vm.RenameRequested += OnRenameRequested;
    }

    private async void OnRenameRequested(MediaEntry entry)
    {
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel is null || DataContext is not MediaViewModel vm) return;

        // Simple input dialog
        string? newName = await ShowRenameDialogAsync(topLevel, entry.Title);
        if (newName is null) return; // cancelled
        vm.ApplyRename(entry, newName);
    }

    private static async Task<string?> ShowRenameDialogAsync(Window owner, string current)
    {
        var tcs = new TaskCompletionSource<string?>();

        var tb = new TextBox
        {
            Text = current,
            SelectionStart  = 0,
            SelectionEnd    = current.Length,
            Background      = Avalonia.Media.Brush.Parse("#2A2A2A"),
            Foreground      = Avalonia.Media.Brush.Parse("#F0F0F0"),
            BorderBrush     = Avalonia.Media.Brush.Parse("#D4610A"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius    = new Avalonia.CornerRadius(6),
            FontSize        = 14,
            Margin          = new Avalonia.Thickness(16, 16, 16, 8)
        };

        var btnOk = new Button
        {
            Content         = "Salvar",
            Background      = Avalonia.Media.Brush.Parse("#D4610A"),
            Foreground      = Avalonia.Media.Brush.Parse("White"),
            Padding         = new Avalonia.Thickness(20, 9),
            CornerRadius    = new Avalonia.CornerRadius(6),
            FontWeight      = Avalonia.Media.FontWeight.SemiBold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin          = new Avalonia.Thickness(16, 0, 16, 16)
        };
        btnOk.Click += (_, _) => { tcs.TrySetResult(tb.Text?.Trim()); };
        tb.KeyDown  += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Return) { tcs.TrySetResult(tb.Text?.Trim()); }
            if (e.Key == Avalonia.Input.Key.Escape) { tcs.TrySetResult(null); }
        };

        var dialog = new Window
        {
            Title             = "Renomear",
            Width             = 420,
            SizeToContent     = SizeToContent.Height,
            CanResize         = false,
            SystemDecorations = Avalonia.Controls.SystemDecorations.Full,
            Background        = Avalonia.Media.Brush.Parse("#1A1A1A"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content           = new StackPanel { Children = { tb, btnOk } }
        };

        dialog.Closed += (_, _) => tcs.TrySetResult(null);
        dialog.Opened += (_, _) => { tb.Focus(); tb.SelectAll(); };

        // Must await on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            await dialog.ShowDialog(owner);
        });

        return await tcs.Task;
    }

    private void SwitchTab(int tab)
    {
        _tab = tab;
        this.FindControl<Grid>("PanelAudio")!.IsVisible  = tab == 0;
        this.FindControl<Grid>("PanelVideo")!.IsVisible  = tab == 1;
        this.FindControl<Grid>("PanelImages")!.IsVisible = tab == 2;

        SetActive(this.FindControl<Button>("BtnAudio")!,  tab == 0);
        SetActive(this.FindControl<Button>("BtnVideo")!,  tab == 1);
        SetActive(this.FindControl<Button>("BtnImages")!, tab == 2);
    }

    private static void SetActive(Button btn, bool active)
    {
        btn.Background = Avalonia.Media.Brush.Parse(active ? "#2A1608" : "#1E1E1E");
        btn.Foreground = Avalonia.Media.Brush.Parse(active ? "#E87820" : "#F0F0F0");
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || DataContext is not MediaViewModel vm) return;

        var filters = _tab switch
        {
            0 => new[] { new FilePickerFileType("Áudio") { Patterns = ["*.mp3","*.wav","*.flac","*.aac","*.ogg","*.m4a"] } },
            1 => new[] { new FilePickerFileType("Vídeo") { Patterns = ["*.mp4","*.mkv","*.avi","*.mov","*.wmv","*.m4v"] } },
            2 => new[] { new FilePickerFileType("Imagem") { Patterns = ["*.jpg","*.jpeg","*.png","*.bmp","*.webp","*.gif"] } },
            _ => Array.Empty<FilePickerFileType>()
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar arquivos",
            AllowMultiple = true,
            FileTypeFilter = filters
        });

        if (files.Count > 0)
            vm.AddFiles(files.Select(f => f.Path.LocalPath));
    }
}
