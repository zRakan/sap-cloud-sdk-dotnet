using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>
/// Loads generated code of a service other than the sample (Guid keys, all primitive types, complex types, navigations, function
/// imports) and runs it against the runtime library.
/// </summary>
public class RuntimeSmokeTests
{
    private const string Payload =
        """
        {
          "KeyPropertyGuid": "123e4567-e89b-12d3-a456-426614174000",
          "KeyPropertyString": "key",
          "StringProperty": "text",
          "BooleanProperty": true,
          "Int16Property": 16,
          "Int32Property": 32,
          "Int64Property": "64",
          "DecimalProperty": "12.50",
          "DoubleProperty": 1.5,
          "TimeProperty": "PT13H20M0S",
          "DateTimeProperty": "/Date(694224000000)/",
          "DateTimeOffSetProperty": "/Date(694224000000)/",
          "ByteProperty": 8,
          "SByteProperty": -8,
          "BinaryProperty": "AQID",
          "ComplexTypeProperty": { "StringProperty": "complex", "Int32Property": 5 },
          "to_MultiLink": { "results": [ { "KeyProperty": "m1" }, { "KeyProperty": "m2" } ] },
          "to_SingleLink": { "KeyProperty": "s1" },
          "CustomField": "extension"
        }
        """;

    private static Assembly CompileAndLoad(string directory)
    {
        var compilation = Harness.CreateCompilation(directory);
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, Harness.Describe(emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray());
    }

    private static T Get<T>(object instance, string property) =>
        (T)instance.GetType().GetProperty(property)!.GetValue(instance)!;

    [Fact]
    public async Task GeneratedEntity_DeserializesSerializesAndTracksChanges()
    {
        var file = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "testService", "input", "test_service.edmx");
        var (_, output) = await Harness.GenerateAsync(
            file,
            options => options.NameSource = NameSource.Name,
            Path.Combine(Path.GetDirectoryName(file)!, "serviceNameMappings.properties"));
        var assembly = CompileAndLoad(output);
        var entityType = assembly.GetType("Generated.Namespaces.Test.TestEntityV2")!;

        var entity = JsonSerializer.Deserialize(Payload, entityType, ODataVdmJsonOptions.Get())!;

        Assert.Equal(Guid.Parse("123e4567-e89b-12d3-a456-426614174000"), Get<Guid?>(entity, "KeyPropertyGuid"));
        Assert.Equal("text", Get<string?>(entity, "StringProperty"));
        Assert.Equal(64L, Get<long?>(entity, "Int64Property"));
        Assert.Equal(12.5m, Get<decimal?>(entity, "DecimalProperty"));
        Assert.Equal(new TimeOnly(13, 20), Get<TimeOnly?>(entity, "TimeProperty"));
        Assert.Equal(new DateTime(1992, 1, 1, 0, 0, 0), Get<DateTime?>(entity, "DateTimeProperty"));
        Assert.Equal((byte)8, Get<byte?>(entity, "ByteProperty"));
        Assert.Equal((sbyte)-8, Get<sbyte?>(entity, "SByteProperty"));
        Assert.Equal(new byte[] { 1, 2, 3 }, Get<byte[]?>(entity, "BinaryProperty"));
        var complex = Get<object>(entity, "ComplexTypeProperty");
        Assert.Equal("complex", Get<string?>(complex, "StringProperty"));
        Assert.Equal(5, Get<int?>(complex, "Int32Property"));

        var multi = (System.Collections.IEnumerable)entityType.GetMethod("GetMultiLinkIfPresent")!.Invoke(entity, null)!;
        Assert.Equal(2, multi.Cast<object>().Count());
        Assert.Equal("s1", Get<string?>(entityType.GetMethod("GetSingleLinkIfPresent")!.Invoke(entity, null)!, "KeyProperty"));
        Assert.Equal("extension", Assert.Single(Get<IReadOnlyDictionary<string, object?>>(entity, "CustomFields")).Value);

        // deserialization does not count as a change
        var changed = (IReadOnlyDictionary<string, object?>)entityType.GetMethod("GetChangedFields")!.Invoke(entity, null)!;
        Assert.Empty(changed);
        entityType.GetProperty("StringProperty")!.SetValue(entity, "changed");
        changed = (IReadOnlyDictionary<string, object?>)entityType.GetMethod("GetChangedFields")!.Invoke(entity, null)!;
        Assert.Equal("changed", changed["StringProperty"]);

        var json = JsonSerializer.Serialize(entity, entityType, ODataVdmJsonOptions.Get());
        using var document = JsonDocument.Parse(json);
        Assert.Equal("changed", document.RootElement.GetProperty("StringProperty").GetString());
        Assert.Equal("64", document.RootElement.GetProperty("Int64Property").GetString());
        Assert.Equal("complex", document.RootElement.GetProperty("ComplexTypeProperty").GetProperty("StringProperty").GetString());
    }

    [Fact]
    public async Task GeneratedBuilderAndFields_Work()
    {
        var file = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "testService", "input", "test_service.edmx");
        var (_, output) = await Harness.GenerateAsync(file, options => options.NameSource = NameSource.Name, Path.Combine(Path.GetDirectoryName(file)!, "serviceNameMappings.properties"));
        var assembly = CompileAndLoad(output);
        var entityType = assembly.GetType("Generated.Namespaces.Test.TestEntityV2")!;

        var builder = entityType.GetMethod("Builder")!.Invoke(null, null)!;
        builder.GetType().GetMethod("KeyPropertyString")!.Invoke(builder, ["k"]);
        builder.GetType().GetMethod("Int16Property")!.Invoke(builder, [(short?)7]);
        var entity = builder.GetType().GetMethod("Build")!.Invoke(builder, null)!;

        Assert.Equal("k", Get<string?>(entity, "KeyPropertyString"));
        Assert.Equal((short)7, Get<short?>(entity, "Int16Property"));
        Assert.Empty((IReadOnlyDictionary<string, object?>)entityType.GetMethod("GetChangedFields")!.Invoke(entity, null)!);

        var fields = entityType.GetNestedType("Fields")!;
        Assert.NotNull(fields.GetField("All"));
        Assert.NotNull(fields.GetField("KeyPropertyGuid"));
        Assert.NotNull(fields.GetField("MultiLink"));
        var field = fields.GetField("StringProperty")!.GetValue(null)!;
        Assert.Equal("StringProperty", field.GetType().GetProperty("FieldName")!.GetValue(field));
    }

    [Fact]
    public async Task GeneratedService_CreatesRequestsForEntitiesAndFunctions()
    {
        var file = Path.Combine(Harness.JavaData, "oDataGeneratorIntegrationTest", "testService", "input", "test_service.edmx");
        var (_, output) = await Harness.GenerateAsync(file, options => options.NameSource = NameSource.Name, Path.Combine(Path.GetDirectoryName(file)!, "serviceNameMappings.properties"));
        var assembly = CompileAndLoad(output);
        var service = Activator.CreateInstance(assembly.GetType("Generated.Services.DefaultTestService")!)!;
        var interfaceType = assembly.GetType("Generated.Services.ITestService")!;

        var read = interfaceType.GetMethod("GetAllTestEntityV2")!.Invoke(service, null)!;
        var request = ((Sap.Cloud.Sdk.DataModel.OData.Helper.IFluentHelperRequest)read).ToRequest();
        Assert.Equal("/sap/opu/odata/sap/API_TEST_SRV", request.ServicePath);
        Assert.EndsWith("/API_TEST_SRV/A_TestEntity", request.GetRelativeUri().ToString().Split('?')[0].TrimEnd('/'), StringComparison.Ordinal);

        var byKey = interfaceType.GetMethod("GetTestEntityV2ByKey")!.Invoke(service, [(Guid?)Guid.Parse("123e4567-e89b-12d3-a456-426614174000"), "k"])!;
        var keyRequest = ((Sap.Cloud.Sdk.DataModel.OData.Helper.IFluentHelperRequest)byKey).ToRequest();
        Assert.Contains("KeyPropertyGuid=guid'123e4567-e89b-12d3-a456-426614174000',KeyPropertyString='k'", Uri.UnescapeDataString(keyRequest.GetRelativeUri().ToString()), StringComparison.Ordinal);

        var function = interfaceType.GetMethod("TestFunctionImportMultipleParams")!.Invoke(service, ["text", true])!;
        var functionRequest = ((Sap.Cloud.Sdk.DataModel.OData.Helper.IFluentHelperRequest)function).ToRequest();
        Assert.Contains("TestFunctionImportMultipleParams?StringParam='text'&BooleanParam=true", Uri.UnescapeDataString(functionRequest.GetRelativeUri().ToString()), StringComparison.Ordinal);
    }
}
