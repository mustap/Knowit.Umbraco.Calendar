using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Configuration;
using Knowit.Umbraco.Calendar.Core.Models;
using Knowit.Umbraco.Calendar.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class CachingCalendarServiceTests
{
    private static readonly CalendarRef Calendar = new("google", "team@knowit.dk", "Team");

    private static readonly CalendarQuery Query =
        new(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

    private static readonly IReadOnlyList<CalendarEvent> Events =
    [
        new("e1", "Event", null, null,
            new DateTimeOffset(2026, 5, 13, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 13, 8, 0, 0, TimeSpan.Zero),
            false, null, null, null, [], null),
    ];

    private static IDistributedCache NewCache()
        => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static CachingCalendarService NewService(ICalendarService inner, IDistributedCache cache)
        => new(inner, cache, Options.Create(new CalendarOptions()), NullLogger<CachingCalendarService>.Instance);

    [Fact]
    public async Task Second_call_is_served_from_cache_without_hitting_the_inner_service()
    {
        var inner = Substitute.For<ICalendarService>();
        inner.GetEventsAsync(Arg.Any<CalendarRef>(), Arg.Any<CalendarQuery>(), Arg.Any<CancellationToken>())
            .Returns(Events);
        var service = NewService(inner, NewCache());

        await service.GetEventsAsync(Calendar, Query);
        var second = await service.GetEventsAsync(Calendar, Query);

        Assert.Single(second);
        await inner.Received(1).GetEventsAsync(
            Arg.Any<CalendarRef>(), Arg.Any<CalendarQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Serves_stale_copy_when_provider_fails_after_a_prior_success()
    {
        var cache = NewCache();
        var inner = Substitute.For<ICalendarService>();
        inner.GetEventsAsync(Arg.Any<CalendarRef>(), Arg.Any<CalendarQuery>(), Arg.Any<CancellationToken>())
            .Returns(Events);
        var service = NewService(inner, cache);

        // Prime fresh + stale.
        await service.GetEventsAsync(Calendar, Query);

        // Expire the fresh entry, then make the provider fail.
        await cache.RemoveAsync(CachingCalendarService.BuildKey(Calendar, Query, stale: false));
        inner.GetEventsAsync(Arg.Any<CalendarRef>(), Arg.Any<CalendarQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("provider down"));

        var result = await service.GetEventsAsync(Calendar, Query);

        Assert.Single(result);
        Assert.Equal("e1", result[0].Id);
    }

    [Fact]
    public async Task Rethrows_when_provider_fails_with_no_cached_fallback()
    {
        var inner = Substitute.For<ICalendarService>();
        inner.GetEventsAsync(Arg.Any<CalendarRef>(), Arg.Any<CalendarQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("provider down"));
        var service = NewService(inner, NewCache());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetEventsAsync(Calendar, Query));
    }

    [Fact]
    public async Task Available_calendars_pass_through_to_inner()
    {
        var inner = Substitute.For<ICalendarService>();
        inner.GetAvailableCalendarsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CalendarRef> { Calendar });
        var service = NewService(inner, NewCache());

        var calendars = await service.GetAvailableCalendarsAsync();

        Assert.Single(calendars);
        await inner.Received(1).GetAvailableCalendarsAsync(Arg.Any<CancellationToken>());
    }
}
