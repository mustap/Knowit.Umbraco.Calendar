using Knowit.Umbraco.Calendar.Core.Abstractions;
using Knowit.Umbraco.Calendar.Core.Providers.Google;
using Knowit.Umbraco.Calendar.Core.Providers.Microsoft;
using Knowit.Umbraco.Calendar.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowit.Umbraco.Calendar.Core.Configuration;

/// <summary>
/// Registers the calendar core (options, providers, cached facade) into any DI container.
/// Used by the Umbraco composer, and usable directly from a plain host (worker/minimal API).
/// </summary>
public static class CalendarServiceCollectionExtensions
{
    public static IServiceCollection AddKnowitCalendar(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CalendarOptions.SectionName);

        services.AddOptions<CalendarOptions>()
            .Bind(section)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CalendarOptions>, CalendarOptionsValidator>();

        // In-process by default; a host-registered distributed cache (for load balancing) wins.
        services.AddDistributedMemoryCache();

        // Only register enabled providers — their constructors build live credentials.
        if (section.GetValue<bool>("Google:Enabled"))
        {
            services.AddSingleton<ICalendarProvider>(sp =>
                new GoogleCalendarProvider(sp.GetRequiredService<IOptions<CalendarOptions>>().Value.Google));
        }

        if (section.GetValue<bool>("Microsoft:Enabled"))
        {
            services.AddSingleton<ICalendarProvider>(sp =>
                new MicrosoftGraphProvider(sp.GetRequiredService<IOptions<CalendarOptions>>().Value.Microsoft));
        }

        services.AddSingleton<DefaultCalendarService>();

        services.AddSingleton<ICalendarService>(sp => new CachingCalendarService(
            sp.GetRequiredService<DefaultCalendarService>(),
            sp.GetRequiredService<IDistributedCache>(),
            sp.GetRequiredService<IOptions<CalendarOptions>>(),
            sp.GetRequiredService<ILogger<CachingCalendarService>>()));

        return services;
    }
}
