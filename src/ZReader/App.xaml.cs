using Microsoft.Extensions.DependencyInjection;
using ZReader.Pages;

namespace ZReader;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();
        MainPage = new NavigationPage(services.GetRequiredService<ShelfPage>());
    }
}
