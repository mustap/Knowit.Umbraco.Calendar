using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;

namespace Knowit.Umbraco.Calendar.Core.Services;

/// <summary>
/// Default facade: aggregates the registered providers and dispatches by provider key.
/// Not cached — <c>CachingCalendarService</c> (Phase 2) decorates this.
/// </summary>
public sealed class DefaultCalendarService : ICalendarService
{
    private readonly IReadOnlyDictionary<string, ICalendarProvider> _providers;

    public DefaultCalendarService(IEnumerable<ICalendarProvider> providers)
        => _providers = providers.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<CalendarRef>> GetAvailableCalendarsAsync(CancellationToken ct = default)
    {
        var all = new List<CalendarRef>();
        foreach (var provider in _providers.Values)
        {
            all.AddRange(await provider.ListCalendarsAsync(ct).ConfigureAwait(false));
        }

        return all;
    }

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarRef calendar,
        CalendarQuery query,
        CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(calendar.ProviderKey, out var provider))
        {
            throw new InvalidOperationException(
                $"No calendar provider is registered for key '{calendar.ProviderKey}'.");
        }

        return provider.GetEventsAsync(calendar.CalendarId, query, ct);
    }
}
