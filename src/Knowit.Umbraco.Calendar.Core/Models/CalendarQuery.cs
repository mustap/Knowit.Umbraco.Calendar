namespace Knowit.Umbraco.Calendar.Core.Models;

/// <summary>
/// Describes which events to fetch from a calendar. Times are treated as absolute instants;
/// providers normalize their results to UTC.
/// </summary>
/// <param name="From">Inclusive start of the window.</param>
/// <param name="To">Exclusive end of the window.</param>
/// <param name="MaxResults">Upper bound on returned events (across pages).</param>
/// <param name="ExpandRecurring">
/// When true, recurring series are expanded into individual occurrences
/// (Google <c>singleEvents</c> / Microsoft <c>calendarView</c>).
/// </param>
public sealed record CalendarQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    int MaxResults = 250,
    bool ExpandRecurring = true);
