using System;

namespace Jellyfin.Plugin.ExternalRatings.Models;

/// <summary>
/// Cached external rating entry.
/// </summary>
public class RatingCacheEntry
{
    /// <summary>
    /// Gets or sets the Jellyfin item ID.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the rating source (e.g., "Letterboxd", "IMDB").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rating value (e.g., "3.8", "8.2/10").
    /// </summary>
    public string? Rating { get; set; }

    /// <summary>
    /// Gets or sets the number of ratings/votes.
    /// </summary>
    public string? VoteCount { get; set; }

    /// <summary>
    /// Gets or sets the direct URL to the rating page.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets when this entry was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Gets a value indicating whether the cache entry is still valid.
    /// </summary>
    public bool IsValid(int cacheDurationHours)
        => DateTime.UtcNow - LastUpdated < TimeSpan.FromHours(cacheDurationHours);
}

/// <summary>
/// Rating result returned by providers.
/// </summary>
public class RatingResult
{
    /// <summary>
    /// Gets or sets the source name.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rating string.
    /// </summary>
    public string? Rating { get; set; }

    /// <summary>
    /// Gets or sets vote count string.
    /// </summary>
    public string? VoteCount { get; set; }

    /// <summary>
    /// Gets or sets the URL.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the fetch was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets an error message if fetch failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
