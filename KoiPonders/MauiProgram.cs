using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;
using System.Text.Json;

namespace KoiPonders
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var assembly = typeof(MauiProgram).Assembly;
            using var stream = assembly.GetManifestResourceStream("KoiPonders.ArcGISSettings.local.json")
                ?? throw new InvalidOperationException(
                    "Create ArcGISSettings.local.json and add your ArcGIS API key.");
            var settings = JsonSerializer.Deserialize<ArcGISSettings>(stream)
                ?? throw new InvalidOperationException("ArcGISSettings.local.json is invalid.");

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UseArcGISRuntime(config => config
                   .UseApiKey(settings.ArcGISApiKey)
                   .ConfigureAuthentication(auth => auth
                       .UseDefaultChallengeHandler()
                       .UseCredentialPersistence()
                   )
                );

            ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;
            return builder.Build();
        }

        private sealed record ArcGISSettings(string ArcGISApiKey);
    }
}