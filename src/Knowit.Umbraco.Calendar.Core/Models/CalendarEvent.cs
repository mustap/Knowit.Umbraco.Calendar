namespace Knowit.Umbraco.Calendar.Core.Models;

/// <summary>
/// A calendar event, normalized across Google and Microsoft. The rendering layer only ever
/// sees this type — never provider SDK types. Start/End are normalized to UTC.
/// </summary>
public sealed record CalendarEvent(
    string Id,
    string Title,
    // Sanitized, safe-to-render HTML (or null). Safe for @Html.Raw / headless clients.
    string? Description,
    string? Location,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsAllDay,
    string? OrganizerName,
    string? HtmlLink,
    OnlineMeeting? OnlineMeeting,
    IReadOnlyList<string> Categories,
    RecurrenceInfo? Recurrence);
