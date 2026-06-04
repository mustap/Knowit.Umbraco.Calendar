using Microsoft.Extensions.Options;

namespace Knowit.Umbraco.Calendar.Core.Configuration;

/// <summary>
/// Validates <see cref="CalendarOptions"/> at startup (wired with <c>ValidateOnStart()</c> in the
/// composer). Fails fast when an enabled provider is missing its credentials.
/// </summary>
public sealed class CalendarOptionsValidator : IValidateOptions<CalendarOptions>
{
    public ValidateOptionsResult Validate(string? name, CalendarOptions options)
    {
        var errors = new List<string>();

        if (options.DefaultCacheSeconds < 0)
        {
            errors.Add("Knowit:Calendar:DefaultCacheSeconds must be >= 0.");
        }

        if (options.Google.Enabled && string.IsNullOrWhiteSpace(options.Google.ServiceAccountJson))
        {
            errors.Add("Google is enabled but Knowit:Calendar:Google:ServiceAccountJson is missing.");
        }

        if (options.Microsoft.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.Microsoft.TenantId))
            {
                errors.Add("Microsoft is enabled but Knowit:Calendar:Microsoft:TenantId is missing.");
            }

            if (string.IsNullOrWhiteSpace(options.Microsoft.ClientId))
            {
                errors.Add("Microsoft is enabled but Knowit:Calendar:Microsoft:ClientId is missing.");
            }

            if (string.IsNullOrWhiteSpace(options.Microsoft.ClientSecret))
            {
                errors.Add("Microsoft is enabled but Knowit:Calendar:Microsoft:ClientSecret is missing.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
