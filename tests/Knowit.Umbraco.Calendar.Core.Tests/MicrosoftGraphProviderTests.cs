using Knowit.Umbraco.Calendar.Core.Providers.Microsoft;

namespace Knowit.Umbraco.Calendar.Core.Tests;

public class MicrosoftGraphProviderTests
{
    private static MicrosoftOptions OptionsWith(params MicrosoftAllowedCalendar[] calendars)
        => new()
        {
            Enabled = true,
            TenantId = "00000000-0000-0000-0000-000000000000",
            ClientId = "00000000-0000-0000-0000-000000000001",
            ClientSecret = "dummy-secret",
            AllowedCalendars = calendars,
        };

    [Fact]
    public async Task ListCalendars_composes_ids_from_mailbox_and_calendar()
    {
        var provider = new MicrosoftGraphProvider(OptionsWith(
            new MicrosoftAllowedCalendar { Mailbox = "events@knowit.dk", DisplayName = "Events" },
            new MicrosoftAllowedCalendar { Mailbox = "team@knowit.dk", CalendarId = "AAA", DisplayName = "Team" },
            new MicrosoftAllowedCalendar { Mailbox = "p@knowit.dk", CalendarId = "primary", DisplayName = "Primary" }));

        var refs = await provider.ListCalendarsAsync();

        Assert.Collection(
            refs,
            r =>
            {
                Assert.Equal("microsoft", r.ProviderKey);
                Assert.Equal("events@knowit.dk", r.CalendarId); // no calendar id -> mailbox only
                Assert.Equal("Events", r.DisplayName);
            },
            r => Assert.Equal("team@knowit.dk|AAA", r.CalendarId), // specific calendar -> composite
            r => Assert.Equal("p@knowit.dk", r.CalendarId));       // "primary" -> default -> mailbox only
    }
}
