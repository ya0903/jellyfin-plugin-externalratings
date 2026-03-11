/**
 * Jellyfin External Ratings - Client Plugin
 * Injects Letterboxd ratings (movies) and IMDB ratings (TV shows)
 * into Jellyfin's detail pages and optionally on poster cards.
 * 
 * This script auto-initializes and hooks into Jellyfin's routing.
 */

(function () {
    'use strict';

    const PLUGIN_NAME = 'ExternalRatings';
    const API_BASE = '/ExternalRatings';

    // -----------------------------------------------------------------------
    // Styles
    // -----------------------------------------------------------------------
    const STYLES = `
        .er-badge-container {
            display: flex;
            align-items: center;
            gap: 10px;
            margin: 6px 0 10px 0;
            flex-wrap: wrap;
        }
        .er-badge {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 4px 10px 4px 8px;
            border-radius: 6px;
            font-size: 0.85em;
            font-weight: 600;
            text-decoration: none;
            transition: opacity 0.15s, transform 0.15s;
            cursor: pointer;
            border: none;
            white-space: nowrap;
        }
        .er-badge:hover {
            opacity: 0.85;
            transform: translateY(-1px);
        }
        .er-badge-letterboxd {
            background-color: #00e054;
            color: #14181c;
        }
        .er-badge-imdb {
            background-color: #f5c518;
            color: #000000;
        }
        .er-badge-logo {
            width: 18px;
            height: 18px;
            border-radius: 3px;
            object-fit: contain;
            flex-shrink: 0;
        }
        .er-badge-rating {
            font-size: 1em;
            font-weight: 700;
        }
        .er-badge-label {
            font-size: 0.8em;
            font-weight: 500;
            opacity: 0.85;
        }
        .er-badge-votes {
            font-size: 0.75em;
            opacity: 0.7;
            font-weight: 400;
        }
        .er-badge-loading {
            background-color: rgba(255,255,255,0.08);
            color: rgba(255,255,255,0.4);
            animation: er-pulse 1.5s infinite;
        }
        @keyframes er-pulse {
            0%, 100% { opacity: 0.5; }
            50% { opacity: 1; }
        }

        /* Card overlay (optional) */
        .er-card-badge {
            position: absolute;
            bottom: 5px;
            left: 5px;
            z-index: 10;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 0.7em;
            font-weight: 700;
            pointer-events: none;
        }
        .er-card-letterboxd {
            background-color: rgba(0, 224, 84, 0.92);
            color: #14181c;
        }
        .er-card-imdb {
            background-color: rgba(245, 197, 24, 0.92);
            color: #000;
        }
    `;

    // -----------------------------------------------------------------------
    // SVG Logos (inline, no external deps)
    // -----------------------------------------------------------------------
    const LETTERBOXD_LOGO_SVG = `<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg" fill="currentColor">
        <circle cx="12" cy="12" r="12" fill="#14181c"/>
        <path d="M7 8.5c0 .83.67 1.5 1.5 1.5S10 9.33 10 8.5 9.33 7 8.5 7 7 7.67 7 8.5zm4 0c0 .83.67 1.5 1.5 1.5s1.5-.67 1.5-1.5S13.33 7 12.5 7 11 7.67 11 8.5zm4 0c0 .83.67 1.5 1.5 1.5S18 9.33 18 8.5 17.33 7 16.5 7 15 7.67 15 8.5z" fill="#00e054"/>
        <circle cx="8.5" cy="15.5" r="1.5" fill="#00e054"/>
        <circle cx="12.5" cy="15.5" r="1.5" fill="#00e054"/>
        <circle cx="16.5" cy="15.5" r="1.5" fill="#00e054"/>
    </svg>`;

    const IMDB_LOGO_SVG = `<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
        <rect width="24" height="24" rx="4" fill="#f5c518"/>
        <text x="2" y="17" font-size="11" font-weight="900" font-family="Arial,sans-serif" fill="#000">IMDb</text>
    </svg>`;

    // -----------------------------------------------------------------------
    // Utility
    // -----------------------------------------------------------------------
    function injectStyles() {
        if (document.getElementById('er-styles')) return;
        const style = document.createElement('style');
        style.id = 'er-styles';
        style.textContent = STYLES;
        document.head.appendChild(style);
    }

    function svgToDataUrl(svg) {
        return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
    }

    function formatGuid(id) {
        if (!id) return null;
        id = id.replace(/-/g, '');
        if (id.length === 32) {
            return id.substring(0,8) + '-' + id.substring(8,12) + '-' + 
                   id.substring(12,16) + '-' + id.substring(16,20) + '-' + id.substring(20);
        }
        return id;
    }

    function getItemId() {
        const match = window.location.hash.match(/id=([a-f0-9]{32,36})/i)
            || window.location.search.match(/[?&]id=([a-f0-9\-]{32,36})/i);
        return match ? formatGuid(match[1]) : null;
    }

    async function fetchRatings(itemId, forceRefresh) {
        const token = ApiClient ? ApiClient.accessToken() : null;
        const url = `${API_BASE}/ratings/${itemId}${forceRefresh ? '?forceRefresh=true' : ''}`;
        const headers = { 'Content-Type': 'application/json' };
        if (token) headers['Authorization'] = `MediaBrowser Token="${token}"`;

        const resp = await fetch(url, { headers });
        if (resp.status === 404) return [];
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        return resp.json();
    }

    async function fetchConfig() {
        try {
            const token = ApiClient ? ApiClient.accessToken() : null;
            const headers = {};
            if (token) headers['Authorization'] = `MediaBrowser Token="${token}"`;
            const resp = await fetch(`${API_BASE}/config`, { headers });
            if (!resp.ok) return null;
            return resp.json();
        } catch { return null; }
    }

    // -----------------------------------------------------------------------
    // Badge rendering
    // -----------------------------------------------------------------------
    function createBadge(rating) {
        const isLetterboxd = rating.source === 'Letterboxd';
        const anchor = document.createElement('a');
        anchor.className = `er-badge ${isLetterboxd ? 'er-badge-letterboxd' : 'er-badge-imdb'}`;
        anchor.href = rating.url || '#';
        anchor.target = rating.url ? '_blank' : '_self';
        anchor.rel = 'noopener noreferrer';
        anchor.title = `Open on ${rating.source}`;

        // Logo
        const logoDiv = document.createElement('span');
        logoDiv.style.cssText = 'width:18px;height:18px;display:inline-flex;align-items:center;flex-shrink:0;';
        logoDiv.innerHTML = isLetterboxd ? LETTERBOXD_LOGO_SVG : IMDB_LOGO_SVG;
        anchor.appendChild(logoDiv);

        // Rating number
        const ratingSpan = document.createElement('span');
        ratingSpan.className = 'er-badge-rating';
        ratingSpan.textContent = isLetterboxd ? `${rating.rating} / 5` : `${rating.rating} / 10`;
        anchor.appendChild(ratingSpan);

        // Vote count
        if (rating.voteCount) {
            const votesSpan = document.createElement('span');
            votesSpan.className = 'er-badge-votes';
            votesSpan.textContent = `(${rating.voteCount})`;
            anchor.appendChild(votesSpan);
        }

        return anchor;
    }

    function createLoadingBadge() {
        const span = document.createElement('span');
        span.className = 'er-badge er-badge-loading';
        span.textContent = '⟳ Loading ratings...';
        return span;
    }

    function findDetailPageInsertPoint() {
        // Try various selectors used by different Jellyfin themes
        const selectors = [
            // Default Jellyfin theme
            '.itemDetailPage .itemMiscInfo',
            '.itemDetailPage .itemMiscInfo-secondary',
            '.detailPageContent .itemName',
            // Jellyseerr / Skin B
            '.detail-page .media-title',
            // Generic fallback
            '.itemDetailPage h1',
            '.itemDetailPage .itemName',
            '[data-type="detail"] .itemName',
            '.detailLogo',
        ];

        for (const sel of selectors) {
            const el = document.querySelector(sel);
            if (el) return el;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Main injection logic
    // -----------------------------------------------------------------------
    async function injectRatingsOnDetailPage() {
        const itemId = getItemId();
        if (!itemId) return;

        // Avoid duplicate injection
        const existingContainer = document.getElementById(`er-container-${itemId}`);
        if (existingContainer) return;

        const insertPoint = findDetailPageInsertPoint();
        if (!insertPoint) return;

        // Show loading state
        const container = document.createElement('div');
        container.className = 'er-badge-container';
        container.id = `er-container-${itemId}`;
        container.appendChild(createLoadingBadge());
        insertPoint.parentNode.insertBefore(container, insertPoint.nextSibling);

        try {
            const ratings = await fetchRatings(itemId, false);
            container.innerHTML = '';

            if (!ratings || ratings.length === 0) {
                container.remove();
                return;
            }

            for (const rating of ratings) {
                if (rating.rating) {
                    container.appendChild(createBadge(rating));
                }
            }

            // Refresh button
            const refreshBtn = document.createElement('button');
            refreshBtn.className = 'er-badge er-badge-loading';
            refreshBtn.style.cssText = 'font-size:0.75em;padding:3px 8px;cursor:pointer;border:1px solid rgba(255,255,255,0.15);background:transparent;color:rgba(255,255,255,0.4);border-radius:4px;';
            refreshBtn.textContent = '↺';
            refreshBtn.title = 'Refresh ratings';
            refreshBtn.addEventListener('click', async () => {
                refreshBtn.textContent = '⟳';
                const fresh = await fetchRatings(itemId, true);
                container.innerHTML = '';
                for (const r of fresh) {
                    if (r.rating) container.appendChild(createBadge(r));
                }
                container.appendChild(refreshBtn);
                refreshBtn.textContent = '↺';
            });
            container.appendChild(refreshBtn);

        } catch (err) {
            console.warn(`[${PLUGIN_NAME}] Failed to fetch ratings:`, err);
            container.remove();
        }
    }

    // -----------------------------------------------------------------------
    // Initialization & routing hook
    // -----------------------------------------------------------------------
    function init() {
        injectStyles();

        // Hook into Jellyfin's navigation events
        document.addEventListener('viewshow', function (e) {
            const view = e.detail || e.target;
            // Check if we're on a detail page
            const isDetailPage = document.querySelector('.itemDetailPage')
                || document.querySelector('.detailPageContent');

            if (isDetailPage) {
                setTimeout(injectRatingsOnDetailPage, 300);
            }
        });

        // Also listen for hash changes (SPA navigation)
        window.addEventListener('hashchange', function () {
            setTimeout(function () {
                const isDetailPage = document.querySelector('.itemDetailPage')
                    || document.querySelector('.detailPageContent');
                if (isDetailPage) {
                    injectRatingsOnDetailPage();
                }
            }, 500);
        });

        // Initial run if we're already on a detail page
        if (document.querySelector('.itemDetailPage') || document.querySelector('.detailPageContent')) {
            setTimeout(injectRatingsOnDetailPage, 600);
        }
    }

    // Wait for DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    console.log(`[${PLUGIN_NAME}] Client plugin loaded`);
})();
