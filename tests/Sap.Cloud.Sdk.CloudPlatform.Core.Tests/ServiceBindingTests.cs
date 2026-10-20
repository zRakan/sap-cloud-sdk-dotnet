using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Core.Tests;

public sealed class ServiceBindingTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("sap-sdk-bindings-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void VcapServices_IsParsed()
    {
        const string vcap = """
            {
              "xsuaa": [{
                "name": "my-xsuaa", "label": "xsuaa", "plan": "application", "tags": ["xsuaa"],
                "credentials": { "clientid": "id", "clientsecret": "s3cr3t-value", "identityzone": "provider", "nested": { "n": 1, "b": true }, "list": ["x"] }
              }],
              "destination": [{ "name": "my-destination", "credentials": { "uri": "https://destination" } }]
            }
            """;
        var bindings = new VcapServicesServiceBindingAccessor(name => name == "VCAP_SERVICES" ? vcap : null)
            .GetServiceBindings();

        Assert.Equal(2, bindings.Count);
        var xsuaa = bindings[0];
        Assert.Equal(("my-xsuaa", "xsuaa", "application"), (xsuaa.Name, xsuaa.ServiceName, xsuaa.ServicePlan));
        Assert.Equal(["xsuaa"], xsuaa.Tags);
        Assert.Equal("id", xsuaa.Credentials["clientid"]);
        var nested = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(xsuaa.Credentials["nested"]);
        Assert.Equal(1L, nested["n"]);
        Assert.Equal(true, nested["b"]);
        Assert.Equal(["x"], Assert.IsAssignableFrom<IReadOnlyList<object?>>(xsuaa.Credentials["list"]));

        // Without "label", the service name is the key in VCAP_SERVICES.
        Assert.Equal("destination", bindings[1].ServiceName);
        Assert.DoesNotContain("s3cr3t-value", xsuaa.ToString()!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    public void VcapServices_MissingOrInvalid_YieldsNoBindings(string? vcap)
    {
        Assert.Empty(new VcapServicesServiceBindingAccessor(_ => vcap).GetServiceBindings());
    }

    [Fact]
    public void ServiceBindingIo_ReadsFilesAndMetadata()
    {
        var binding = Directory.CreateDirectory(Path.Combine(_root, "my-xsuaa")).FullName;
        File.WriteAllText(Path.Combine(binding, "type"), "xsuaa");
        File.WriteAllText(Path.Combine(binding, "plan"), "application\n");
        File.WriteAllText(Path.Combine(binding, "tags"), """["xsuaa","auth"]""");
        File.WriteAllText(Path.Combine(binding, "clientid"), "id");
        File.WriteAllText(Path.Combine(binding, "json"), """{"a": 1}""");
        File.WriteAllText(Path.Combine(binding, ".metadata"), """
            { "metaDataProperties": [ { "name": "type", "format": "text" }, { "name": "plan", "format": "text" } ],
              "credentialProperties": [ { "name": "clientid", "format": "text" }, { "name": "json", "format": "json" } ] }
            """);

        var bindings = new ServiceBindingIoServiceBindingAccessor(name => name == "SERVICE_BINDING_ROOT" ? _root : null)
            .GetServiceBindings();

        var result = Assert.Single(bindings);
        Assert.Equal(("my-xsuaa", "xsuaa", "application"), (result.Name, result.ServiceName, result.ServicePlan));
        Assert.Equal(["xsuaa", "auth"], result.Tags);
        Assert.Equal("id", result.Credentials["clientid"]);
        Assert.Equal(1L, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Credentials["json"])["a"]);
        Assert.False(result.Credentials.ContainsKey("type"));
        Assert.False(result.Credentials.ContainsKey(".metadata"));
    }

    [Fact]
    public void Combining_AndCaching()
    {
        var calls = 0;
        var source = new DelegateAccessor(() =>
        {
            calls++;
            return [new DefaultServiceBinding("a", "s", null, [], new Dictionary<string, object?>())];
        });
        var accessor = new CachingServiceBindingAccessor(new CombiningServiceBindingAccessor(source, source));

        Assert.Equal(2, accessor.GetServiceBindings().Count);
        Assert.Equal(2, accessor.GetServiceBindings().Count);
        Assert.Equal(2, calls);
    }

    private sealed class DelegateAccessor(Func<IReadOnlyList<IServiceBinding>> get) : IServiceBindingAccessor
    {
        public IReadOnlyList<IServiceBinding> GetServiceBindings() => get();
    }
}
