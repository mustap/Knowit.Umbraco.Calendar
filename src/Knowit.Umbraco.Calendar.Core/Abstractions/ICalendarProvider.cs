using Knowit.Umbraco.Calendar.Core.Models;

namespace Knowit.Umbraco.Calendar.Core.Abstractions;

/// <summary>
/// A calendar backend (Google, Microsoft, …). One implementation per provider.
/// Implementations are read-only in v1 and must normalize results to <see cref="CalendarEvent"/>.
/// </summary>
public interface ICalendarProvider
{
    /// <summary>Stable provider discriminator, matching <see cref="CalendarRef.ProviderKey"/>.</summary>
    string Key { get; }

    /// <summary>Lists the calendars this provider exposes (the admin-configured allowlist).</summary>
    Task<IReadOnlyList<CalendarRef>> ListCalendarsAsync(CancellationToken ct = default);

    /// <summary>Fetches events for a single calendar within the query window.</summary>
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string calendarId,
        CalendarQuery query,
        CancellationToken ct = default);
}
