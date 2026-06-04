namespace Knowit.Umbraco.Calendar.Core.Models;

/// <summary>The conferencing platform backing an online meeting.</summary>
public enum OnlineMeetingProvider
{
    Unknown = 0,
    MicrosoftTeams,
    GoogleMeet,
    Other,
}

/// <summary>
/// A video-conference join link attached to an event, normalized across providers.
/// Null on the owning <see cref="CalendarEvent"/> when the event isn't an online meeting.
/// </summary>
/// <param name="JoinUrl">The URL rendered as a "Join" button. Sanitized/validated before render.</param>
/// <param name="Provider">Normalized provider classification.</param>
/// <param name="ProviderDisplayName">Raw provider label, e.g. "Google Meet" or "Microsoft Teams".</param>
public sealed record OnlineMeeting(
    string JoinUrl,
    OnlineMeetingProvider Provider,
    string? ProviderDisplayName);
