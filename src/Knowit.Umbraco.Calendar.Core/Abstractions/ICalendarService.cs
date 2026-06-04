using Knowit.Umbraco.Calendar.Core.Models;

namespace Knowit.Umbraco.Calendar.Core.Abstractions;

/// <summary>
/// The provider-agnostic facade the rest of the package (rendering, backoffice, Delivery API)
/// depends on. Routes to the correct <see cref="ICalendarProvider"/> by
/// <see cref="CalendarRef.ProviderKey"/>. The caching decorator wraps this in Phase 2.
/// </summary>
public interface ICalendarService
{
    /// <summary>Returns every calendar across all registered providers (the union of allowlists).</summary>
    Task<IReadOnlyList<CalendarRef>> GetAvailableCalendarsAsync(CancellationToken ct = default);

    /// <summary>Fetches events for the given calendar, dispatching to its provider.</summary>
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarRef calendar,
        CalendarQuery query,
        CancellationToken ct = default);
}
