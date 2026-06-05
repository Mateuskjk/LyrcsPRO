using Avalonia.Controls;
using Avalonia.Input;
using LyricsPro.ViewModels;
using LyricsPro.Views;

namespace LyricsPro;

public partial class MainWindow : Window
{
    private ProjectorWindow? _projectorWindow;

    public MainWindow()
    {
        InitializeComponent();

        // DataContext is set in XAML *during* InitializeComponent, so wire immediately after
        WireViewModels();

        var titleBar = this.FindControl<Grid>("TitleBar");
        if (titleBar != null)
            titleBar.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

        KeyDown += OnMainKeyDown;
    }

    private void WireViewModels()
    {
        if (DataContext is not MainViewModel vm) return;

        var searchView  = this.FindControl<SearchView>("SearchView");
        var lyricsView  = this.FindControl<LyricsView>("LyricsView");
        var mediaView   = this.FindControl<MediaView>("MediaView");
        var bibleView   = this.FindControl<BibleView>("BibleView");
        var liveView    = this.FindControl<PresenterControlView>("LiveView");

        if (searchView  != null) searchView.DataContext  = vm.LyricsVM;
        if (lyricsView  != null) lyricsView.DataContext  = vm.LyricsVM;
        if (mediaView   != null) mediaView.DataContext   = vm.MediaVM;

        vm.MediaVM.VideoProjectorRequested += entry =>
        {
            var win = new Views.VideoProjectorWindow(entry.FilePath, entry.Title);
            win.Show();
        };
        if (bibleView   != null) bibleView.DataContext   = vm.BibleVM;
        if (liveView    != null) liveView.DataContext    = vm.ProjectorVM;

        vm.ProjectorVM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ProjectorViewModel.IsActive)) return;
            if (vm.ProjectorVM.IsActive) OpenProjector(vm.ProjectorVM);
            else CloseProjector();
        };
    }

    private void OpenProjector(ProjectorViewModel vm)
    {
        if (_projectorWindow is { IsVisible: true }) return;
        _projectorWindow = new ProjectorWindow(vm);
        _projectorWindow.Show();
        _projectorWindow.Closed += (_, _) => _projectorWindow = null;
    }

    private void CloseProjector()
    {
        _projectorWindow?.Close();
        _projectorWindow = null;
    }

    private void OnMainKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel { ProjectorVM.IsActive: true } vm)
        {
            switch (e.Key)
            {
                case Key.Right: case Key.Down:  vm.ProjectorVM.Next();        break;
                case Key.Left:  case Key.Up:    vm.ProjectorVM.Previous();    break;
                case Key.B:                     vm.ProjectorVM.ToggleBlank(); break;
            }
        }
    }

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseProjector();
        Close();
    }
}
