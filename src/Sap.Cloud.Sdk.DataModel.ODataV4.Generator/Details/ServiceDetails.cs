using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Details;

internal sealed record ServiceInfo(string? Title, string? Description, string? Version);

internal sealed record ServiceExternalDocs(string? Description, string? Url);

internal sealed record ServiceExternalOverview(string? Name, IReadOnlyList<string> Values);

internal sealed record ServiceStateInfo(string? State, string? DeprecationRelease, string? SuccessorApi, string? DeprecationDate);

/// <summary>What the generator knows about a service besides its EDM: the service path and, from a swagger file, the documentation.</summary>
/// <remarks>Port of <c>ServiceDetails</c>.</remarks>
internal sealed class ServiceDetails
{
    public string ServiceUrl { get; set; } = "/";

    public ServiceInfo? Info { get; set; }

    public ServiceExternalDocs? ExternalDocs { get; set; }

    public string? MinErpVersion { get; set; }

    public IReadOnlyList<ServiceExternalOverview>? ExtOverview { get; set; }

    public ServiceStateInfo? StateInfo { get; set; }

    public bool IsDeprecated => string.Equals(StateInfo?.State, "Deprecated", StringComparison.Ordinal);
}

/// <summary>
/// Determines the service path (and the documentation) of a service from, in this order, a swagger/OpenAPI file next
/// to the EDMX, the default base path plus the namespace of the EDMX, and the <c>atom:link</c> of the EDMX.
/// </summary>
/// <remarks>Port of <c>ServiceDetailsResolver</c>, <c>ServiceDetailsParser</c> and the swagger specific details classes.</remarks>
internal static partial class ServiceDetailsResolver
{
    private static readonly Regex s_markdownLink = MarkdownLinkRegex();

    public static ServiceDetails Create(string edmxPath, XDocument edmx, byte[]? swagger, string? defaultBasePath, Action<string>? debug = null)
    {
        ServiceDetails? details = null;
        if (swagger is not null)
        {
            details = ReadFromSwagger(swagger);
        }
        else
        {
            debug?.Invoke("No swagger file given. Trying to use the default base path.");
        }

        if (details is null && defaultBasePath is not null)
        {
            var serviceName = ReadServiceName(edmxPath, edmx);
            details = new ServiceDetails { ServiceUrl = defaultBasePath + serviceName };
        }
        else if (details is null)
        {
            debug?.Invoke("No default base path given. Trying to read the base path from the metadata file.");
        }

        details ??= ReadFromAtomLink(edmx);

        return details ?? throw new ODataGeneratorReadException(
            "Could not determine a valid default service path. A default service path is the prefix of the HTTP "
            + "query component used for all outgoing queries of the service to be generated (if no custom service path is given).\n"
            + "Tried to determine the service path with the following priority:\n"
            + " 1. The 'basePath' field in the swagger file\n"
            + " 2. The default base path (if given) concatenated with the namespace defined in the metadata file. "
            + "In case of no namespace, the file name is considered as the service name.\n"
            + " 3. The atom:link field in the metadata file.");
    }

    private static string ReadServiceName(string edmxPath, XDocument edmx)
    {
        var namespaces = edmx.Descendants().Where(e => e.Name.LocalName == "Schema").ToList();
        var first = namespaces
            .Select(s => s.Attribute("Namespace")?.Value)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return first ?? Path.GetFileNameWithoutExtension(edmxPath);
    }

    private static ServiceDetails? ReadFromAtomLink(XDocument edmx)
    {
        foreach (var schema in edmx.Descendants().Where(e => e.Name.LocalName == "Schema"))
        {
            var link = schema.Elements().FirstOrDefault(e =>
                e.Name.LocalName == "link"
                && e.Name.NamespaceName == "http://www.w3.org/2005/Atom"
                && e.Attribute("rel")?.Value == "self");
            var href = link?.Attribute("href")?.Value;
            if (href is not null)
            {
                return new ServiceDetails { ServiceUrl = ExtractPathFromMetadataUrl(href) };
            }
        }

        return null;
    }

    private static string ExtractPathFromMetadataUrl(string href)
    {
        var url = href.StartsWith("http", StringComparison.Ordinal) ? href : "https://" + href;
        if (url.Any(c => char.IsWhiteSpace(c) || c is '<' or '>' or '"' or '{' or '}' or '|' or '\\' or '^' or '`'))
        {
            throw new ODataGeneratorReadException("Could not parse the atom:link url " + href);
        }

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var rest = schemeEnd < 0 ? url : url[(schemeEnd + 3)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        var path = slash < 0 ? string.Empty : rest[slash..];
        var end = path.IndexOfAny(['?', '#']);
        if (end >= 0)
        {
            path = path[..end];
        }

        const string Suffix = "/$metadata";
        return path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ? path[..^Suffix.Length] : path;
    }

    private static ServiceDetails ReadFromSwagger(byte[] content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException e)
        {
            throw new ODataGeneratorReadException("Could not read the swagger file: " + e.Message, e);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ODataGeneratorReadException("The swagger file does not contain a JSON object.");
            }

            var details = new ServiceDetails();
            if (root.TryGetProperty("openapi", out var openapi)
                && openapi.ValueKind == JsonValueKind.String
                && OpenApiVersionRegex().IsMatch(openapi.GetString()!))
            {
                details.ServiceUrl = ReadOpenApiServerUrl(root);
            }
            else if (root.TryGetProperty("swagger", out var swagger)
                && swagger.ValueKind == JsonValueKind.String
                && swagger.GetString() == "2.0")
            {
                details.ServiceUrl = root.TryGetProperty("basePath", out var basePath) && basePath.ValueKind == JsonValueKind.String
                    ? basePath.GetString()!
                    : "/";
            }
            else
            {
                throw new ODataGeneratorReadException(
                    "The given swagger is no valid Swagger 2.0 (missing or invalid 'swagger' property) or OpenAPI 3.x.x (missing or invalid 'openapi' property).");
            }

            const string PrefixToRemove = "/s4hanacloud";
            if (details.ServiceUrl.StartsWith(PrefixToRemove, StringComparison.OrdinalIgnoreCase))
            {
                details.ServiceUrl = details.ServiceUrl[PrefixToRemove.Length..];
            }

            if (root.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object)
            {
                details.Info = new ServiceInfo(
                    String(info, "title"),
                    String(info, "description") is { } description ? MultipleSpaceRegex().Replace(description, " ") : null,
                    String(info, "version"));
            }

            if (root.TryGetProperty("externalDocs", out var docs) && docs.ValueKind == JsonValueKind.Object)
            {
                details.ExternalDocs = new ServiceExternalDocs(String(docs, "description"), String(docs, "url")?.Trim());
            }

            details.MinErpVersion = String(root, "x-sap-software-min-version");
            if (root.TryGetProperty("x-sap-ext-overview", out var overview) && overview.ValueKind == JsonValueKind.Array)
            {
                details.ExtOverview = overview.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.Object).Select(ReadOverview).ToList();
            }

            if (root.TryGetProperty("x-sap-stateInfo", out var state) && state.ValueKind == JsonValueKind.Object)
            {
                details.StateInfo = new ServiceStateInfo(
                    String(state, "state"),
                    String(state, "deprecationrelease"),
                    String(state, "successorApi"),
                    String(state, "deprecationdate"));
            }

            return details;
        }
    }

    private static ServiceExternalOverview ReadOverview(JsonElement element)
    {
        var values = new List<string>();
        if (element.TryGetProperty("values", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in array.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    values.Add(value.GetString()!);
                }
                else if (value.ValueKind == JsonValueKind.Object)
                {
                    var format = String(value, "format");
                    var text = String(value, "text");
                    if (format == "markdown" && text is not null)
                    {
                        var description = MultipleSpaceRegex().Replace(s_markdownLink.Replace(text, "$1"), " ");
                        var url = s_markdownLink.Replace(text, "$2");
                        values.Add($"<a href='{url}'>{description}</a>");
                    }
                    else if (text is not null)
                    {
                        values.Add(text);
                    }
                }
            }
        }

        return new ServiceExternalOverview(String(element, "name"), values);
    }

    private static string ReadOpenApiServerUrl(JsonElement root)
    {
        var urls = new List<string>();
        if (root.TryGetProperty("servers", out var servers) && servers.ValueKind == JsonValueKind.Array)
        {
            foreach (var server in servers.EnumerateArray())
            {
                if (server.ValueKind == JsonValueKind.Object && String(server, "url") is { } url)
                {
                    urls.Add(url);
                }
            }
        }

        if (urls.Count == 0)
        {
            // according to the OpenAPI 3.x.x spec, an empty array of servers can be interpreted as '/'
            return "/";
        }

        var fromTemplate = urls.Select(ExtractPath).FirstOrDefault(p => p is not null);
        if (fromTemplate is not null)
        {
            return fromTemplate;
        }

        var sandbox = urls.Where(u => u.Contains("sandbox.api.sap.com", StringComparison.Ordinal)).Select(PathOf).FirstOrDefault();
        return sandbox ?? throw new ODataGeneratorReadException("Could not read base path from the 'servers' property of the swagger file.");
    }

    // expected format: <any>://<any>/base/path/taken; in case of <any>://<any> this will produce "/"
    private static string? ExtractPath(string url)
    {
        if (!url.Contains("://", StringComparison.Ordinal) || url.EndsWith("://", StringComparison.Ordinal))
        {
            return null;
        }

        var afterScheme = url.Split("://")[1];
        var slash = afterScheme.IndexOf('/', StringComparison.Ordinal);
        var path = slash > 0 ? afterScheme[slash..] : string.Empty;
        return path.Length == 0 ? "/" : path;
    }

    private static string PathOf(string url)
    {
        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var rest = schemeEnd < 0 ? url : url[(schemeEnd + 3)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? string.Empty : rest[slash..];
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex(@"^3\.\d+\.\d+$", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex OpenApiVersionRegex();

    [GeneratedRegex(" {2,}", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MultipleSpaceRegex();

    [GeneratedRegex("""^\[(.*)\]\((.*) "(.*)"\)$""", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MarkdownLinkRegex();
}
