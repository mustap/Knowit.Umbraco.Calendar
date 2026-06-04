using System.Diagnostics;
using System.Text.Json;
using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Configuration;
using Knowit.Umbraco.Calendar.Core.Diagnostics;
using Knowit.Umbraco.Calendar.Core.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowit.Umbraco.Calendar.Core.Services;

/// <summary>
/// Decorates an <see cref="ICalendarService"/> with caching so providers are never hit
/// synchronously from a request hot path. Adds a short fresh TTL (+ jitter) and a longer stale
/// copy that is served when the provider call fails — keeping the page rendering even when an
/// upstream API is down.
/// </summary>
/// <remarks>
/// Backed by <see cref="IDistributedCache"/> so it works with both the in-process memory cache
/// and a real distributed cache (required for a load-balanced backoffice). Transient-error
/// backoff/retry is handled by the underlying Graph/Google SDK pipelines.
/// </remarks>
public sealed class CachingCalendarService : ICalendarService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICalendarService _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachingCalendarService> _logger;
    private readonly CalendarOptions _options;

    public CachingCalendarService(
        ICalendarService inner,
        IDistributedCache cache,
        IOptions<CalendarOptions> options,
        ILogger<CachingCalendarService> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    // The set of available calendars comes from configuration (cheap) — pass straight through.
    public Task<IReadOnlyList<CalendarRef>> GetAvailableCalendarsAsync(CancellationToken ct = default)
        => _inner.GetAvailableCalendarsAsync(ct);

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarRef calendar,
        CalendarQuery query,
        CancellationToken ct = default)
    {
        var freshKey = BuildKey(calendar, query, stale: false);
        var staleKey = BuildKey(calendar, query, stale: true);

        var providerTag = new TagList { { "provider", calendar.ProviderKey } };

        if (await TryReadAsync(freshKey, ct).ConfigureAwait(false) is { } fresh)
        {
            CalendarMetrics.CacheHits.Add(1, providerTag);
            return fresh;
        }

        CalendarMetrics.CacheMisses.Add(1, providerTag);

        try
        {
            var events = await _inner.GetEventsAsync(calendar, query, ct).ConfigureAwait(false);
            var payload = JsonSerializer.SerializeToUtf8Bytes(events, JsonOptions);

            await _cache.SetAsync(freshKey, payload, FreshEntryOptions(), ct).ConfigureAwait(false);
            await _cache.SetAsync(staleKey, payload, StaleEntryOptions(), ct).ConfigureAwait(false);

            return events;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CalendarMetrics.ProviderErrors.Add(1, providerTag);

            if (await TryReadAsync(staleKey, ct).ConfigureAwait(false) is { } stale)
            {
                CalendarMetrics.StaleServed.Add(1, providerTag);
                _logger.LogWarning(
                    ex,
                    "Calendar provider '{Provider}' failed for calendar '{CalendarId}'; serving stale cached events.",
                    calendar.ProviderKey, calendar.CalendarId);
                return stale;
            }

            _logger.LogError(
                ex,
                "Calendar provider '{Provider}' failed for calendar '{CalendarId}' and no cached fallback is available.",
                calendar.ProviderKey, calendar.CalendarId);
            throw;
        }
    }

    private async Task<IReadOnlyList<CalendarEvent>?> TryReadAsync(string key, CancellationToken ct)
    {
        var bytes = await _cache.GetAsync(key, ct).ConfigureAwait(false);
        if (bytes is null or { Length: 0 })
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<CalendarEvent>>(bytes, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached calendar entry '{Key}'; ignoring.", key);
            return null;
        }
    }

    private DistributedCacheEntryOptions FreshEntryOptions()
    {
        var baseTtl = TimeSpan.FromSeconds(Math.Max(1, _options.DefaultCacheSeconds));
        // Add up to 10% jitter to avoid synchronized expiry (thundering herd under load balancing).
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * baseTtl.TotalMilliseconds * 0.10);
        return new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = baseTtl + jitter };
    }

    private DistributedCacheEntryOptions StaleEntryOptions()
        => new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, _options.StaleFallbackHours)) };

    internal static string BuildKey(CalendarRef calendar, CalendarQuery query, bool stale)
        => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"knowit:cal:{(stale ? "stale" : "fresh")}:{calendar.ProviderKey}:{calendar.CalendarId}:{query.From:o}:{query.To:o}:{query.MaxResults}:{query.ExpandRecurring}");
}
