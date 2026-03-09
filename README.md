# 🎬 Jellyfin External Ratings Plugin

A Jellyfin plugin that displays **Letterboxd ratings** for movies and **IMDB ratings** for TV shows, directly in Jellyfin's item detail pages. Works with all standard Jellyfin themes.

---

## ✨ Features

| Content Type | Rating Source | How It Works |
|---|---|---|
| Movies | [Letterboxd](https://letterboxd.com) | Scrapes letterboxd.com (no API key needed) |
| TV Shows | [IMDB via OMDB](https://www.omdbapi.com) | Uses OMDB API (free key required) |

- **Themed badges** — green Letterboxd badge and yellow IMDB badge
- **Clickable** — opens the film/show's page on Letterboxd or IMDB
- **Cached** — ratings are cached to disk to avoid excessive requests (configurable duration)
- **Works with all themes** — uses Jellyfin's routing events to inject badges
- **Admin UI** — configure from Dashboard → Plugins → External Ratings

---

## 📦 Installation

### Method 1: Manual (Recommended)

1. Download the latest release `.zip` from the [Releases page](../../releases)
2. Extract to your Jellyfin plugins directory:
   - **Linux**: `/var/lib/jellyfin/plugins/ExternalRatings/`
   - **Windows**: `%APPDATA%\Jellyfin\plugins\ExternalRatings\`
   - **Docker**: `/config/plugins/ExternalRatings/`
3. Restart Jellyfin
4. Go to **Dashboard → Plugins → External Ratings** to configure

### Method 2: Build from Source

Requirements:
- .NET 8 SDK
- Jellyfin 10.9+

```bash
git clone https://github.com/your-repo/Jellyfin.Plugin.ExternalRatings
cd Jellyfin.Plugin.ExternalRatings
dotnet publish -c Release -o ./dist
# Copy ./dist/Jellyfin.Plugin.ExternalRatings.dll to your plugins folder
```

---

## ⚙️ Configuration

1. Navigate to **Dashboard → Plugins → External Ratings**
2. Configure the following:

### Letterboxd (Movies)
- ✅ Enable/disable — **No API key needed**
- Ratings are scraped from letterboxd.com using movie title + year

### IMDB (TV Shows)
- ✅ Enable/disable
- **OMDB API Key** — Get a free key at [omdbapi.com/apikey.aspx](https://www.omdbapi.com/apikey.aspx)
  - Free tier: 1,000 requests/day
  - Ratings are fetched by IMDB ID (if available) or title search

### Cache
- **Cache Duration** — How long to cache ratings before re-fetching (default: 24 hours)
- **Clear Cache** — Force refresh all ratings

---

## 🎨 How Ratings Look

Ratings appear as colored badge pills below the item title on detail pages:

```
[🟢 3.8 / 5 (247K)]    ← Letterboxd (movies)
[🟡 8.2 / 10 (1.2M)]   ← IMDB (TV shows)
```

- Click a badge to open the movie/show's page on Letterboxd or IMDB
- A small ↺ refresh button lets you force-refresh ratings for a specific item

---

## 🔌 API Endpoints

The plugin exposes these REST endpoints:

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/ExternalRatings/ratings/{itemId}` | Get ratings for an item |
| `GET` | `/ExternalRatings/ratings/{itemId}?forceRefresh=true` | Force-refresh ratings |
| `GET` | `/ExternalRatings/config` | Get plugin config (admin) |
| `DELETE` | `/ExternalRatings/cache` | Clear all cached ratings (admin) |
| `DELETE` | `/ExternalRatings/cache/{itemId}` | Invalidate item cache (admin) |

---

## 🏗️ Architecture

```
Jellyfin.Plugin.ExternalRatings/
├── Plugin.cs                           # Plugin entry point & ID
├── PluginConfiguration.cs              # Configuration model
├── PluginServiceRegistrator.cs         # DI registration
├── Providers/
│   ├── LetterboxdRatingProvider.cs     # Scrapes Letterboxd
│   └── ImdbRatingProvider.cs          # Fetches OMDB/IMDB
├── Services/
│   ├── ExternalRatingService.cs        # Orchestration
│   └── RatingCacheService.cs          # Disk-backed cache
├── Api/
│   └── ExternalRatingsController.cs   # REST API
├── Models/
│   └── RatingModels.cs                # Data models
├── Configuration/
│   └── configPage.html                # Admin dashboard page
└── Web/
    ├── externalratings.js             # Client-side injection
    └── externalratings.css            # Styles
```

---

## ⚠️ Notes

- **Letterboxd**: This plugin scrapes Letterboxd's public pages. There is no official API. If Letterboxd changes their HTML structure, the scraping may break until the plugin is updated.
- **Rate limiting**: Ratings are cached to minimize requests. The default 24-hour cache means each movie/show is fetched at most once per day.
- **IMDB ID matching**: If your media has an IMDB ID in its metadata (from TMDB or TVDB scrapers), ratings will be highly accurate. Without it, the plugin falls back to title search.

---

## 📝 License

MIT
