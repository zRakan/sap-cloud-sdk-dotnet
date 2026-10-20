# OpenAPI clients

The Java SDK generates OpenAPI clients with its own Maven plugin (a wrapper around OpenAPI Generator with custom
templates) and runs them on `openapi-core` (`ApiClient`). In .NET, clients are generated with
[Kiota](https://learn.microsoft.com/openapi/kiota/) and run on `Sap.Cloud.Sdk.DataModel.OpenApi`, which connects them
to SAP destinations.

## Generate a client

The repository pins Kiota as a local tool (`dotnet-tools.json`):

```bash
dotnet tool restore
dotnet kiota generate -l CSharp -d path/to/spec.yaml -c MyServiceClient -n MyCompany.MyService -o Generated/MyService
```

Add `Microsoft.Kiota.Bundle` and `Sap.Cloud.Sdk.DataModel.OpenApi` to the project that contains the generated code.

## Call the API through a destination

```csharp
// Destination from the BTP Destination service, env vars, a service binding, ...
var adapter = await OpenApiRequestAdapterFactory.CreateAsync("MY_DESTINATION");
var client = new MyServiceClient(adapter);
var items = await client.Items.GetAsync();
```

The adapter uses `HttpClientAccessor`, so the destination's authentication (Basic, OAuth 2.0 flows, principal
propagation), proxy, mTLS and CSRF handling apply, and tenant isolation works as for every other SDK request.
With `IHttpClientFactory`, pass the named client instead:

```csharp
services.AddHttpClient("my-service").AddSapDestination("MY_DESTINATION");
// ...
var adapter = OpenApiRequestAdapterFactory.Create(httpClientFactory.CreateClient("my-service"),
    new Uri("https://placeholder.invalid")); // the destination handler replaces the base address per request
```

## Differences to the Java SDK

| Java | .NET |
|---|---|
| `openapi-generator-maven-plugin`, SAP templates | Kiota |
| `ApiClient`, `AbstractOpenApiService`, `OpenApiResponse` | Kiota runtime + `OpenApiRequestAdapterFactory` |
| `OpenApiRequestException` | `Microsoft.Kiota.Abstractions.ApiException` (and generated error types) |
