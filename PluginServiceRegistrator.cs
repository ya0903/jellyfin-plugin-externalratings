using System.Net.Http;
using Jellyfin.Plugin.ExternalRatings.Providers;
using Jellyfin.Plugin.ExternalRatings.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ExternalRatings;

/// <summary>
/// Service locator for plugin dependencies.
/// Instantiated by the Plugin entry point.
/// </summary>
public static class PluginServiceLocator
{
    public static RatingCacheService? CacheService { get; private set; }
    public static ExternalRatingService? RatingService { get; private set; }

    public static void Initialize(
        IApplicationPaths appPaths,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory)
    {
        var httpClientFactory = new DefaultHttpClientFactory();

        CacheService = new RatingCacheService(
            appPaths,
            loggerFactory.CreateLogger<RatingCacheService>());

        var letterboxd = new LetterboxdRatingProvider(
            httpClientFactory,
            loggerFactory.CreateLogger<LetterboxdRatingProvider>());

        var imdb = new ImdbRatingProvider(
            httpClientFactory,
            loggerFactory.CreateLogger<ImdbRatingProvider>());

        RatingService = new ExternalRatingService(
            letterboxd,
            imdb,
            CacheService,
            libraryManager,
            loggerFactory.CreateLogger<ExternalRatingService>());
    }

    private sealed class DefaultHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }
}
