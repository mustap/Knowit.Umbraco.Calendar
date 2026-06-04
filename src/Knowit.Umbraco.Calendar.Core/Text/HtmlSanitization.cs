using System.Text.Encodings.Web;
using Ganss.Xss;

namespace Knowit.Umbraco.Calendar.Core.Text;

/// <summary>
/// Produces safe HTML for the <c>Description</c> field so it can be rendered directly
/// (e.g. <c>@Html.Raw(...)</c> or by a headless client) without XSS risk.
/// </summary>
public static class HtmlSanitization
{
    // HtmlSanitizer.Sanitize is thread-safe as long as the configuration isn't mutated.
    private static readonly HtmlSanitizer Sanitizer = new();

    /// <summary>Sanitizes HTML, stripping scripts/handlers and unsafe attributes.</summary>
    public static string? SanitizeHtml(string? html)
        => string.IsNullOrEmpty(html) ? html : Sanitizer.Sanitize(html);

    /// <summary>HTML-encodes plain text so it is safe to treat as HTML.</summary>
    public static string? EncodeText(string? text)
        => string.IsNullOrEmpty(text) ? text : HtmlEncoder.Default.Encode(text);
}
