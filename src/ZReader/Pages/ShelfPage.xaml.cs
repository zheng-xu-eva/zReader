using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ZReader.Core.Domain;
using ZReader.Core.Services;

namespace ZReader.Pages;

/// <summary>
/// Displays the local shelf and coordinates TXT file imports.
/// </summary>
public partial class ShelfPage : ContentPage
{
    private readonly IBookRepository _repository;
    private readonly BookImportService _importService;
    private readonly IServiceProvider _services;
    private bool _isBusy;

    public ShelfPage(IBookRepository repository, BookImportService importService, IServiceProvider services)
    {
        InitializeComponent();
        _repository = repository;
        _importService = importService;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadShelfAsync();
    }

    private async Task LoadShelfAsync()
    {
        try
        {
            await _repository.InitializeAsync(CancellationToken.None);
            BooksView.ItemsSource = await _repository.GetShelfAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            await DisplayAlert("书架不可用", exception.Message, "确定");
        }
    }

    private async void OnImportClicked(object? sender, EventArgs eventArgs)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            _isBusy = true;
            StatusLabel.Text = "正在导入...";
            StatusLabel.IsVisible = true;
            var pickedFile = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "选择 TXT 文件",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = ["text/plain", "text/*"]
                })
            });
            if (pickedFile is null)
            {
                return;
            }

            await using var source = await pickedFile.OpenReadAsync();
            using var memory = new MemoryStream();
            await source.CopyToAsync(memory);
            var bytes = memory.ToArray();
            var book = await ImportWithEncodingSelectionAsync(pickedFile.FileName, bytes);
            StatusLabel.Text = $"已导入《{book.Title}》";
            await LoadShelfAsync();
        }
        catch (Exception exception)
        {
            StatusLabel.Text = "导入失败";
            await DisplayAlert("导入失败", exception.Message, "确定");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task<Book> ImportWithEncodingSelectionAsync(string fileName, byte[] bytes)
    {
        try
        {
            return await _importService.ImportAsync(fileName, bytes, null, CancellationToken.None);
        }
        catch (EncodingSelectionRequiredException exception)
        {
            var selected = await DisplayActionSheet("请选择 TXT 编码", "取消", null, exception.AvailableChoices.Select(GetEncodingName).ToArray());
            if (string.IsNullOrWhiteSpace(selected) || selected == "取消")
            {
                throw new OperationCanceledException("未选择文件编码。");
            }

            var choice = exception.AvailableChoices.Single(item => GetEncodingName(item) == selected);
            return await _importService.ImportAsync(fileName, bytes, choice, CancellationToken.None);
        }
    }

    private async void OnBookSelected(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (eventArgs.CurrentSelection.FirstOrDefault() is not Book book)
        {
            return;
        }

        BooksView.SelectedItem = null;
        var readerPage = _services.GetRequiredService<ReaderPage>();
        await Navigation.PushAsync(readerPage);
        await readerPage.OpenBookAsync(book.Id);
    }

    private static string GetEncodingName(TextEncodingChoice choice) => choice switch
    {
        TextEncodingChoice.Gbk => "GBK",
        TextEncodingChoice.Gb18030 => "GB18030",
        TextEncodingChoice.Utf8 => "UTF-8",
        TextEncodingChoice.Utf8Bom => "UTF-8 BOM",
        _ => choice.ToString()
    };
}
