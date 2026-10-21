using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>An entry of the <c>x-sap-ext-overview</c> table of a swagger file.</summary>
internal sealed record ExternalOverview(string? Name, IReadOnlyList<string> Values);

/// <summary>The deprecation state of a service from <c>x-sap-stateInfo</c>.</summary>
internal sealed record StateInfo(bool IsDeprecated, string? DeprecationRelease, string? SuccessorApi, string? DeprecationDate);

/// <summary>What the generator knows about a service besides its metadata: path, title, description and state.</summary>
/// <remarks>Port of <c>ServiceDetails</c>, <c>ServiceDetailsSwagger2</c>, <c>ServiceDetailsOpenApi3</c> and <c>ServiceDetailsParser</c>.</remarks>
internal sealed partial class ServiceDetails
{
    private const string MarkdownLinkPattern = "^\\[(.*)\\]\\((.*) \"(.*)\"\\)$";

    [GeneratedRegex(" {2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleSpaces();

    [GeneratedRegex(MarkdownLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();

    internal string? ServiceUrl { get; set; }

    internal string? Title { get; init; }

    internal string? Description { get; init; }

    internal string? Version { get; init; }

    internal string? ExternalDocsDescription { get; init; }

    internal string? ExternalDocsUrl { get; init; }

    internal string? MinErpVersion { get; init; }

    internal IReadOnlyList<ExternalOverview>? ExtOverview { get; init; }

    internal StateInfo? StateInfo { get; init; }

    internal bool IsDeprecated => StateInfo is { IsDeprecated: true };

    /// <summary>Business hub descriptions contain extra spaces at random places.</summary>
    internal static string FormatDescriptionText(string rawText) => MultipleSpaces().Replace(rawText, " ");

    /// <summary>Parses a swagger 2.0 or OpenAPI 3.x file.</summary>
    /// <exception cref="ODataGeneratorReadException">The file is no valid swagger / OpenAPI document.</exception>
    internal static ServiceDetails ParseSwaggerFile(string path)
    {
        JsonDocument document;
        try
        {
            using var stream = File.OpenRead(path);
            document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new ODataGeneratorReadException("The swagger file cannot be read: " + path, e);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ODataGeneratorReadException("The swagger file is not a JSON object: " + path);
            }

            string? serviceUrl;
            if (root.TryGetProperty("openapi", out var openApi) && openApi.ValueKind == JsonValueKind.String
                && Regex.IsMatch(openApi.GetString()!, "^3\\.\\d+\\.\\d+$", RegexOptions.CultureInvariant))
            {
                serviceUrl = ReadOpenApi3ServiceUrl(root);
            }
            else if (root.TryGetProperty("swagger", out var swagger) && swagger.ValueKind == JsonValueKind.String && swagger.GetString() == "2.0")
            {
                serviceUrl = root.TryGetProperty("basePath", out var basePath) && basePath.ValueKind == JsonValueKind.String ? basePath.GetString() : "/";
            }
            else
            {
                throw new ODataGeneratorReadException(
                    "The given swagger is no valid Swagger 2.0 (missing or invalid 'swagger' property) or OpenAPI 3.x.x (missing or invalid 'openapi' property).");
            }

            // The S/4HANA Cloud API hub prefixes the paths of the cloud services.
            serviceUrl = serviceUrl is null ? null : NamingUtils.RemoveStartIgnoreCase(serviceUrl, "/s4hanacloud");
            return new ServiceDetails
            {
                ServiceUrl = serviceUrl,
                Title = Info(root, "title"),
                Description = Info(root, "description") is { } description ? FormatDescriptionText(description) : null,
                Version = Info(root, "version"),
                ExternalDocsDescription = Child(root, "externalDocs", "description"),
                ExternalDocsUrl = Child(root, "externalDocs", "url")?.Trim(),
                MinErpVersion = String(root, "x-sap-software-min-version"),
                ExtOverview = ReadExtOverview(root),
                StateInfo = ReadStateInfo(root),
            };
        }
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Info(JsonElement root, string name) => Child(root, "info", name);

    private static string? Child(JsonElement root, string parent, string name) =>
        root.TryGetProperty(parent, out var container) && container.ValueKind == JsonValueKind.Object ? String(container, name) : null;

    private static StateInfo? ReadStateInfo(JsonElement root)
    {
        if (!root.TryGetProperty("x-sap-stateInfo", out var state) || state.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return new StateInfo(
            string.Equals(String(state, "state"), "Deprecated", StringComparison.Ordinal),
            String(state, "deprecationrelease"),
            String(state, "successorApi"),
            String(state, "deprecationdate"));
    }

    private static List<ExternalOverview>? ReadExtOverview(JsonElement root)
    {
        if (!root.TryGetProperty("x-sap-ext-overview", out var overview) || overview.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var result = new List<ExternalOverview>();
        foreach (var entry in overview.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
        {
            var values = new List<string>();
            if (entry.TryGetProperty("values", out var valueArray) && valueArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var value in valueArray.EnumerateArray())
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
                            var description = FormatDescriptionText(MarkdownLink().Replace(text, "$1"));
                            var url = MarkdownLink().Replace(text, "$2");
                            values.Add($"<a href='{url}'>{description}</a>");
                        }
                        else if (text is not null)
                        {
                            values.Add(text);
                        }
                    }
                }
            }
            result.Add(new ExternalOverview(String(entry, "name"), values));
        }
        return result;
    }

    private static string ReadOpenApi3ServiceUrl(JsonElement root)
    {
        var serverUrls = new List<string>();
        if (root.TryGetProperty("servers", out var servers) && servers.ValueKind == JsonValueKind.Array)
        {
            foreach (var server in servers.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object))
            {
                if (String(server, "url") is { } url)
                {
                    serverUrls.Add(url);
                }
            }
        }

        // According to the OpenAPI 3.x.x spec, an empty array of servers can be interpreted as '/'.
        if (serverUrls.Count == 0)
        {
            return "/";
        }

        var fromTemplate = serverUrls.Select(ExtractPath).FirstOrDefault(p => p is not null);
        if (fromTemplate is not null)
        {
            return fromTemplate;
        }

        var sandbox = serverUrls.FirstOrDefault(s => s.Contains("sandbox.api.sap.com", StringComparison.Ordinal));
        if (sandbox is not null && Uri.TryCreate(sandbox, UriKind.Absolute, out var sandboxUri))
        {
            return sandboxUri.AbsolutePath;
        }
        throw new ODataGeneratorReadException("Could not read base path from the 'servers' property of the swagger file.");
    }

    // expected format: <any>://<any>/base/path/taken ; in case of <any>://<any> this produces "/"
    private static string? ExtractPath(string urlString)
    {
        if (!urlString.Contains("://", StringComparison.Ordinal) || urlString.EndsWith("://", StringComparison.Ordinal))
        {
            return null;
        }
        var afterScheme = urlString.Split("://")[1];
        var slash = afterScheme.IndexOf('/', StringComparison.Ordinal);
        var path = slash > 0 ? afterScheme[slash..] : "";
        return path.Length == 0 ? "/" : path;
    }
}
