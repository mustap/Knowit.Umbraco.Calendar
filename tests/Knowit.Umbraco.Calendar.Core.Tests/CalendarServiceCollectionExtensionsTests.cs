using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Configuration;
using Knowit.Umbraco.Calendar.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class CalendarServiceCollectionExtensionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Resolves_cached_facade_with_no_providers_when_all_disabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKnowitCalendar(Config(new Dictionary<string, string?>
        {
            ["Knowit:Calendar:Google:Enabled"] = "false",
            ["Knowit:Calendar:Microsoft:Enabled"] = "false",
        }));

        using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<ICalendarService>();
        Assert.IsType<CachingCalendarService>(service);
        Assert.Empty(provider.GetServices<ICalendarProvider>());
    }

    [Fact]
    public async Task Facade_returns_no_calendars_when_no_providers_registered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKnowitCalendar(Config(new Dictionary<string, string?>()));

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ICalendarService>();

        Assert.Empty(await service.GetAvailableCalendarsAsync());
    }

    [Fact]
    public void Binds_cache_settings_from_configuration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKnowitCalendar(Config(new Dictionary<string, string?>
        {
            ["Knowit:Calendar:DefaultCacheSeconds"] = "120",
            ["Knowit:Calendar:StaleFallbackHours"] = "12",
        }));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CalendarOptions>>().Value;

        Assert.Equal(120, options.DefaultCacheSeconds);
        Assert.Equal(12, options.StaleFallbackHours);
    }
}
