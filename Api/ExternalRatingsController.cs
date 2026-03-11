using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ExternalRatings.Models;
using Jellyfin.Plugin.ExternalRatings.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ExternalRatings.Api;

/// <summary>
/// API controller for external ratings.
/// </summary>
[ApiController]
[Route("ExternalRatings")]
public class ExternalRatingsController : ControllerBase
{
    private readonly ExternalRatingService _ratingService;
    private readonly RatingCacheService _cacheService;
    private readonly ILogger<ExternalRatingsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalRatingsController"/> class.
    /// </summary>
    public ExternalRatingsController(ILogger<ExternalRatingsController> logger)
    {
        _ratingService = PluginServiceLocator.RatingService!;
        _cacheService = PluginServiceLocator.CacheService!;
        _logger = logger;
    }

    /// <summary>
    /// Gets external ratings for a Jellyfin item.
    /// </summary>
    [HttpGet("ratings/{itemId}")]
    public async Task<ActionResult<IEnumerable<RatingDto>>> GetRatings(
        [FromRoute] string itemId,
        [FromQuery] bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(itemId, out var id) && 
            !Guid.TryParseExact(itemId, "N", out id))
            return BadRequest("Invalid item ID");

        var ratings = await _ratingService.GetRatingsAsync(id, forceRefresh, cancellationToken).ConfigureAwait(false);
        var list = ratings.ToList();
        if (!list.Any()) return NotFound();

        return Ok(list.Select(r => new RatingDto
        {
            Source = r.Source,
            Rating = r.Rating,
            VoteCount = r.VoteCount,
            Url = r.Url,
            LastUpdated = r.LastUpdated
        }));
    }

    /// <summary>
    /// Clears all cached ratings (admin only).
    /// </summary>
    [HttpDelete("cache")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ClearCache(CancellationToken cancellationToken)
    {
        await _cacheService.ClearAllAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Invalidates the cached rating for a specific item (admin only).
    /// </summary>
    [HttpDelete("cache/{itemId}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> InvalidateItem([FromRoute] Guid itemId, CancellationToken cancellationToken)
    {
        await _cacheService.InvalidateItemAsync(itemId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Gets the plugin configuration (admin only).
    /// </summary>
    [HttpGet("config")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PluginConfigDto> GetConfig()
    {
        var config = Plugin.Instance?.Configuration;
        return Ok(new PluginConfigDto
        {
            EnableLetterboxdRatings = config?.EnableLetterboxdRatings ?? true,
            EnableImdbTvRatings = config?.EnableImdbTvRatings ?? true,
            HasOmdbApiKey = !string.IsNullOrWhiteSpace(config?.OmdbApiKey),
            CacheDurationHours = config?.CacheDurationHours ?? 24,
            ShowRatingBadge = config?.ShowRatingBadge ?? true,
            ShowRatingOnCards = config?.ShowRatingOnCards ?? false
        });
    }
}

/// <summary>
/// DTO for rating data returned to the client.
/// </summary>
public class RatingDto
{
    /// <summary>Gets or sets the source name.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the rating value.</summary>
    public string? Rating { get; set; }

    /// <summary>Gets or sets the vote count.</summary>
    public string? VoteCount { get; set; }

    /// <summary>Gets or sets the URL.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets when this was last fetched.</summary>
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// DTO for plugin config (safe to expose to client).
/// </summary>
public class PluginConfigDto
{
    /// <summary>Gets or sets whether Letterboxd is enabled.</summary>
    public bool EnableLetterboxdRatings { get; set; }

    /// <summary>Gets or sets whether IMDB TV is enabled.</summary>
    public bool EnableImdbTvRatings { get; set; }

    /// <summary>Gets or sets whether an OMDB key is configured.</summary>
    public bool HasOmdbApiKey { get; set; }

    /// <summary>Gets or sets cache duration in hours.</summary>
    public int CacheDurationHours { get; set; }

    /// <summary>Gets or sets whether to show rating badge.</summary>
    public bool ShowRatingBadge { get; set; }

    /// <summary>Gets or sets whether to show rating on cards.</summary>
    public bool ShowRatingOnCards { get; set; }
}
