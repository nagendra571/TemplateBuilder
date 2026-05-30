using Ganss.Xss;

namespace TemplateBuilder.Application.Services;

public class HtmlSanitizerService : IHtmlSanitizerService
{
    private static readonly System.Text.RegularExpressions.Regex AllowedDataUri = new(
        @"^data:image/(png|jpeg|gif|webp);base64,",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] {
            "p", "div", "span", "strong", "em", "b", "i", "u", "s", "del", "ins",
            "sub", "sup", "blockquote", "pre", "code",
            "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "br", "hr",
            "table", "thead", "tbody", "tr", "th", "td", "colgroup", "col", "caption",
            "figure", "figcaption", "a", "img" })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("src");
        _sanitizer.AllowedAttributes.Add("alt");
        _sanitizer.AllowedAttributes.Add("width");
        _sanitizer.AllowedAttributes.Add("height");
        _sanitizer.AllowedAttributes.Add("colspan");
        _sanitizer.AllowedAttributes.Add("rowspan");
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("style");

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("mailto");

        _sanitizer.FilterUrl += (sender, args) =>
        {
            if (args.OriginalUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
                !AllowedDataUri.IsMatch(args.OriginalUrl))
            {
                args.SanitizedUrl = null;
            }
        };
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
