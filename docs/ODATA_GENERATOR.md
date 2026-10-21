# OData code generators

`sap-odata-generator` generates typed C# clients from OData v2 or v4 EDMX metadata. It is the .NET counterpart of the
Java SDK's `odata-generator` / `odata-v4-generator` (CLI and Maven plugin) and produces code for
`Sap.Cloud.Sdk.DataModel.OData.Core` (v2) or `Sap.Cloud.Sdk.DataModel.ODataV4.Core` (v4).

## Install and run

```bash
dotnet pack src/Sap.Cloud.Sdk.DataModel.OData.Generator.Cli -o ./nupkg
dotnet tool install --global --add-source ./nupkg Sap.Cloud.Sdk.DataModel.OData.Generator.Cli

sap-odata-generator v2 --input edmx/API_BUSINESS_PARTNER.edmx --output Generated --namespace MyApp.S4
sap-odata-generator v4 --input edmx/ --output Generated --default-base-path /odata/v4/
```

Run `sap-odata-generator --help` for all options. Exit codes: `0` success, `1` a service failed (or warnings with
`--fail-on-warning`), `2` invalid arguments.

As in Java, every service needs a service path: from a swagger/OpenAPI file next to the EDMX (`basePath`), from
`--default-base-path` (+ namespace), or from the EDMX `atom:link`. Service names can be pinned in a
`serviceNameMappings.properties` file (`--service-name-mappings`).

## Use the generated code

```csharp
var service = new DefaultSdkGroceryStoreService();
var cheap = await service.GetAllProduct()
    .Select(Product.Fields.Name, Product.Fields.Price)
    .Filter(Product.Fields.Price.Lt(5m))
    .ExecuteAsync(destination);
```

## Programmatic use

The generators are libraries as well: `ODataGenerator.GenerateAsync(ODataGeneratorOptions)` (v2) and
`ODataV4Generator.GenerateAsync(ODataV4GeneratorOptions)` (v4).

## Verification

- The sample services used by the runtime tests (`tests/…OData.Core.Tests/Sample`, `tests/…ODataV4.Core.Tests/Sample`)
  are generator output; golden tests fail if regenerating them changes a byte (`SAP_SDK_UPDATE_GOLDEN=1` /
  `UPDATE_GOLDEN=1` rewrites them deliberately).
- Every EDMX from the Java generators' test suites is generated and compiled in memory: v2 27 of 36 inputs, v4 18 of
  31; the rest are inputs Java rejects as well (wrong OData version, malformed or inconsistent metadata, no service path).

## Not supported (yet)

v4 singletons, stream and geography properties, and operations returning enums are skipped with a warning. Java's
annotation strategies, metadata-JSON output and v2 `keepExistingSignatures` are not ported.
