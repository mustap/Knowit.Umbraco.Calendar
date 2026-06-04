using Knowit.Umbraco.Calendar.Core.Models;
using Knowit.Umbraco.Calendar.Core.Providers.Google;
using GData = Google.Apis.Calendar.v3.Data;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class GoogleEventMapperTests
{
    [Fact]
    public void Maps_timed_event_and_normalizes_start_to_utc()
    {
        var e = new GData.Event
        {
            Id = "evt1",
            Summary = "Standup",
            Description = "Daily sync",
            Location = "Room A",
            HtmlLink = "https://calendar.google.com/evt1",
            Organizer = new GData.Event.OrganizerData { DisplayName = "Alice", Email = "alice@example.com" },
            // 09:00 at +02:00 == 07:00 UTC
            Start = new GData.EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.FromHours(2)) },
            End = new GData.EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(2026, 5, 13, 9, 30, 0, TimeSpan.FromHours(2)) },
        };

        var result = GoogleEventMapper.Map(e);

        Assert.Equal("evt1", result.Id);
        Assert.Equal("Standup", result.Title);
        Assert.Equal("Daily sync", result.Description);
        Assert.Equal("Room A", result.Location);
        Assert.Equal("Alice", result.OrganizerName);
        Assert.False(result.IsAllDay);
        Assert.Equal(new DateTimeOffset(2026, 5, 13, 7, 0, 0, TimeSpan.Zero), result.Start);
        Assert.Equal(TimeSpan.Zero, result.Start.Offset);
        Assert.Equal(new DateTimeOffset(2026, 5, 13, 7, 30, 0, TimeSpan.Zero), result.End);
        Assert.Null(result.OnlineMeeting);
        Assert.Null(result.Recurrence);
    }

    [Fact]
    public void Maps_all_day_event_from_date_only_fields()
    {
        var e = new GData.Event
        {
            Id = "allday",
            Summary = "Holiday",
            Start = new GData.EventDateTime { Date = "2026-05-13" },
            End = new GData.EventDateTime { Date = "2026-05-14" },
        };

        var result = GoogleEventMapper.Map(e);

        Assert.True(result.IsAllDay);
        Assert.Equal(new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero), result.Start);
        Assert.Equal(new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero), result.End);
    }

    [Fact]
    public void Prefers_video_conference_entry_point_over_phone()
    {
        var e = new GData.Event
        {
            Id = "meet1",
            Summary = "Planning",
            Start = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow },
            End = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow.AddHours(1) },
            ConferenceData = new GData.ConferenceData
            {
                EntryPoints =
                [
                    new GData.EntryPoint { EntryPointType = "phone", Uri = "tel:+4512345678" },
                    new GData.EntryPoint { EntryPointType = "video", Uri = "https://meet.google.com/abc-defg-hij" },
                ],
                ConferenceSolution = new GData.ConferenceSolution
                {
                    Name = "Google Meet",
                    Key = new GData.ConferenceSolutionKey { Type = "hangoutsMeet" },
                },
            },
        };

        var meeting = GoogleEventMapper.Map(e).OnlineMeeting;

        Assert.NotNull(meeting);
        Assert.Equal("https://meet.google.com/abc-defg-hij", meeting!.JoinUrl);
        Assert.Equal(OnlineMeetingProvider.GoogleMeet, meeting.Provider);
        Assert.Equal("Google Meet", meeting.ProviderDisplayName);
    }

    [Fact]
    public void Falls_back_to_legacy_hangout_link()
    {
        var e = new GData.Event
        {
            Id = "meet2",
            Summary = "Old style",
            Start = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow },
            End = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow.AddHours(1) },
            HangoutLink = "https://hangouts.google.com/legacy",
        };

        var meeting = GoogleEventMapper.Map(e).OnlineMeeting;

        Assert.NotNull(meeting);
        Assert.Equal("https://hangouts.google.com/legacy", meeting!.JoinUrl);
        Assert.Equal(OnlineMeetingProvider.GoogleMeet, meeting.Provider);
    }

    [Fact]
    public void Returns_null_meeting_when_not_online()
    {
        var e = new GData.Event
        {
            Id = "offline",
            Summary = "In person",
            Start = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow },
            End = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow.AddHours(1) },
        };

        Assert.Null(GoogleEventMapper.Map(e).OnlineMeeting);
    }

    [Fact]
    public void Maps_expanded_occurrence_as_recurrence_instance()
    {
        var e = new GData.Event
        {
            Id = "inst1",
            Summary = "Weekly sync (occurrence)",
            RecurringEventId = "series-123",
            Start = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow },
            End = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow.AddHours(1) },
        };

        var recurrence = GoogleEventMapper.Map(e).Recurrence;

        Assert.NotNull(recurrence);
        Assert.True(recurrence!.IsInstance);
        Assert.Equal("series-123", recurrence.SeriesId);
        Assert.Empty(recurrence.Rules);
    }

    [Fact]
    public void Maps_series_master_rules_when_not_expanded()
    {
        var e = new GData.Event
        {
            Id = "master1",
            Summary = "Weekly sync (master)",
            Recurrence = ["RRULE:FREQ=WEEKLY;BYDAY=MO"],
            Start = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow },
            End = new GData.EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow.AddHours(1) },
        };

        var recurrence = GoogleEventMapper.Map(e).Recurrence;

        Assert.NotNull(recurrence);
        Assert.False(recurrence!.IsInstance);
        Assert.Null(recurrence.SeriesId);
        Assert.Equal("RRULE:FREQ=WEEKLY;BYDAY=MO", Assert.Single(recurrence.Rules));
    }
}
