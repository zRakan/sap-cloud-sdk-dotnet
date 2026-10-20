# SAP Cloud SDK for .NET (port)

A .NET 10 port of the [SAP Cloud SDK for Java](https://github.com/SAP/cloud-sdk-java), with the
[JavaScript SDK](https://github.com/SAP/cloud-sdk-js) as reference for async API design. Both are Apache-2.0.

## Packages

| Package | Contents | Java module |
|---|---|---|
| `Sap.Cloud.Sdk.CloudPlatform.Core` | `Result<T>`, request-scoped context (`AsyncLocal`), request headers, service bindings (`VCAP_SERVICES`, servicebinding.io), `SdkModules` | `cloudplatform-core` |
| `Sap.Cloud.Sdk.CloudPlatform.Security` | `AuthToken`, Basic Auth, principals (XSUAA, IAS, Basic), credential types | `security` |
| `Sap.Cloud.Sdk.CloudPlatform.Tenant` | Tenant from context, JWT or service binding | `tenant` |
| `Sap.Cloud.Sdk.CloudPlatform.Caching` | Tenant/principal-isolated `CacheKey`, `CacheManager` | `caching` |
| `Sap.Cloud.Sdk.CloudPlatform.Resilience` | `ResilienceDecorator` on Polly: timeout, retry, circuit breaker, bulkhead, rate limiter, cache | `resilience-api`, `resilience4j` |
| `Sap.Cloud.Sdk.CloudPlatform.Connectivity` | Destinations, `DestinationAccessor`, env-var and service-binding loaders | `cloudplatform-connectivity` |
| `Sap.Cloud.Sdk.CloudPlatform.Connectivity.Http` | `HttpClientAccessor`, TLS/proxy/CSRF pipeline, `IHttpClientFactory` integration | `connectivity-apache-httpclient5` |
| `Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth` | OAuth 2.0 token flows (XSUAA, IAS), service bindings → destinations | `connectivity-oauth` (+ SAP `token-client`) |
| `Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationService` | BTP Destination service loader, transparent proxy | `connectivity-destination-service` |
| `Sap.Cloud.Sdk.AspNetCore` | `app.UseSapCloudSdk()` | `servlet-jakarta` |
| `Sap.Cloud.Sdk.DataModel.OData.Client` | Untyped OData v2/v4 protocol client | `odata-client` |
| `Sap.Cloud.Sdk.DataModel.OData.Core` | Typed OData v2 (VDM) runtime | `odata-core` |
| `Sap.Cloud.Sdk.DataModel.ODataV4.Core` | Typed OData v4 (VDM) runtime | `odata-v4-core` |
| `Sap.Cloud.Sdk.DataModel.FluentResult` | Generic JSON result elements | `fluent-result` |
| `Sap.Cloud.Sdk.DataModel.OpenApi` | Kiota clients on SAP destinations ([docs/OPENAPI.md](docs/OPENAPI.md)) | `openapi-core`, `openapi-generator` |

OData code generators (v2, v4) are in progress. RFC/JCo, SOAP and DWC are out of scope. Conventions, the module map and
every intentional difference from the Java SDK are in [docs/PORTING_GUIDE.md](docs/PORTING_GUIDE.md).

## Usage

```csharp
var app = builder.Build();
app.UseAuthentication();   // validate tokens first: the SDK decodes but does not validate them
app.UseSapCloudSdk();

app.MapGet("/whoami", () => new
{
    Tenant = TenantAccessor.GetCurrentTenant().TenantId,
    User = PrincipalAccessor.GetCurrentPrincipal().PrincipalId,
});

// Call a system configured in the BTP Destination service, on behalf of the current tenant/user:
app.MapGet("/partners", async () =>
{
    var destination = (IHttpDestination)await DestinationAccessor.GetDestinationAsync("S4HANA");
    var client = HttpClientAccessor.GetHttpClient(destination);
    return await client.GetStringAsync("sap/opu/odata/sap/API_BUSINESS_PARTNER/A_BusinessPartner?$top=5");
});

// Run code on behalf of a specific tenant (also flows into Task.Run and across await):
await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("tenant-id", "subdomain"), async () => { /* ... */ });
```

Typed OData requests run against a destination the same way:

```csharp
var products = await service.GetAllProduct()
    .Select(Product.Fields.Name, Product.Fields.Price)
    .Filter(Product.Fields.Price.Gt(10m))
    .ExecuteAsync(destination);
```

## Build

```bash
dotnet build   # warnings are errors
dotnet test
```
