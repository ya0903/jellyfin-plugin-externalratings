using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Jellyfin.Plugin.ExternalRatings.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ExternalRatings.Providers;

/// <summary>
/// Fetches movie ratings from Letterboxd.
/// Letterboxd uses a 5-star scale; ratings are shown as "X.XX / 5".
/// </summary>
public class LetterboxdRatingProvider
{
    private const string BaseUrl = "https://letterboxd.com";
    private const string SearchUrl = "https://letterboxd.com/search/films/";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LetterboxdRatingProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LetterboxdRatingProvider"/> class.
    /// </summary>
    public LetterboxdRatingProvider(IHttpClientFactory httpClientFactory, ILogger<LetterboxdRatingProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the Letterboxd rating for a movie by title and year.
    /// </summary>
    public async Task<RatingResult> GetRatingAsync(string title, int? year, string? imdbId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // First try direct film slug if we can derive it
            string? filmUrl = null;

            // Try searching by title + year
            filmUrl = await SearchForFilmAsync(title, year, cancellationToken).ConfigureAwait(false);

            if (filmUrl == null)
            {
                return new RatingResult
                {
                    Source = "Letterboxd",
                    Success = false,
                    ErrorMessage = $"Could not find '{title}' on Letterboxd"
                };
            }

            return await ScrapeFilmPageAsync(filmUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Letterboxd rating for '{Title}'", title);
            return new RatingResult
            {
                Source = "Letterboxd",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<string?> SearchForFilmAsync(string title, int? year, CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();

        var query = year.HasValue ? $"{title} {year}" : title;
        var searchUri = $"{SearchUrl}{Uri.EscapeDataString(query)}/";

        _logger.LogDebug("Searching Letterboxd: {Uri}", searchUri);

        var response = await client.GetAsync(searchUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Search results: ul.results > li.film-detail
        var results = doc.DocumentNode.SelectNodes("//ul[contains(@class,'results')]//li[contains(@class,'film')]//div[@class='film-detail-content']");

        if (results == null || results.Count == 0)
        {
            // Fallback: look for any film poster links
            var posterLinks = doc.DocumentNode.SelectNodes("//div[contains(@class,'film-poster')]/@data-film-slug");
            if (posterLinks != null && posterLinks.Count > 0)
            {
                var slug = posterLinks[0].GetAttributeValue("data-film-slug", null);
                if (!string.IsNullOrEmpty(slug))
                    return $"{BaseUrl}/film/{slug}/";
            }
            return null;
        }

        foreach (var result in results)
        {
            // Get the film title link
            var titleNode = result.SelectSingleNode(".//h2[contains(@class,'film-name')]/a");
            if (titleNode == null) continue;

            var resultTitle = titleNode.InnerText.Trim();
            var href = titleNode.GetAttributeValue("href", null);

            // Check year if available
            if (year.HasValue)
            {
                var yearNode = result.SelectSingleNode(".//span[@class='metadata']");
                var yearText = yearNode?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(yearText) && yearText.Contains(year.Value.ToString()))
                {
                    return $"{BaseUrl}{href}";
                }
                // If title matches closely, use it anyway
                if (string.Equals(resultTitle, title, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{BaseUrl}{href}";
                }
            }
            else
            {
                return $"{BaseUrl}{href}";
            }
        }

        // Return first result as fallback
        var firstLink = results[0].SelectSingleNode(".//h2[contains(@class,'film-name')]/a");
        if (firstLink != null)
        {
            var href = firstLink.GetAttributeValue("href", null);
            if (!string.IsNullOrEmpty(href))
                return $"{BaseUrl}{href}";
        }

        return null;
    }

    private async Task<RatingResult> ScrapeFilmPageAsync(string filmUrl, CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();

        _logger.LogDebug("Scraping Letterboxd film page: {Url}", filmUrl);

        var response = await client.GetAsync(filmUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Rating is in a meta tag: <meta name="twitter:data2" content="3.74 out of 5">
        var ratingMeta = doc.DocumentNode.SelectSingleNode("//meta[@name='twitter:data2']");
        var ratingContent = ratingMeta?.GetAttributeValue("content", null);

        string? ratingValue = null;
        if (!string.IsNullOrEmpty(ratingContent))
        {
            // "3.74 out of 5" -> extract "3.74"
            var match = Regex.Match(ratingContent, @"([\d.]+)\s+out of\s+5");
            if (match.Success)
                ratingValue = match.Groups[1].Value;
        }

        // Fallback: try the aggregated rating in the page body
        if (ratingValue == null)
        {
            var ratingDiv = doc.DocumentNode.SelectSingleNode("//a[contains(@class,'display-rating')]");
            ratingValue = ratingDiv?.InnerText?.Trim();
        }

        // Get vote count from the histogram or rating count
        string? voteCount = null;
        var countMatch = Regex.Match(html, @"""ratingCount""\s*:\s*(\d+)");
        if (countMatch.Success)
        {
            var count = int.Parse(countMatch.Groups[1].Value);
            voteCount = FormatCount(count);
        }

        if (ratingValue == null)
        {
            return new RatingResult
            {
                Source = "Letterboxd",
                Success = false,
                ErrorMessage = "Rating not found on Letterboxd page",
                Url = filmUrl
            };
        }

        return new RatingResult
        {
            Source = "Letterboxd",
            Rating = ratingValue,
            VoteCount = voteCount,
            Url = filmUrl,
            Success = true
        };
    }

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("ExternalRatings");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
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
}
