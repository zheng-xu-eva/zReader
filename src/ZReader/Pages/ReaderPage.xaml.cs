using ZReader.Core.Domain;
using ZReader.Core.Services;

namespace ZReader.Pages;

/// <summary>
/// Renders a local book and maps touch input to the persisted reader session.
/// </summary>
public partial class ReaderPage : ContentPage
{
    private readonly ReaderSession _session;
    private bool _loaded;
    private bool _suppressControls;

    public ReaderPage(ReaderSession session)
    {
        InitializeComponent();
        _session = session;
    }

    public async Task OpenBookAsync(long bookId)
    {
        await _session.LoadAsync(bookId, GetPageCapacity(), CancellationToken.None);
        _loaded = true;
        FontSizeSlider.Value = _session.FontSize;
        LineSpacingSlider.Value = _session.LineSpacing;
        Render();
    }

    private async void OnReadingSurfaceTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (!_loaded || eventArgs.GetPosition(ReadingSurface) is not Point point)
        {
            return;
        }

        if (point.X < ReadingSurface.Width / 2)
        {
            await MovePreviousAsync();
        }
        else
        {
            await MoveNextAsync();
        }
    }

    private async void OnNextPageSwiped(object? sender, SwipedEventArgs eventArgs) => await MoveNextAsync();

    private async void OnPreviousPageSwiped(object? sender, SwipedEventArgs eventArgs) => await MovePreviousAsync();

    private async Task MoveNextAsync()
    {
        if (!_loaded) return;
        await _session.NextPageAsync(CancellationToken.None);
        Render();
    }

    private async Task MovePreviousAsync()
    {
        if (!_loaded) return;
        await _session.PreviousPageAsync(CancellationToken.None);
        Render();
    }

    private async void OnProgressChanged(object? sender, ValueChangedEventArgs eventArgs)
    {
        if (!_loaded || _suppressControls) return;
        await _session.SeekAsync(eventArgs.NewValue, CancellationToken.None);
        Render();
    }

    private async void OnFontSizeChanged(object? sender, ValueChangedEventArgs eventArgs)
    {
        if (!_loaded || _suppressControls) return;
        await SaveSettingsAsync(eventArgs.NewValue, LineSpacingSlider.Value, _session.Theme);
    }

    private async void OnLineSpacingChanged(object? sender, ValueChangedEventArgs eventArgs)
    {
        if (!_loaded || _suppressControls) return;
        await SaveSettingsAsync(FontSizeSlider.Value, eventArgs.NewValue, _session.Theme);
    }

    private async void OnLightThemeClicked(object? sender, EventArgs eventArgs) => await SaveSettingsAsync(FontSizeSlider.Value, LineSpacingSlider.Value, ReaderTheme.Light);

    private async void OnDarkThemeClicked(object? sender, EventArgs eventArgs) => await SaveSettingsAsync(FontSizeSlider.Value, LineSpacingSlider.Value, ReaderTheme.Dark);

    private async Task SaveSettingsAsync(double fontSize, double lineSpacing, ReaderTheme theme)
    {
        if (!_loaded) return;
        await _session.SaveSettingsAsync(fontSize, lineSpacing, theme, CancellationToken.None);
        _session.UpdatePageCapacity(GetPageCapacity());
        Render();
    }

    private void OnSettingsClicked(object? sender, EventArgs eventArgs)
    {
        SettingsPanel.IsVisible = !SettingsPanel.IsVisible;
    }

    private void OnReadingSurfaceSizeChanged(object? sender, EventArgs eventArgs)
    {
        if (!_loaded) return;
        _session.UpdatePageCapacity(GetPageCapacity());
        Render();
    }

    private void Render()
    {
        _suppressControls = true;
        PageLabel.Text = _session.PageText;
        PageLabel.FontSize = _session.FontSize;
        PageLabel.LineHeight = _session.LineSpacing;
        ProgressSlider.Value = _session.Progress;
        ProgressLabel.Text = $"{_session.Progress:P0}";
        ApplyTheme(_session.Theme);
        _suppressControls = false;
    }

    private void ApplyTheme(ReaderTheme theme)
    {
        var isDark = theme == ReaderTheme.Dark;
        RootGrid.BackgroundColor = Color.FromArgb(isDark ? "#1B1C1A" : "#FFFCF5");
        PageLabel.TextColor = Color.FromArgb(isDark ? "#E8E4DE" : "#1C1B1A");
        ProgressLabel.TextColor = Color.FromArgb(isDark ? "#CFC9C2" : "#68645F");
    }

    private int GetPageCapacity()
    {
        var width = Math.Max(ReadingSurface.Width, 280);
        var height = Math.Max(ReadingSurface.Height, 420);
        var fontSize = _loaded ? _session.FontSize : 18;
        var lineSpacing = _loaded ? _session.LineSpacing : 1.6;
        var columns = Math.Max(8, (int)(width / Math.Max(fontSize, 1)));
        var rows = Math.Max(4, (int)(height / Math.Max(fontSize * lineSpacing, 1)));
        return columns * rows;
    }
}
