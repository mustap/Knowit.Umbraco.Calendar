namespace Knowit.Umbraco.Calendar.Core.Providers.Microsoft;

/// <summary>
/// Developer/admin-supplied configuration for the Microsoft provider. Bound from
/// <c>Knowit:Calendar:Microsoft</c>. Secrets must come from a secret store.
/// </summary>
/// <remarks>
/// App-level access uses application permission <c>Calendars.Read</c> (admin consent required),
/// which can read every mailbox in the tenant. Admins should constrain this with an
/// application access policy limiting the app to the intended mailboxes.
/// </remarks>
public sealed class MicrosoftOptions
{
    public bool Enabled { get; set; }

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret. Inject from user-secrets / Key Vault, never appsettings.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The calendars editors are allowed to pick.</summary>
    public IList<MicrosoftAllowedCalendar> AllowedCalendars { get; set; } = [];
}

/// <summary>A single Microsoft calendar exposed to editors.</summary>
public sealed class MicrosoftAllowedCalendar
{
    /// <summary>Mailbox UPN or object id that owns the calendar (e.g. events@contoso.com).</summary>
    public string Mailbox { get; set; } = string.Empty;

    /// <summary>Specific calendar id. Empty/"primary" targets the mailbox's default calendar.</summary>
    public string? CalendarId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}
