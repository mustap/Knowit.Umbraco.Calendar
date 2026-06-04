namespace Knowit.Umbraco.Calendar.Core.Models;

/// <summary>
/// Recurrence metadata for an event. Null when the event is a one-off.
/// </summary>
/// <param name="IsInstance">
/// True when this event is a single expanded occurrence of a recurring series
/// (as returned when <see cref="CalendarQuery.ExpandRecurring"/> is set).
/// </param>
/// <param name="SeriesId">Identifier of the parent series, when this is an instance.</param>
/// <param name="Rules">
/// Raw recurrence rules (RRULE strings) when this is a series master rather than an instance.
/// </param>
public sealed record RecurrenceInfo(
    bool IsInstance,
    string? SeriesId,
    IReadOnlyList<string> Rules);
