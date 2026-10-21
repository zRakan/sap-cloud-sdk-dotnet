namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// Determines the default service path (and, from a swagger file, title, description and deprecation) of a service.
/// Port of <c>ServiceDetailsResolver</c>.
/// </summary>
/// <remarks>
/// Priority of the service path: 1. the <c>basePath</c> (swagger 2) or <c>servers</c> (OpenAPI 3) of a swagger file next to the EDMX file;
/// 2. the default base path option concatenated with the namespace of the metadata (or the file name if there is none);
/// 3. the <c>atom:link rel="self"</c> of the metadata.
/// </remarks>
internal sealed class ServiceDetailsResolver(string? defaultBasePath)
{
    internal ServiceDetails CreateServiceDetails(string edmxPath, EdmModel model, string? swaggerPath)
    {
        ServiceDetails? details = null;
        if (swaggerPath is not null && File.Exists(swaggerPath))
        {
            details = ServiceDetails.ParseSwaggerFile(swaggerPath);
        }

        if (details is null && defaultBasePath is not null)
        {
            var serviceName = model.FirstNamespace;
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                // no namespace specified, consider the file name as the service name
                serviceName = Path.GetFileNameWithoutExtension(edmxPath);
            }
            details = new ServiceDetails { ServiceUrl = defaultBasePath + serviceName };
        }

        if (details is null)
        {
            var selfLink = model.Schemas.Select(s => s.SelfLink).FirstOrDefault(l => l is not null);
            if (selfLink is not null)
            {
                details = new ServiceDetails { ServiceUrl = AdjustMetadataServiceUrl(selfLink) };
            }
        }

        if (details?.ServiceUrl is null)
        {
            throw new ODataGeneratorReadException(
                "Could not determine a valid default service path. A default service path is the prefix of the HTTP query component used for all outgoing queries of the service to be generated (if no custom service path is given).\n"
                + "Tried to determine the service path with the following priority:\n"
                + " 1. The 'basePath' field in the swagger file\n"
                + " 2. The default base path (if given via the options) concatenated with the namespace defined in metadata file. In case of no namespace, filename is considered as the service name.\n"
                + " 3. The atom:link field in the metadata file.");
        }
        return details;
    }

    private static string AdjustMetadataServiceUrl(string fullServiceUrl)
    {
        var parsable = fullServiceUrl.StartsWith("http", StringComparison.Ordinal) ? fullServiceUrl : "https://" + fullServiceUrl;
        if (parsable.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '<' or '>' or '"' or '{' or '}' or '|' or '\\' or '^' or '`'))
        {
            throw new ODataGeneratorReadException("Could not parse the atom:link url " + fullServiceUrl);
        }

        var schemeEnd = parsable.IndexOf("://", StringComparison.Ordinal);
        var rest = schemeEnd >= 0 ? parsable[(schemeEnd + 3)..] : parsable;
        var pathStart = rest.IndexOf('/', StringComparison.Ordinal);
        var path = pathStart >= 0 ? rest[pathStart..] : "";
        var query = path.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            path = path[..query];
        }
        return NamingUtils.RemoveEndIgnoreCase(Uri.UnescapeDataString(path), "/$metadata");
    }
}
