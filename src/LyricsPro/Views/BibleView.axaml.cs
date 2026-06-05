using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LyricsPro.ViewModels;

namespace LyricsPro.Views;

public partial class BibleView : UserControl
{
    public BibleView()
    {
        InitializeComponent();
        var btn = this.FindControl<Button>("BtnImportBible");
        if (btn != null) btn.Click += OnImportBibleClick;
    }

    private async void OnImportBibleClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || DataContext is not BibleViewModel vm) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar JSON da Bíblia",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });

        if (files.Count > 0)
            await vm.ImportJsonAsync(files[0].Path.LocalPath);
    }
}
