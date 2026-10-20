using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

/// <summary>
/// Reads Kubernetes service bindings mounted as files, following the
/// <see href="https://servicebinding.io/spec/core/1.1.0/">servicebinding.io</see> layout used by the SAP BTP service
/// operator: one directory per binding below <c>SERVICE_BINDING_ROOT</c>, one file per entry.
/// </summary>
/// <remarks>
/// The file <c>type</c> holds the service name, <c>plan</c> the plan and <c>tags</c> a JSON array of tags. If a
/// <c>.metadata</c> file is present, its <c>metaDataProperties</c> and <c>credentialProperties</c> decide which files
/// are metadata and which credential values are JSON. Without it, every other file is a string credential.
/// </remarks>
public sealed class ServiceBindingIoServiceBindingAccessor : IServiceBindingAccessor
{
    /// <summary>The environment variable naming the root directory.</summary>
    public const string ServiceBindingRoot = "SERVICE_BINDING_ROOT";

    private static readonly ILogger Log = SdkLogging.CreateLogger<ServiceBindingIoServiceBindingAccessor>();
    private static readonly HashSet<string> WellKnownMetadata =
        new(["type", "plan", "tags", "label", "instance_name", "instance_guid", "provider", ".metadata"],
            StringComparer.Ordinal);

    private readonly Func<string, string?> _environmentReader;

    /// <summary>Creates an accessor that reads the process environment.</summary>
    public ServiceBindingIoServiceBindingAccessor()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Creates an accessor that reads environment variables using <paramref name="environmentReader"/>.</summary>
    /// <param name="environmentReader">Returns the value of an environment variable, or <see langword="null"/>.</param>
    public ServiceBindingIoServiceBindingAccessor(Func<string, string?> environmentReader)
    {
        ArgumentNullException.ThrowIfNull(environmentReader);
        _environmentReader = environmentReader;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IServiceBinding> GetServiceBindings()
    {
        var root = _environmentReader(ServiceBindingRoot);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        var bindings = new List<IServiceBinding>();
        foreach (var directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            try
            {
                bindings.Add(ReadBinding(directory));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Log.LogWarning(e, "Skipping service binding in {Directory}.", directory);
            }
        }
        return bindings;
    }

    private static DefaultServiceBinding ReadBinding(string directory)
    {
        // Kubernetes mounts files as symlinks into hidden "..data" directories; only read top-level, non-hidden entries.
        var files = Directory.EnumerateFiles(directory)
            .Where(f => !Path.GetFileName(f).StartsWith("..", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetFileName(f), f => File.ReadAllText(f).Trim(), StringComparer.Ordinal);

        var metadataNames = new HashSet<string>(WellKnownMetadata, StringComparer.Ordinal);
        var jsonCredentials = new HashSet<string>(StringComparer.Ordinal);
        if (files.TryGetValue(".metadata", out var metadataJson))
        {
            using var metadata = JsonDocument.Parse(metadataJson);
            foreach (var (name, _) in ReadProperties(metadata.RootElement, "metaDataProperties"))
            {
                metadataNames.Add(name);
            }
            foreach (var (name, format) in ReadProperties(metadata.RootElement, "credentialProperties"))
            {
                metadataNames.Remove(name);
                if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    jsonCredentials.Add(name);
                }
            }
        }

        var credentials = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, content) in files.Where(f => !metadataNames.Contains(f.Key)))
        {
            if (jsonCredentials.Contains(name))
            {
                using var json = JsonDocument.Parse(content);
                credentials[name] = JsonValues.ToValue(json.RootElement);
            }
            else
            {
                credentials[name] = content;
            }
        }

        return new DefaultServiceBinding(
            files.GetValueOrDefault("instance_name") ?? Path.GetFileName(directory),
            files.GetValueOrDefault("type") ?? files.GetValueOrDefault("label"),
            files.GetValueOrDefault("plan"),
            ParseTags(files.GetValueOrDefault("tags")),
            credentials.AsReadOnly());
    }

    private static IEnumerable<(string Name, string? Format)> ReadProperties(JsonElement metadata, string propertyName)
    {
        if (!metadata.TryGetProperty(propertyName, out var properties) || properties.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }
        foreach (var property in properties.EnumerateArray())
        {
            var name = JsonValues.GetString(property, "name");
            if (name is not null)
            {
                yield return (name, JsonValues.GetString(property, "format"));
            }
        }
    }

    private static IReadOnlyList<string> ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return [];
        }
        if (tags.StartsWith('['))
        {
            using var json = JsonDocument.Parse(tags);
            return [.. json.RootElement.EnumerateArray()
                .Where(t => t.ValueKind == JsonValueKind.String)
                .Select(t => t.GetString()!)];
        }
        return [.. tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
