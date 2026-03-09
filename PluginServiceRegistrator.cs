using Jellyfin.Plugin.ExternalRatings.Providers;
using Jellyfin.Plugin.ExternalRatings.Services;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ExternalRatings;

/// <summary>
/// Registers plugin services with the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpClient("ExternalRatings");

        serviceCollection.AddSingleton<RatingCacheService>();
        serviceCollection.AddSingleton<LetterboxdRatingProvider>();
        serviceCollection.AddSingleton<ImdbRatingProvider>();
        serviceCollection.AddSingleton<ExternalRatingService>();
    }
}
