using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LyricsPro.ViewModels;

namespace LyricsPro.Views;

public partial class LyricsView : UserControl
{
    public LyricsView()
    {
        InitializeComponent();
        var btn = this.FindControl<Button>("BtnPickBg");
        if (btn != null) btn.Click += OnPickBackground;
    }

    private async void OnPickBackground(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || DataContext is not LyricsViewModel vm) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar imagem de fundo",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Imagem")
            {
                Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp"]
            }]
        });

        if (files.Count > 0)
            vm.SetBackgroundPath(files[0].Path.LocalPath);
    }
}
