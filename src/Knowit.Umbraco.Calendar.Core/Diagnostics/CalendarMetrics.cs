using System.Diagnostics.Metrics;

namespace Knowit.Umbraco.Calendar.Core.Diagnostics;

/// <summary>
/// Metrics for observing calendar health. Collect with OpenTelemetry or
/// <c>dotnet-counters</c> using the meter name <see cref="MeterName"/>.
/// </summary>
public static class CalendarMetrics
{
    public const string MeterName = "Knowit.Umbraco.Calendar";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Events served from the fresh cache without hitting a provider.</summary>
    public static readonly Counter<long> CacheHits =
        Meter.CreateCounter<long>("knowit_calendar.cache.hits");

    /// <summary>Requests that missed the fresh cache and called a provider.</summary>
    public static readonly Counter<long> CacheMisses =
        Meter.CreateCounter<long>("knowit_calendar.cache.misses");

    /// <summary>Provider failures where a stale cached copy was served instead.</summary>
    public static readonly Counter<long> StaleServed =
        Meter.CreateCounter<long>("knowit_calendar.cache.stale_served");

    /// <summary>Provider failures (whether or not a stale fallback was available).</summary>
    public static readonly Counter<long> ProviderErrors =
        Meter.CreateCounter<long>("knowit_calendar.provider.errors");
}
