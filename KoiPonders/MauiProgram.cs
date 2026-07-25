using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;

namespace KoiPonders
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            /* Authentication for ArcGIS location services:
             * Use of ArcGIS location services, including basemaps and geocoding, requires either:
             * 1) User authentication: Automatically generates a unique, short-lived access token when a user signs in to your application with their ArcGIS account
             *    giving your application permission to access the content and location services authorized to an existing ArcGIS user's account.
             *    You'll get an identity by signing into the ArcGIS Portal.
             * 2) API key authentication: Uses a long-lived access token to authenticate requests to location services and private content.
             *    Go to https://links.esri.com/create-an-api-key to learn how to create and manage an API key using API key credentials, and then call 
             *    .UseApiKey("[Your ArcGIS location services API Key]")
             *    in the UseArcGISRuntime call below. */

            /* Licensing:
             * Production deployment of applications built with the ArcGIS Maps SDK requires you to license ArcGIS functionality.
             * For more information see https://links.esri.com/arcgis-runtime-license-and-deploy.
             * You can set the license string by calling .UseLicense(licenseString) in the UseArcGISRuntime call below
             * or retrieve a license dynamically after signing into a portal:
             * ArcGISRuntimeEnvironment.SetLicense(await myArcGISPortal.GetLicenseInfoAsync()); */

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UseArcGISRuntime(config => config
                // .UseLicense("[Your ArcGIS Maps SDK license string]")
                // .UseApiKey("[Your ArcGIS location services API key]")
                   .ConfigureAuthentication(auth => auth
                       .UseDefaultChallengeHandler() // Use the default authentication dialog
                       .UseCredentialPersistence()
                  // .UseOAuthAuthorizeHandler(myOauthAuthorizationHandler) // Configure a custom OAuth dialog

                  )
                );
            // Enable support for TimestampOffset fields, which also changes behavior of Date fields.
            // For more information see https://links.esri.com/DotNetDateTime
            ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;

            return builder.Build();
        }
    }
}
