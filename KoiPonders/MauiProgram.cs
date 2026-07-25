using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;
using KoiPonders.Services;
using KoiPonders.ViewModels;
using KoiPonders.Views;

namespace KoiPonders
{
    public static class MauiProgram
    {
        /// <summary>
        /// The built application's service provider, exposed so pages created outside of DI
        /// (such as <see cref="MainPage"/>, which is instantiated by a Shell DataTemplate) can
        /// resolve shared services like <see cref="IReportStore"/>.
        /// </summary>
        public static IServiceProvider Services { get; private set; } = default!;

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UseArcGISRuntime(config => config
                   .UseApiKey("yourkey")
                   .ConfigureAuthentication(auth => auth
                       .UseDefaultChallengeHandler()
                       .UseCredentialPersistence()
                   )
                );

            ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;

            // Reporting feature (ported from the kyle branch): a JSON-backed report store
            // plus the map-located report form.
            builder.Services.AddSingleton<IReportStore, JsonReportStore>();
            builder.Services.AddTransient<ReportEditViewModel>();
            builder.Services.AddTransient<ReportEditPage>();

            var app = builder.Build();
            Services = app.Services;
            return app;
        }
    }
}