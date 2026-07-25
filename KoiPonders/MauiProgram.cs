using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;

namespace KoiPonders
{
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
                })
                .UseArcGISRuntime(config => config
                   .UseApiKey("yourkey")
                   .ConfigureAuthentication(auth => auth
                       .UseDefaultChallengeHandler()
                       .UseCredentialPersistence()
                   )
                );

            ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;
            return builder.Build();
        }
    }
}