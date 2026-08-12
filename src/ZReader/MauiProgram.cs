using ZReader.Core.Services;
using ZReader.Pages;
using ZReader.Platforms.Android;

namespace ZReader;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton<ITextEncodingDetector, TextEncodingDetector>();
        builder.Services.AddSingleton<IReaderPaginator, ReaderPaginator>();
        builder.Services.AddSingleton<IBookRepository, AndroidBookRepository>();
        builder.Services.AddSingleton<IEncryptedBookStore, AndroidEncryptedBookStore>();
        builder.Services.AddSingleton<BookImportService>();
        builder.Services.AddTransient<ReaderSession>();
        builder.Services.AddTransient<ShelfPage>();
        builder.Services.AddTransient<ReaderPage>();
        return builder.Build();
    }
}
