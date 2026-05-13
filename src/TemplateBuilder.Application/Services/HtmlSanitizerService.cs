using Ganss.Xss;

namespace TemplateBuilder.Application.Services;

public class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "div", "span", "strong", "em", "b", "i", "u",
            "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "br", "hr",
            "table", "thead", "tbody", "tr", "th", "td", "a", "img" })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("src");
        _sanitizer.AllowedAttributes.Add("class");
        // style excluded: CSS injection vector

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("http");

        var allowedDataUri = new System.Text.RegularExpressions.Regex(
            @"^data:image/(png|jpeg|gif|webp);base64,",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        _sanitizer.FilterUrl += (sender, args) =>
        {
            if (args.OriginalUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
                !allowedDataUri.IsMatch(args.OriginalUrl))
            {
                args.SanitizedUrl = null;
            }
        };
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
