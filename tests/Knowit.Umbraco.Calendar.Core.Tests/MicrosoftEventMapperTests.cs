using Knowit.Umbraco.Calendar.Core.Models;
using Knowit.Umbraco.Calendar.Core.Providers.Microsoft;
using MGraph = Microsoft.Graph.Models;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class MicrosoftEventMapperTests
{
    [Fact]
    public void Maps_timed_event_as_utc()
    {
        var e = new MGraph.Event
        {
            Id = "evt1",
            Subject = "Sprint review",
            BodyPreview = "Demo of the increment",
            Location = new MGraph.Location { DisplayName = "Room 2" },
            Organizer = new MGraph.Recipient
            {
                EmailAddress = new MGraph.EmailAddress { Name = "Bob", Address = "bob@contoso.com" },
            },
            WebLink = "https://outlook.office.com/evt1",
            Start = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T07:00:00.0000000", TimeZone = "UTC" },
            End = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T08:00:00.0000000", TimeZone = "UTC" },
            Categories = ["Work", "Important"],
        };

        var result = MicrosoftEventMapper.Map(e);

        Assert.Equal("evt1", result.Id);
        Assert.Equal("Sprint review", result.Title);
        Assert.Equal("Demo of the increment", result.Description);
        Assert.Equal("Room 2", result.Location);
        Assert.Equal("Bob", result.OrganizerName);
        Assert.False(result.IsAllDay);
        Assert.Equal(new DateTimeOffset(2026, 5, 13, 7, 0, 0, TimeSpan.Zero), result.Start);
        Assert.Equal(new DateTimeOffset(2026, 5, 13, 8, 0, 0, TimeSpan.Zero), result.End);
        Assert.Equal(["Work", "Important"], result.Categories);
        Assert.Null(result.OnlineMeeting);
        Assert.Null(result.Recurrence);
    }

    [Fact]
    public void Maps_all_day_flag()
    {
        var e = new MGraph.Event
        {
            Id = "allday",
            Subject = "Company holiday",
            IsAllDay = true,
            Start = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T00:00:00.0000000", TimeZone = "UTC" },
            End = new MGraph.DateTimeTimeZone { DateTime = "2026-05-14T00:00:00.0000000", TimeZone = "UTC" },
        };

        var result = MicrosoftEventMapper.Map(e);

        Assert.True(result.IsAllDay);
        Assert.Equal(new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero), result.Start);
    }

    [Fact]
    public void Maps_teams_online_meeting()
    {
        var e = new MGraph.Event
        {
            Id = "teams1",
            Subject = "Standup",
            IsOnlineMeeting = true,
            OnlineMeetingProvider = MGraph.OnlineMeetingProviderType.TeamsForBusiness,
            OnlineMeeting = new MGraph.OnlineMeetingInfo { JoinUrl = "https://teams.microsoft.com/l/meetup-join/xyz" },
            Start = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T07:00:00.0000000", TimeZone = "UTC" },
            End = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T07:15:00.0000000", TimeZone = "UTC" },
        };

        var meeting = MicrosoftEventMapper.Map(e).OnlineMeeting;

        Assert.NotNull(meeting);
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/xyz", meeting!.JoinUrl);
        Assert.Equal(OnlineMeetingProvider.MicrosoftTeams, meeting.Provider);
        Assert.Equal("Microsoft Teams", meeting.ProviderDisplayName);
    }

    [Fact]
    public void Returns_null_meeting_when_no_join_url()
    {
        var e = new MGraph.Event
        {
            Id = "offline",
            Subject = "Desk work",
            IsOnlineMeeting = false,
            Start = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T07:00:00.0000000", TimeZone = "UTC" },
            End = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T08:00:00.0000000", TimeZone = "UTC" },
        };

        Assert.Null(MicrosoftEventMapper.Map(e).OnlineMeeting);
    }

    [Fact]
    public void Maps_occurrence_as_recurrence_instance()
    {
        var e = new MGraph.Event
        {
            Id = "occ1",
            Subject = "Weekly (occurrence)",
            Type = MGraph.EventType.Occurrence,
            SeriesMasterId = "series-abc",
            Start = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T07:00:00.0000000", TimeZone = "UTC" },
            End = new MGraph.DateTimeTimeZone { DateTime = "2026-05-13T07:30:00.0000000", TimeZone = "UTC" },
        };

        var recurrence = MicrosoftEventMapper.Map(e).Recurrence;

        Assert.NotNull(recurrence);
        Assert.True(recurrence!.IsInstance);
        Assert.Equal("series-abc", recurrence.SeriesId);
    }
}
