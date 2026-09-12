using System.Windows;
using System.Windows.Input;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.App;

public partial class PetQuickPanel : Window
{
    public Action<bool>? OpenPlanner { get; set; }
    private void OnCalendarClick(object sender, RoutedEventArgs e) { Hide(); OpenPlanner?.Invoke(false); }
    private void OnAlarmClick(object sender, RoutedEventArgs e) { Hide(); OpenPlanner?.Invoke(true); }
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<bool> _setLocked;
    private readonly Action<bool> _setTopmost;
    private readonly Action<bool> _setMusicIslands;
    private readonly Action<int> _setScale;

    public PetQuickPanel(Func<AppSettings> getSettings, Action<bool> setLocked,
        Action<bool> setTopmost, Action<bool> setMusicIslands, Action<int> setScale)
    {
        _getSettings = getSettings;
        _setLocked = setLocked;
        _setTopmost = setTopmost;
        _setMusicIslands = setMusicIslands;
        _setScale = setScale;
        InitializeComponent();
    }

    public void RefreshState()
    {
        AppSettings settings = _getSettings();
        LockPositionCheckBox.IsChecked = settings.Window.LockPosition;
        TopmostCheckBox.IsChecked = settings.Window.AlwaysOnTop;
        MusicIslandsCheckBox.IsChecked = settings.Media.ShowMusicIslands;
        int scale = settings.Appearance.DisplayScalePercent;
        ScaleValueText.Text = $"{scale}%";
        DecreaseScaleButton.IsEnabled = scale > AppearancePreferences.MinimumDisplayScalePercent;
        IncreaseScaleButton.IsEnabled = scale < AppearancePreferences.MaximumDisplayScalePercent;
    }

    public void ShowNearPet(DesktopRectangle pet, DesktopRectangle workArea)
    {
        RefreshState();
        Opacity = 0;
        Show();
        UpdateLayout();
        double preferredLeft = pet.Right + 6;
        if (preferredLeft + ActualWidth > workArea.Right)
        {
            preferredLeft = pet.Left - ActualWidth - 6;
        }
        Left = Numeric.Clamp(preferredLeft, workArea.Left, Math.Max(workArea.Left, workArea.Right - ActualWidth));
        Top = Numeric.Clamp(pet.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - ActualHeight));
        Opacity = 1;
        Activate();
        LockPositionCheckBox.Focus();
    }

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        _setLocked(LockPositionCheckBox.IsChecked == true);
        RefreshState();
    }

    private void OnTopmostClick(object sender, RoutedEventArgs e)
    {
        _setTopmost(TopmostCheckBox.IsChecked == true);
        RefreshState();
    }

    private void OnMusicClick(object sender, RoutedEventArgs e)
    {
        bool visible = MusicIslandsCheckBox.IsChecked == true;
        Hide();
        _setMusicIslands(visible);
        RefreshState();
    }

    private void OnDecreaseClick(object sender, RoutedEventArgs e) => ChangeScale(-5);
    private void OnIncreaseClick(object sender, RoutedEventArgs e) => ChangeScale(5);
    private void ChangeScale(int delta)
    {
        _setScale(_getSettings().Appearance.DisplayScalePercent + delta);
        RefreshState();
    }
    private void OnDeactivated(object? sender, EventArgs e) => Hide();
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }
}
