using Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>Ports of <c>ServiceDetailsResolverTest</c> and <c>AllowedFunctionsResolverTest</c>, using the same input files.</summary>
public class ServiceDetailsTests
{
    private static string Resolver(string name) => Path.Combine(Harness.JavaData, "ServiceDetailsResolverTest", name);

    private static string Allowed(string name) => Path.Combine(Harness.JavaData, "AllowedFunctionsResolverTest", name);

    /// <summary>The model is not needed for most of the checks; the resolver files refer to types of other namespaces, so only the XML shell is read.</summary>
    private static EdmModel Model(string file)
    {
        // The test files declare entity sets whose types do not resolve (see JavaInputCompilationTests); read them leniently for the details.
        var text = File.ReadAllText(file);
        var model = new EdmModel();
        var document = System.Xml.Linq.XDocument.Parse(text.TrimStart('﻿'));
        foreach (var schema in document.Descendants().Where(e => e.Name.LocalName == "Schema"))
        {
            model.Schemas.Add(new EdmSchema
            {
                Namespace = (string?)schema.Attribute("Namespace") ?? "",
                SelfLink = schema.Elements().Where(e => e.Name.LocalName == "link" && (string?)e.Attribute("rel") == "self").Select(e => (string?)e.Attribute("href")).FirstOrDefault(),
            });
        }
        return model;
    }

    private static ServiceDetails Details(string edmx, string? swagger, string? defaultBasePath = null) =>
        new ServiceDetailsResolver(defaultBasePath).CreateServiceDetails(edmx, Model(edmx), swagger);

    [Fact]
    public void Swagger2_S4Style_UsesBasePathWithoutCloudPrefix() =>
        Assert.Equal("/sap/opu/odata/my/custom/path/CompleteBusinessObject", Details(Resolver("Baseline.edmx"), Resolver("v2S4StyleUrl.json")).ServiceUrl);

    [Fact]
    public void OpenApi3_S4Style_UsesServerPathWithoutCloudPrefix() =>
        Assert.Equal("/sap/opu/odata/my/custom/path/CompleteBusinessObject", Details(Resolver("Baseline.edmx"), Resolver("v3S4StyleUrl.json")).ServiceUrl);

    [Fact]
    public void Swagger2_Custom() =>
        Assert.Equal("/othercloud/sap/opu/odata/my/custom/path/CompleteBusinessObject", Details(Resolver("Baseline.edmx"), Resolver("v2Baseline.json")).ServiceUrl);

    [Fact]
    public void Swagger2_WithoutBasePath_IsRoot() =>
        Assert.Equal("/", Details(Resolver("Baseline.edmx"), Resolver("v2NoBasePath.json")).ServiceUrl);

    [Fact]
    public void OpenApi3_Custom() =>
        Assert.Equal("/othercloud/sap/opu/odata/my/custom/path/CompleteBusinessObject", Details(Resolver("Baseline.edmx"), Resolver("v3Baseline.json")).ServiceUrl);

    [Fact]
    public void MissingSwaggerFile_FallsBackToMetadata() =>
        Assert.Equal("/sap/opu/odata/sap/SomeService", Details(Resolver("Baseline.edmx"), "some/non/existing/path.json").ServiceUrl);

    [Theory]
    [InlineData("Baseline.edmx", "/custom/base/path/CustomBusinessObjectNamespace")]
    [InlineData("NoNamespace.edmx", "/custom/base/path/NoNamespace")]
    [InlineData("EmptyNamespace.edmx", "/custom/base/path/EmptyNamespace")]
    [InlineData("MultipleSchemas.edmx", "/custom/base/path/CustomBusinessService")]
    [InlineData("MultipleNamespace.edmx", "/custom/base/path/SomeService1")]
    public void DefaultBasePath_IsFollowedByTheNamespaceOrFileName(string file, string expected) =>
        Assert.Equal(expected, Details(Resolver(file), null, "/custom/base/path/").ServiceUrl);

    [Theory]
    [InlineData("Baseline.edmx", "/sap/opu/odata/sap/SomeService")]
    [InlineData("DoubleAtomLink.edmx", "/sap/opu/odata/sap/SomeService1")]
    [InlineData("MultipleSchemas.edmx", "/sap/opu/odata/sap/SomeService1")]
    [InlineData("ShortS4StyleAtomLink.edmx", "/sap/opu/odata/sap/SomeService")]
    [InlineData("LongS4StyleAtomLink.edmx", "/sap/opu/odata/sap/SomeService;v=0002")]
    public void AtomLink_GivesTheServicePath(string file, string expected) =>
        Assert.Equal(expected, Details(Resolver(file), null).ServiceUrl);

    [Fact]
    public void WithoutAnyBasePath_Throws() =>
        Assert.Throws<ODataGeneratorReadException>(() => Details(Resolver("WithoutAtomLink.edmx"), null));

    [Fact]
    public void MalformedAtomLink_Throws() =>
        Assert.Throws<ODataGeneratorReadException>(() => Details(Resolver("MalformedUrl.edmx"), null));

    [Theory]
    [InlineData("v2Baseline.json")]
    [InlineData("v3Baseline.json")]
    public void SwaggerWithoutDeprecation_IsNotDeprecated(string swagger)
    {
        var details = Details(Resolver("Baseline.edmx"), Resolver(swagger));
        Assert.False(details.IsDeprecated);
        Assert.Null(details.StateInfo);
        Assert.Equal("Service Java Class and Package Name", details.Title);
    }

    [Theory]
    [InlineData("v2DeprecatedService.json")]
    [InlineData("v3DeprecatedService.json")]
    public void SwaggerWithDeprecation_IsDeprecated(string swagger)
    {
        var details = Details(Resolver("Baseline.edmx"), Resolver(swagger));
        Assert.True(details.IsDeprecated);
        var state = details.StateInfo!;
        Assert.Equal("AUG-2019", state.DeprecationDate);
        Assert.Equal("1908", state.DeprecationRelease);
        Assert.Equal("https://api.sap.com/api/SOME_OTHER_API", state.SuccessorApi);
    }

    [Fact]
    public void OpenApi3_WithoutMeaningfulServerUrls_Throws() =>
        Assert.Throws<ODataGeneratorReadException>(() => Details(Resolver("Baseline.edmx"), Resolver("v3NoMeaningfulUrl.json")));

    [Fact]
    public void OpenApi3_WithoutServers_IsRoot() =>
        Assert.Equal("/", Details(Resolver("Baseline.edmx"), Resolver("v3NoServersUrl.json")).ServiceUrl);

    [Fact]
    public void Swagger_ReadsTextsAndFormatsExtensionOverview()
    {
        var details = Details(Resolver("Baseline.edmx"), Resolver("v3Baseline.json"));
        Assert.Equal("Some description text for service class javadocs.", details.Description);
        Assert.Equal("link text for URL", details.ExternalDocsDescription);
        Assert.Equal("http://any.url", details.ExternalDocsUrl);
        var overview = Assert.Single(details.ExtOverview!);
        Assert.Equal("table entry", overview.Name);
        Assert.Equal(["some text", "some text", "<a href='http://any.url'>link text for URL</a>"], overview.Values);
    }

    [Fact]
    public void InvalidSwaggerFile_Throws()
    {
        var file = Path.Combine(Harness.NewTemporaryDirectory(), "x.json");
        File.WriteAllText(file, "{\"foo\": 1}");
        Assert.Throws<ODataGeneratorReadException>(() => ServiceDetails.ParseSwaggerFile(file));
        File.WriteAllText(file, "not json");
        Assert.Throws<ODataGeneratorReadException>(() => ServiceDetails.ParseSwaggerFile(file));
    }

    [Fact]
    public void FormatDescriptionText_CollapsesSpaces() => Assert.Equal("a b c", ServiceDetails.FormatDescriptionText("a   b  c"));

    // ----- Allowed functions ----------------------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, IReadOnlyCollection<ApiFunction>> Functions(string edmx, string? swagger)
    {
        var model = EdmxReader.ReadFile(edmx);
        return AllowedFunctionsResolver.FindAllowedFunctions(model, swagger, []);
    }

    [Fact]
    public void AllowedFunctions_FromSwaggerFile()
    {
        // Baseline.edmx declares entity sets of an unknown namespace: read the entity sets leniently through the details test helper.
        var model = LenientModel(Allowed("Baseline.edmx"));
        var functions = AllowedFunctionsResolver.FindAllowedFunctions(model, Allowed("Baseline.json"), []);

        Assert.Equal(["FullEntitySet", "SomeEntitySet"], functions.Keys.Order());
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete], functions["SomeEntitySet"]);
        Assert.Equal([ApiFunction.Update, ApiFunction.Delete], functions["FullEntitySet"]);
    }

    [Theory]
    [InlineData("some/non/existing/path.json")]
    [InlineData(null)]
    public void AllowedFunctions_WithoutSwaggerFile_UseSapAnnotations(string? swagger)
    {
        var functions = AllowedFunctionsResolver.FindAllowedFunctions(LenientModel(Allowed("Baseline.edmx")), swagger, []);

        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey], functions["SomeEntitySet"]);
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete], functions["FullEntitySet"]);
    }

    [Fact]
    public void AllowedFunctions_InvalidSapAttribute_Throws() =>
        Assert.Throws<ODataGeneratorReadException>(() => AllowedFunctionsResolver.FindAllowedFunctions(LenientModel(Allowed("InvalidAttributeValue.edmx")), null, []));

    [Fact]
    public void AllowedFunctions_FromODataAnnotations_IgnoreAnnotationsOfOtherTargets()
    {
        var functions = Functions(Allowed("UnrelatedAnnotations.edmx"), null);

        Assert.Equal(["FieldValues", "TestEntity"], functions.Keys.Order());
        Assert.Equal([ApiFunction.ReadByKey, ApiFunction.Create], functions["TestEntity"]);
        Assert.Equal([ApiFunction.ReadByKey, ApiFunction.Create], functions["FieldValues"]);
    }

    [Fact]
    public void AllowedFunctions_AnnotationsForASubsetOfEntitySets_LeaveTheOthersFullyEnabled()
    {
        var functions = AllowedFunctionsResolver.FindAllowedFunctions(LenientModel(Allowed("PartlyNoAnnotations.edmx")), null, []);

        Assert.Equal(["Config", "PageConfig", "SchemaSizes", "TableSizes", "UserConfig"], functions.Keys.Order());
        Assert.Equal([ApiFunction.ReadByKey], functions["Config"]);
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete], functions["PageConfig"]);
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey], functions["SchemaSizes"]);
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey], functions["TableSizes"]);
        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey, ApiFunction.Create, ApiFunction.Update, ApiFunction.Delete], functions["UserConfig"]);
    }

    [Fact]
    public void AllowedFunctions_SwaggerUnsupportedOperations_AreReported()
    {
        var swagger = Path.Combine(Harness.NewTemporaryDirectory(), "s.json");
        File.WriteAllText(swagger, "{\"paths\": {\"/Foos\": {\"get\": {}, \"put\": {}}, \"/Foos(1)\": {\"get\": {}}}}");
        var warnings = new List<string>();

        var functions = AllowedFunctionsResolver.FindAllowedFunctions(LenientModel(Allowed("Baseline.edmx")), swagger, warnings);

        Assert.Equal([ApiFunction.Read, ApiFunction.ReadByKey], functions["Foos"]);
        Assert.Contains(warnings, w => w.Contains("'put'", StringComparison.Ordinal));
    }

    /// <summary>Entity sets and annotations without resolving their types (some resolver test files declare types in other namespaces).</summary>
    private static EdmModel LenientModel(string file)
    {
        var document = System.Xml.Linq.XDocument.Parse(File.ReadAllText(file).TrimStart('﻿'));
        var model = new EdmModel { Source = document };
        var schema = new EdmSchema { Namespace = "Lenient" };
        var container = new EdmEntityContainer { Name = "C" };
        foreach (var set in document.Descendants().Where(e => e.Name.LocalName == "EntitySet"))
        {
            var entitySet = new EdmEntitySet { Name = (string)set.Attribute("Name")!, EntityTypeName = (string)set.Attribute("EntityType")! };
            foreach (var attribute in set.Attributes().Where(a => a.Name.NamespaceName == EdmxReader.SapNamespace))
            {
                entitySet.SapAttributes[attribute.Name.LocalName] = attribute.Value;
            }
            container.EntitySets.Add(entitySet);
        }
        schema.Containers.Add(container);
        model.Schemas.Add(schema);
        return model;
    }
}
