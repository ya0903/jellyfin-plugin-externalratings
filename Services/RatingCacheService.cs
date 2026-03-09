using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ExternalRatings.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ExternalRatings.Services;

/// <summary>
/// In-memory and disk-backed cache for external ratings.
/// </summary>
public class RatingCacheService
{
    private readonly ConcurrentDictionary<string, RatingCacheEntry> _cache = new();
    private readonly string _cacheFilePath;
    private readonly ILogger<RatingCacheService> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingCacheService"/> class.
    /// </summary>
    public RatingCacheService(IApplicationPaths appPaths, ILogger<RatingCacheService> logger)
    {
        _logger = logger;
        var pluginDataPath = Path.Combine(appPaths.DataPath, "ExternalRatings");
        Directory.CreateDirectory(pluginDataPath);
        _cacheFilePath = Path.Combine(pluginDataPath, "ratings_cache.json");
        LoadFromDisk();
    }

    /// <summary>
    /// Gets a cache key for an item + source pair.
    /// </summary>
    private static string GetKey(Guid itemId, string source) => $"{itemId}:{source}";

    /// <summary>
    /// Tries to get a cached rating.
    /// </summary>
    public bool TryGet(Guid itemId, string source, out RatingCacheEntry? entry)
    {
        var key = GetKey(itemId, source);
        if (_cache.TryGetValue(key, out entry))
        {
            var config = Plugin.Instance?.Configuration;
            var cacheDuration = config?.CacheDurationHours ?? 24;
            if (entry.IsValid(cacheDuration))
                return true;

            // Expired - remove it
            _cache.TryRemove(key, out _);
        }
        entry = null;
        return false;
    }

    /// <summary>
    /// Stores a rating result in the cache.
    /// </summary>
    public async Task SetAsync(Guid itemId, RatingResult result, CancellationToken cancellationToken = default)
    {
        var key = GetKey(itemId, result.Source);
        var entry = new RatingCacheEntry
        {
            ItemId = itemId,
            Source = result.Source,
            Rating = result.Rating,
            VoteCount = result.VoteCount,
            Url = result.Url,
            LastUpdated = DateTime.UtcNow
        };
        _cache[key] = entry;
        await SaveToDiskAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all cached entries for a specific item.
    /// </summary>
    public IEnumerable<RatingCacheEntry> GetAllForItem(Guid itemId)
    {
        var config = Plugin.Instance?.Configuration;
        var cacheDuration = config?.CacheDurationHours ?? 24;

        return _cache.Values
            .Where(e => e.ItemId == itemId && e.IsValid(cacheDuration));
    }

    /// <summary>
    /// Clears all cached ratings.
    /// </summary>
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        await SaveToDiskAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a cached rating for a specific item.
    /// </summary>
    public async Task InvalidateItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(itemId.ToString())).ToList();
        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);
        await SaveToDiskAsync(cancellationToken).ConfigureAwait(false);
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_cacheFilePath)) return;
            var json = File.ReadAllText(_cacheFilePath);
            var entries = JsonSerializer.Deserialize<List<RatingCacheEntry>>(json);
            if (entries == null) return;

            foreach (var entry in entries)
            {
                var key = GetKey(entry.ItemId, entry.Source);
                _cache[key] = entry;
            }
            _logger.LogInformation("Loaded {Count} cached ratings from disk", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ratings cache from disk");
        }
    }

    private async Task SaveToDiskAsync(CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = _cache.Values.ToList();
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(_cacheFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save ratings cache to disk");
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
