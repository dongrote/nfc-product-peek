using Microsoft.Extensions.Logging;
using PCSC;
using PCSC.Monitoring;
using TumblerTags.ViewModel;

namespace TumblerTags;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
		builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<IContextFactory>(ContextFactory.Instance);
        builder.Services.AddSingleton<IMonitorFactory>(MonitorFactory.Instance);
        builder.Services.AddSingleton<MainPageViewModel>();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}