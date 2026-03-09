using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ExternalRatings.Models;
using Jellyfin.Plugin.ExternalRatings.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ExternalRatings.Services;

/// <summary>
/// Orchestrates fetching external ratings from the appropriate provider.
/// </summary>
public class ExternalRatingService
{
    private readonly LetterboxdRatingProvider _letterboxdProvider;
    private readonly ImdbRatingProvider _imdbProvider;
    private readonly RatingCacheService _cache;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ExternalRatingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalRatingService"/> class.
    /// </summary>
    public ExternalRatingService(
        LetterboxdRatingProvider letterboxdProvider,
        ImdbRatingProvider imdbProvider,
        RatingCacheService cache,
        ILibraryManager libraryManager,
        ILogger<ExternalRatingService> logger)
    {
        _letterboxdProvider = letterboxdProvider;
        _imdbProvider = imdbProvider;
        _cache = cache;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets external ratings for a Jellyfin item by ID.
    /// Returns cached results if available, otherwise fetches fresh.
    /// </summary>
    public async Task<IEnumerable<RatingCacheEntry>> GetRatingsAsync(Guid itemId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found", itemId);
            return Enumerable.Empty<RatingCacheEntry>();
        }

        if (!forceRefresh)
        {
            var cached = _cache.GetAllForItem(itemId).ToList();
            if (cached.Count > 0)
                return cached;
        }

        return await FetchAndCacheAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IEnumerable<RatingCacheEntry>> FetchAndCacheAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var results = new List<RatingCacheEntry>();

        var imdbId = item.GetProviderId("Imdb");
        var title = item.Name;
        var year = item.ProductionYear;

        // Movie -> Letterboxd
        if (item is Movie && config.EnableLetterboxdRatings)
        {
            _logger.LogInformation("Fetching Letterboxd rating for movie '{Title}' ({Year})", title, year);
            var result = await _letterboxdProvider.GetRatingAsync(title, year, imdbId, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                await _cache.SetAsync(item.Id, result, cancellationToken).ConfigureAwait(false);
                results.Add(new RatingCacheEntry
                {
                    ItemId = item.Id,
                    Source = result.Source,
                    Rating = result.Rating,
                    VoteCount = result.VoteCount,
                    Url = result.Url,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("Letterboxd fetch failed for '{Title}': {Error}", title, result.ErrorMessage);
            }
        }

        // TV Series -> IMDB via OMDB
        if (item is Series && config.EnableImdbTvRatings)
        {
            _logger.LogInformation("Fetching IMDB rating for series '{Title}' ({Year})", title, year);
            var result = await _imdbProvider.GetRatingAsync(title, year, imdbId, config.OmdbApiKey, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                await _cache.SetAsync(item.Id, result, cancellationToken).ConfigureAwait(false);
                results.Add(new RatingCacheEntry
                {
                    ItemId = item.Id,
                    Source = result.Source,
                    Rating = result.Rating,
                    VoteCount = result.VoteCount,
                    Url = result.Url,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("IMDB fetch failed for '{Title}': {Error}", title, result.ErrorMessage);
            }
        }

        return results;
    }
}
