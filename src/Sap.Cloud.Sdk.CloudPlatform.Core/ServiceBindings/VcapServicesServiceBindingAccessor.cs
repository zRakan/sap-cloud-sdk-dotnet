using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

/// <summary>Reads service bindings from the Cloud Foundry <c>VCAP_SERVICES</c> environment variable.</summary>
public sealed class VcapServicesServiceBindingAccessor : IServiceBindingAccessor
{
    /// <summary>The name of the environment variable.</summary>
    public const string VcapServices = "VCAP_SERVICES";

    private static readonly ILogger Log = SdkLogging.CreateLogger<VcapServicesServiceBindingAccessor>();

    private readonly Func<string, string?> _environmentReader;

    /// <summary>Creates an accessor that reads the process environment.</summary>
    public VcapServicesServiceBindingAccessor()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Creates an accessor that reads environment variables using <paramref name="environmentReader"/>.</summary>
    /// <param name="environmentReader">Returns the value of an environment variable, or <see langword="null"/>.</param>
    public VcapServicesServiceBindingAccessor(Func<string, string?> environmentReader)
    {
        ArgumentNullException.ThrowIfNull(environmentReader);
        _environmentReader = environmentReader;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IServiceBinding> GetServiceBindings()
    {
        var vcapServices = _environmentReader(VcapServices);
        if (string.IsNullOrWhiteSpace(vcapServices))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(vcapServices);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Log.LogWarning("Ignoring {Variable}: it is not a JSON object.", VcapServices);
                return [];
            }

            var bindings = new List<IServiceBinding>();
            foreach (var service in document.RootElement.EnumerateObject())
            {
                if (service.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (var binding in service.Value.EnumerateArray().Where(b => b.ValueKind == JsonValueKind.Object))
                {
                    bindings.Add(new DefaultServiceBinding(
                        JsonValues.GetString(binding, "name"),
                        JsonValues.GetString(binding, "label") ?? service.Name,
                        JsonValues.GetString(binding, "plan"),
                        JsonValues.GetStrings(binding, "tags"),
                        binding.TryGetProperty("credentials", out var credentials)
                            ? JsonValues.ToDictionary(credentials)
                            : new Dictionary<string, object?>().AsReadOnly()));
                }
            }
            return bindings;
        }
        catch (JsonException e)
        {
            Log.LogWarning(e, "Ignoring {Variable}: it is not valid JSON.", VcapServices);
            return [];
        }
    }
}
