using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ExternalRatings;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the OMDB API key (for IMDB TV ratings).
    /// Free key available at https://www.omdbapi.com/apikey.aspx
    /// </summary>
    public string OmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to show Letterboxd ratings for movies.
    /// </summary>
    public bool EnableLetterboxdRatings { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show IMDB ratings for TV shows.
    /// </summary>
    public bool EnableImdbTvRatings { get; set; } = true;

    /// <summary>
    /// Gets or sets cache duration in hours.
    /// </summary>
    public int CacheDurationHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets a value indicating whether to show rating badge in item detail pages.
    /// </summary>
    public bool ShowRatingBadge { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to show rating badge on cards/posters.
    /// </summary>
    public bool ShowRatingOnCards { get; set; } = false;
}
