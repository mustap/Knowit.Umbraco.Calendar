using System.Globalization;
using Knowit.Umbraco.Calendar.Core.Models;
using Knowit.Umbraco.Calendar.Core.Text;
using GData = Google.Apis.Calendar.v3.Data;

namespace Knowit.Umbraco.Calendar.Core.Providers.Google;

/// <summary>
/// Pure mapping from Google's <see cref="GData.Event"/> to the normalized <see cref="CalendarEvent"/>.
/// Kept free of I/O so it can be unit-tested against constructed event POCOs.
/// </summary>
public static class GoogleEventMapper
{
    public static CalendarEvent Map(GData.Event e)
    {
        var (start, startIsDateOnly) = Resolve(e.Start);
        var (end, _) = Resolve(e.End);

        return new CalendarEvent(
            Id: e.Id ?? string.Empty,
            Title: e.Summary ?? string.Empty,
            // Google descriptions can contain HTML — sanitize to safe HTML.
            Description: HtmlSanitization.SanitizeHtml(e.Description),
            Location: e.Location,
            Start: start,
            End: end,
            IsAllDay: startIsDateOnly,
            OrganizerName: e.Organizer?.DisplayName ?? e.Organizer?.Email,
            HtmlLink: e.HtmlLink,
            OnlineMeeting: ResolveOnlineMeeting(e),
            Categories: [],
            Recurrence: ResolveRecurrence(e));
    }

    /// <summary>
    /// Resolves an event time. All-day events carry a date-only <c>Date</c> string; timed events
    /// carry an instant. Both are normalized to UTC.
    /// </summary>
    private static (DateTimeOffset value, bool isDateOnly) Resolve(GData.EventDateTime? dt)
    {
        if (dt is null)
        {
            return (default, false);
        }

        if (!string.IsNullOrEmpty(dt.Date))
        {
            var date = DateOnly.ParseExact(dt.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return (new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), true);
        }

        var instant = dt.DateTimeDateTimeOffset ?? default;
        return (instant.ToUniversalTime(), false);
    }

    private static OnlineMeeting? ResolveOnlineMeeting(GData.Event e)
    {
        // Preferred: a "video" conference entry point.
        var videoEntry = e.ConferenceData?.EntryPoints?
            .FirstOrDefault(ep => string.Equals(ep.EntryPointType, "video", StringComparison.OrdinalIgnoreCase));

        if (videoEntry?.Uri is { Length: > 0 } videoUri)
        {
            var solution = e.ConferenceData?.ConferenceSolution;
            return new OnlineMeeting(videoUri, ClassifyGoogle(solution?.Key?.Type), solution?.Name);
        }

        // Fallback: legacy Hangouts/Meet link.
        if (e.HangoutLink is { Length: > 0 } hangout)
        {
            return new OnlineMeeting(hangout, OnlineMeetingProvider.GoogleMeet, "Google Meet");
        }

        return null;
    }

    private static OnlineMeetingProvider ClassifyGoogle(string? solutionKeyType) => solutionKeyType switch
    {
        "hangoutsMeet" => OnlineMeetingProvider.GoogleMeet,
        "eventHangout" or "eventNamedHangout" => OnlineMeetingProvider.GoogleMeet,
        null or "" => OnlineMeetingProvider.Unknown,
        _ => OnlineMeetingProvider.Other,
    };

    private static RecurrenceInfo? ResolveRecurrence(GData.Event e)
    {
        // Expanded occurrence of a series.
        if (!string.IsNullOrEmpty(e.RecurringEventId))
        {
            return new RecurrenceInfo(IsInstance: true, SeriesId: e.RecurringEventId, Rules: []);
        }

        // Unexpanded series master, carrying RRULEs.
        if (e.Recurrence is { Count: > 0 } rules)
        {
            return new RecurrenceInfo(IsInstance: false, SeriesId: null, Rules: rules.ToList());
        }

        return null;
    }
}
