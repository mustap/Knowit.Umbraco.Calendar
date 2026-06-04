using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Models;
using Knowit.Umbraco.Calendar.Core.Services;
using NSubstitute;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class DefaultCalendarServiceTests
{
    private static readonly CalendarQuery Query =
        new(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task Routes_to_provider_matching_the_calendar_provider_key()
    {
        var expected = new List<CalendarEvent>
        {
            new("e1", "Event", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
                false, null, null, null, [], null),
        };

        var google = Substitute.For<ICalendarProvider>();
        google.Key.Returns("google");
        google.GetEventsAsync("cal-1", Arg.Any<CalendarQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var microsoft = Substitute.For<ICalendarProvider>();
        microsoft.Key.Returns("microsoft");

        var service = new DefaultCalendarService([google, microsoft]);

        var result = await service.GetEventsAsync(
            new CalendarRef("google", "cal-1", "Team"), Query);

        Assert.Same(expected, result);
        await microsoft.DidNotReceive().GetEventsAsync(
            Arg.Any<string>(), Arg.Any<CalendarQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_for_unknown_provider_key()
    {
        var google = Substitute.For<ICalendarProvider>();
        google.Key.Returns("google");
        var service = new DefaultCalendarService([google]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetEventsAsync(new CalendarRef("nope", "x", "X"), Query));
    }

    [Fact]
    public async Task Aggregates_calendars_across_all_providers()
    {
        var google = Substitute.For<ICalendarProvider>();
        google.Key.Returns("google");
        google.ListCalendarsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CalendarRef> { new("google", "g1", "Google Cal") });

        var microsoft = Substitute.For<ICalendarProvider>();
        microsoft.Key.Returns("microsoft");
        microsoft.ListCalendarsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CalendarRef> { new("microsoft", "m1", "MS Cal") });

        var service = new DefaultCalendarService([google, microsoft]);

        var calendars = await service.GetAvailableCalendarsAsync();

        Assert.Equal(2, calendars.Count);
        Assert.Contains(calendars, c => c.ProviderKey == "google" && c.CalendarId == "g1");
        Assert.Contains(calendars, c => c.ProviderKey == "microsoft" && c.CalendarId == "m1");
    }
}
