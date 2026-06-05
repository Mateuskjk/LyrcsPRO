using Avalonia.Controls;
using Avalonia.Input;
using LyricsPro.ViewModels;

namespace LyricsPro.Views;

public partial class PresenterControlView : UserControl
{
    public PresenterControlView()
    {
        InitializeComponent();

        // Wire small font-size buttons by name after load
        AttachedToVisualTree += (_, _) => WireFontButtons();
        KeyDown += OnKeyDown;
    }

    private void WireFontButtons()
    {
        // Font controls are in a StackPanel — find by traversal
        // Simpler: handle in ViewModel via RelayCommand later
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ProjectorViewModel vm) return;
        switch (e.Key)
        {
            case Key.Right: case Key.Down: case Key.Space: vm.Next(); break;
            case Key.Left: case Key.Up: vm.Previous(); break;
            case Key.B: vm.ToggleBlank(); break;
        }
    }
}
