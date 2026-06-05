using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using LyricsPro.ViewModels;
using System.IO;

namespace LyricsPro.Views;

public partial class ProjectorWindow : Window
{
    private readonly ProjectorViewModel _vm;

    // Parameterless ctor required by Avalonia XAML loader
    public ProjectorWindow() : this(new ProjectorViewModel()) { }

    public ProjectorWindow(ProjectorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.PropertyChanged += OnVmChanged;
        KeyDown += OnKeyDown;

        // Try to open on the second screen
        MoveToSecondScreen();
        UpdateBackground();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectorViewModel.CurrentSlide))
            UpdateBackground();
    }

    private void UpdateBackground()
    {
        var img = this.FindControl<Image>("BgImage");
        if (img is null) return;
        var path = _vm.CurrentSlide?.BackgroundImagePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try { img.Source = new Bitmap(path); }
            catch { img.Source = null; }
        }
        else img.Source = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
            case Key.Space:
            case Key.PageDown:
                _vm.Next(); break;

            case Key.Left:
            case Key.Up:
            case Key.PageUp:
                _vm.Previous(); break;

            case Key.B:
                _vm.ToggleBlank(); break;

            case Key.Escape:
                _vm.IsActive = false;
                Close(); break;

            case Key.OemPlus:
            case Key.Add:
                _vm.FontSize = Math.Min(_vm.FontSize + 4, 120); break;

            case Key.OemMinus:
            case Key.Subtract:
                _vm.FontSize = Math.Max(_vm.FontSize - 4, 20); break;
        }
    }

    private void MoveToSecondScreen()
    {
        var screens = Screens.All;
        if (screens.Count > 1)
        {
            // Pick the screen that isn't the primary
            var secondary = screens.FirstOrDefault(s => !s.IsPrimary) ?? screens[1];
            Position = new PixelPoint(secondary.Bounds.X, secondary.Bounds.Y);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.PropertyChanged -= OnVmChanged;
        // Don't clear slides — just mark inactive so user can reopen
        _vm.IsActive = false;
        base.OnClosed(e);
    }
}
