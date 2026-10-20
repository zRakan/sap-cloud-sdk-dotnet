using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Loads destinations from an environment variable (default name <c>destinations</c>) that holds a JSON array of objects,
/// each with a <c>name</c> and further destination properties.
/// </summary>
/// <remarks>
/// <para>
/// All JSON values become string properties (non-string values keep their JSON text). The JSON is parsed with
/// <see cref="System.Text.Json"/> (trailing commas and comments are accepted); Java's json-sanitizer, which also repairs
/// other malformed JSON, is not ported.
/// </para>
/// <para>
/// Destinations of type <c>RFC</c> are returned as plain <see cref="DefaultDestination"/>s, because RFC is not supported
/// by this SDK; all others become <see cref="DefaultHttpDestination"/>s.
/// </para>
/// </remarks>
public sealed class EnvVarDestinationLoader : IDestinationLoader
{
    private const string DefaultDestinationVariableName = "destinations";

    private static readonly ILogger Log = SdkLogging.CreateLogger<EnvVarDestinationLoader>();

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly Func<string, string?> _environmentVariableAccessor;
    private readonly Func<string, IDestination> _destinationParser;
    private readonly string _variableName;

    /// <summary>Creates a loader that reads the process environment variable <c>destinations</c>.</summary>
    public EnvVarDestinationLoader()
        : this(null, null, null)
    {
    }

    /// <summary>Creates a loader with a custom variable source and name.</summary>
    /// <param name="environmentVariableAccessor">Reads a variable; defaults to the process environment.</param>
    /// <param name="variableName">The variable name; defaults to <c>destinations</c>.</param>
    public EnvVarDestinationLoader(Func<string, string?>? environmentVariableAccessor, string? variableName)
        : this(environmentVariableAccessor, null, variableName)
    {
    }

    /// <summary>Creates a loader with custom variable source, destination parser and variable name.</summary>
    /// <param name="environmentVariableAccessor">Reads a variable; defaults to the process environment.</param>
    /// <param name="destinationParser">Finds a destination by name; defaults to parsing the JSON array.</param>
    /// <param name="variableName">The variable name; defaults to <c>destinations</c>.</param>
    public EnvVarDestinationLoader(
        Func<string, string?>? environmentVariableAccessor,
        Func<string, IDestination>? destinationParser,
        string? variableName)
    {
        _environmentVariableAccessor = environmentVariableAccessor ?? Environment.GetEnvironmentVariable;
        _destinationParser = destinationParser ?? ExtractDestination;
        _variableName = variableName ?? DefaultDestinationVariableName;
    }

    /// <inheritdoc/>
    public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(destinationName);
        return Result.Of(() => _destinationParser(destinationName));
    }

    /// <inheritdoc/>
    public Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGetDestination(destinationName, options));

    /// <summary>Parses all destinations of the environment variable.</summary>
    /// <returns>The destinations, or the reason why the variable could not be read.</returns>
    public Result<IReadOnlyList<IDestination>> TryGetAllDestinations() =>
        Result.Of(() =>
        {
            using var document = ReadDocument();
            return (IReadOnlyList<IDestination>)[.. document.RootElement.EnumerateArray().Select(ParseDestination)];
        });

    private JsonDocument ReadDocument()
    {
        var destinationsJson = _environmentVariableAccessor(_variableName);
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Trying to extract destinations from environment variables.");
        }
        if (string.IsNullOrEmpty(destinationsJson))
        {
            throw new DestinationNotFoundException(null, $"Could not find environment variable for name '{_variableName}'.");
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(destinationsJson, JsonOptions);
        }
        catch (JsonException e)
        {
            throw new DestinationAccessException($"The environment variable with name {_variableName} could not be parsed.", e);
        }
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            document.Dispose();
            throw new DestinationAccessException(
                $"The content of the '{_variableName}' environment variable has to be a JSON Array.");
        }
        return document;
    }

    private IDestination ExtractDestination(string destinationName)
    {
        using var document = ReadDocument();
        foreach (var node in document.RootElement.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("name", out var nameNode))
            {
                // The raw JSON is printed as in Java, but may contain secrets: log only the property names.
                var names = node.ValueKind == JsonValueKind.Object
                    ? string.Join(", ", node.EnumerateObject().Select(p => p.Name))
                    : node.ValueKind.ToString();
                throw new DestinationAccessException(
                    "Destination with the following properties lacks a property \"name\": " + names);
            }
            if (string.Equals(AsText(nameNode), destinationName, StringComparison.Ordinal))
            {
                return ParseDestination(node);
            }
        }
        throw new DestinationNotFoundException(
            destinationName,
            $"Could not find a destination with the name {destinationName} in the environment variable with name {_variableName}.");
    }

    private static IDestination ParseDestination(JsonElement destinationNode)
    {
        var map = new Dictionary<string, object>(StringComparer.Ordinal);
        if (destinationNode.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in destinationNode.EnumerateObject())
            {
                map[field.Name] = AsText(field.Value);
            }
        }
        var properties = DefaultDestination.FromMap(map).Build();
        if (properties.TryGet(DestinationProperty.Type, out var type) && type == DestinationType.Rfc)
        {
            return properties;
        }
        return DefaultHttpDestination.FromProperties(properties).Build();
    }

    private static string AsText(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
}
