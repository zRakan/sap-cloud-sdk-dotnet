using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Details;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Edm;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>Ports of Java's <c>ServiceDetailsResolverTest</c> and <c>AllowedFunctionsResolverTest</c>.</summary>
public sealed class ServiceDetailsTests
{
    private static string Resolver(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "ServiceDetailsResolverTest", name);

    private static string Allowed(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "AllowedFunctionsResolverTest", name);

    private static async Task<ServiceDetails> Details(string edmx, string? swagger = null, string? defaultBasePath = null)
    {
        var path = Resolver(edmx);
        var content = swagger is not null && File.Exists(Resolver(swagger)) ? await File.ReadAllBytesAsync(Resolver(swagger)) : null;
        return ServiceDetailsResolver.Create(path, await CsdlReader.LoadAsync(path), content, defaultBasePath);
    }

    [Theory]
    [InlineData("v2S4StyleUrl.json", "/sap/opu/odata/my/custom/path/CompleteBusinessObject")]
    [InlineData("v3S4StyleUrl.json", "/sap/opu/odata/my/custom/path/CompleteBusinessObject")]
    [InlineData("v2Baseline.json", "/othercloud/sap/opu/odata/my/custom/path/CompleteBusinessObject")]
    [InlineData("v3Baseline.json", "/othercloud/sap/opu/odata/my/custom/path/CompleteBusinessObject")]
    [InlineData("v2NoBasePath.json", "/")]
    [InlineData("v3NoServersUrl.json", "/")]
    public async Task Service_path_comes_from_the_swagger_file(string swagger, string expected) =>
        Assert.Equal(expected, (await Details("Baseline.edmx", swagger)).ServiceUrl);

    [Fact]
    public async Task Missing_swagger_file_falls_back_to_the_atom_link() =>
        Assert.Equal("/sap/opu/odata/sap/SomeService", (await Details("Baseline.edmx", "does-not-exist.json")).ServiceUrl);

    [Theory]
    [InlineData("Baseline.edmx", "/custom/base/path/CustomBusinessObjectNamespace")]
    [InlineData("NoNamespace.edmx", "/custom/base/path/NoNamespace")]
    [InlineData("EmptyNamespace.edmx", "/custom/base/path/EmptyNamespace")]
    [InlineData("MultipleSchemas.edmx", "/custom/base/path/CustomBusinessService")]
    [InlineData("MultipleNamespace.edmx", "/custom/base/path/SomeService1")]
    public async Task Service_path_is_default_base_path_plus_namespace(string edmx, string expected) =>
        Assert.Equal(expected, (await Details(edmx, defaultBasePath: "/custom/base/path/")).ServiceUrl);

    [Theory]
    [InlineData("Baseline.edmx", "/sap/opu/odata/sap/SomeService")]
    [InlineData("DoubleAtomLink.edmx", "/sap/opu/odata/sap/SomeService1")]
    [InlineData("MultipleSchemas.edmx", "/sap/opu/odata/sap/SomeService1")]
    [InlineData("ShortS4StyleAtomLink.edmx", "/sap/opu/odata/sap/SomeService")]
    [InlineData("LongS4StyleAtomLink.edmx", "/sap/opu/odata/sap/SomeService;v=0002")]
    public async Task Service_path_comes_from_the_atom_link(string edmx, string expected) =>
        Assert.Equal(expected, (await Details(edmx)).ServiceUrl);

    [Theory]
    [InlineData("WithoutAtomLink.edmx")]
    [InlineData("MalformedUrl.edmx")]
    public async Task Service_path_cannot_be_determined(string edmx) =>
        await Assert.ThrowsAnyAsync<ODataGeneratorException>(() => Details(edmx));

    [Fact]
    public async Task Swagger_without_meaningful_server_urls_is_rejected()
    {
        var exception = await Assert.ThrowsAsync<ODataGeneratorReadException>(() => Details("Baseline.edmx", "v3NoMeaningfulUrl.json"));

        Assert.Contains("servers", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("v2Baseline.json")]
    [InlineData("v3Baseline.json")]
    public async Task Swagger_without_deprecation_flag_is_not_deprecated(string swagger)
    {
        var details = await Details("Baseline.edmx", swagger);

        Assert.False(details.IsDeprecated);
        Assert.Null(details.StateInfo);
    }

    [Theory]
    [InlineData("v2DeprecatedService.json")]
    [InlineData("v3DeprecatedService.json")]
    public async Task Swagger_with_deprecation_flag_is_deprecated(string swagger)
    {
        var details = await Details("Baseline.edmx", swagger);

        Assert.True(details.IsDeprecated);
        Assert.Equal("Deprecated", details.StateInfo!.State);
        Assert.Equal("AUG-2019", details.StateInfo.DeprecationDate);
        Assert.Equal("1908", details.StateInfo.DeprecationRelease);
        Assert.Equal("https://api.sap.com/api/SOME_OTHER_API", details.StateInfo.SuccessorApi);
    }

    [Fact]
    public async Task Swagger_documentation_is_read()
    {
        var details = await Details("Baseline.edmx", "v3Baseline.json");

        Assert.Equal("Service Java Class and Package Name", details.Info!.Title);
        Assert.Equal("Some description text for service class javadocs.", details.Info.Description);
        Assert.Equal("1", details.Info.Version);
        Assert.Equal("http://any.url", details.ExternalDocs!.Url);
        var overview = Assert.Single(details.ExtOverview!);
        Assert.Equal("table entry", overview.Name);
        Assert.Equal(["some text", "some text", "<a href='http://any.url'>link text for URL</a>"], overview.Values);
    }

    [Fact]
    public async Task A_json_file_without_swagger_version_is_rejected()
    {
        var path = Allowed("Baseline.edmx");
        var document = await CsdlReader.LoadAsync(path);
        var swagger = await File.ReadAllBytesAsync(Allowed("Baseline.json"));

        Assert.Throws<ODataGeneratorReadException>(() => ServiceDetailsResolver.Create(path, document, swagger, null));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // allowed functions

    private static async Task<Dictionary<string, SortedSet<ApiFunction>>> Functions(string edmx, string? swagger)
    {
        var path = Allowed(edmx);
        var model = CsdlReader.Read(await CsdlReader.LoadAsync(path), requireODataV4: false);
        var content = swagger is not null && File.Exists(swagger) ? await File.ReadAllBytesAsync(swagger) : null;
        return AllowedFunctionsResolver.Find(model, content, _ => { });
    }

    [Fact]
    public async Task Allowed_functions_come_from_the_paths_of_a_valid_swagger_file()
    {
        var details = await Functions("Baseline.edmx", Allowed("Baseline.json"));

        Assert.Equal(["FullEntitySet", "SomeEntitySet"], details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            [ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete, ApiFunction.Navigate],
            details["SomeEntitySet"]);
        Assert.Equal([ApiFunction.Update, ApiFunction.Delete, ApiFunction.Navigate], details["FullEntitySet"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("some/non/existing/path.json")]
    public async Task Allowed_functions_come_from_the_annotations_without_a_swagger_file(string? swagger)
    {
        var details = await Functions("Baseline.edmx", swagger);

        Assert.Equal(["FullEntitySet", "SomeEntitySet"], details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Navigate], details["SomeEntitySet"]);
        Assert.Equal(
            [ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete, ApiFunction.Navigate],
            details["FullEntitySet"]);
    }

    [Fact]
    public async Task Unexpected_annotation_values_allow_everything_and_warn()
    {
        var warnings = new List<string>();
        var path = Allowed("InvalidAttributeValue.edmx");
        var model = CsdlReader.Read(await CsdlReader.LoadAsync(path), requireODataV4: false);

        var details = AllowedFunctionsResolver.Find(model, null, warnings.Add);

        Assert.Equal(["SomeEntitySet"], details.Keys);
        Assert.Equal(
            [ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete, ApiFunction.Navigate],
            details["SomeEntitySet"]);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Unrelated_annotations_are_ignored()
    {
        var details = await Functions("UnrelatedAnnotations.edmx", null);

        Assert.Equal(["FieldValues", "TestEntity"], details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Navigate], details["TestEntity"]);
        Assert.Equal([ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Navigate], details["FieldValues"]);
    }

    [Fact]
    public async Task Annotations_for_a_subset_of_the_entity_sets()
    {
        var details = await Functions("PartlyNoAnnotations.edmx", null);

        Assert.Equal(["Config", "PageConfig", "SchemaSizes", "TableSizes", "UserConfig"], details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Navigate], details["Config"]);
        Assert.Equal(
            [ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete, ApiFunction.Navigate],
            details["PageConfig"]);
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Update, ApiFunction.Navigate], details["SchemaSizes"]);
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Navigate], details["TableSizes"]);
        Assert.Equal(
            [ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete, ApiFunction.Navigate],
            details["UserConfig"]);
    }
}
