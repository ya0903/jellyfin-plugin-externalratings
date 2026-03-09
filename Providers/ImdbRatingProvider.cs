using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ExternalRatings.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ExternalRatings.Providers;

/// <summary>
/// Fetches TV show ratings from IMDB via the OMDB API.
/// Requires a free API key from https://www.omdbapi.com/apikey.aspx
/// </summary>
public class ImdbRatingProvider
{
    private const string OmdbBaseUrl = "https://www.omdbapi.com/";
    private const string ImdbTitleBaseUrl = "https://www.imdb.com/title/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImdbRatingProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbRatingProvider"/> class.
    /// </summary>
    public ImdbRatingProvider(IHttpClientFactory httpClientFactory, ILogger<ImdbRatingProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Fetches IMDB rating for a TV show using the OMDB API.
    /// </summary>
    public async Task<RatingResult> GetRatingAsync(
        string title,
        int? year,
        string? imdbId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new RatingResult
            {
                Source = "IMDB",
                Success = false,
                ErrorMessage = "OMDB API key not configured. Get a free key at https://www.omdbapi.com/apikey.aspx"
            };
        }

        try
        {
            OmdbResponse? omdbResult;

            if (!string.IsNullOrEmpty(imdbId))
            {
                // Direct lookup by IMDB ID - most accurate
                omdbResult = await FetchByImdbIdAsync(imdbId, apiKey, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Search by title + year
                omdbResult = await FetchByTitleAsync(title, year, apiKey, cancellationToken).ConfigureAwait(false);
            }

            if (omdbResult == null || omdbResult.Response == "False")
            {
                return new RatingResult
                {
                    Source = "IMDB",
                    Success = false,
                    ErrorMessage = $"Could not find '{title}' on OMDB/IMDB"
                };
            }

            var rating = omdbResult.ImdbRating;
            if (string.IsNullOrEmpty(rating) || rating == "N/A")
            {
                return new RatingResult
                {
                    Source = "IMDB",
                    Success = false,
                    ErrorMessage = "No IMDB rating available",
                    Url = !string.IsNullOrEmpty(omdbResult.ImdbId) ? $"{ImdbTitleBaseUrl}{omdbResult.ImdbId}/" : null
                };
            }

            // Format vote count
            string? voteCount = null;
            if (!string.IsNullOrEmpty(omdbResult.ImdbVotes) && omdbResult.ImdbVotes != "N/A")
            {
                var raw = omdbResult.ImdbVotes.Replace(",", "");
                if (int.TryParse(raw, out var votes))
                    voteCount = FormatCount(votes);
            }

            return new RatingResult
            {
                Source = "IMDB",
                Rating = rating,
                VoteCount = voteCount,
                Url = !string.IsNullOrEmpty(omdbResult.ImdbId) ? $"{ImdbTitleBaseUrl}{omdbResult.ImdbId}/" : null,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching IMDB rating for '{Title}'", title);
            return new RatingResult
            {
                Source = "IMDB",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<OmdbResponse?> FetchByImdbIdAsync(string imdbId, string apiKey, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("ExternalRatings");
        var uri = $"{OmdbBaseUrl}?i={Uri.EscapeDataString(imdbId)}&apikey={apiKey}";
        _logger.LogDebug("Fetching OMDB by IMDB ID: {ImdbId}", imdbId);
        return await client.GetFromJsonAsync<OmdbResponse>(uri, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OmdbResponse?> FetchByTitleAsync(string title, int? year, string apiKey, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("ExternalRatings");
        var uri = $"{OmdbBaseUrl}?t={Uri.EscapeDataString(title)}&type=series&apikey={apiKey}";
        if (year.HasValue)
            uri += $"&y={year.Value}";

        _logger.LogDebug("Fetching OMDB by title: {Title}", title);
        return await client.GetFromJsonAsync<OmdbResponse>(uri, cancellationToken).ConfigureAwait(false);
    }

    private static string FormatCount(int count)
    {
        return count switch
        {
            >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
            >= 1_000 => $"{count / 1_000.0:F0}K",
            _ => count.ToString()
        };
    }

    private sealed class OmdbResponse
    {
        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        [JsonPropertyName("Year")]
        public string? Year { get; set; }

        [JsonPropertyName("imdbRating")]
        public string? ImdbRating { get; set; }

        [JsonPropertyName("imdbVotes")]
        public string? ImdbVotes { get; set; }

        [JsonPropertyName("imdbID")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("Response")]
        public string? Response { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }
    }
}
