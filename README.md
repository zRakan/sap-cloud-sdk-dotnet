# SAP Cloud SDK for .NET (port)

A .NET 10 port of the [SAP Cloud SDK for Java](https://github.com/SAP/cloud-sdk-java), with the
[JavaScript SDK](https://github.com/SAP/cloud-sdk-js) as reference for async API design. Both are Apache-2.0.

## Status

| Package | Contents |
|---|---|
| `Sap.Cloud.Sdk.CloudPlatform.Core` | `Result<T>`, request-scoped context (`IThreadContext`, `AsyncLocal`-based), request headers, service bindings (`VCAP_SERVICES`, servicebinding.io) |
| `Sap.Cloud.Sdk.CloudPlatform.Security` | `AuthToken`, Basic Auth, principals (XSUAA, IAS, Basic), credential types |
| `Sap.Cloud.Sdk.CloudPlatform.Tenant` | Tenant from context, JWT (XSUAA/IAS) or service binding |
| `Sap.Cloud.Sdk.AspNetCore` | `app.UseSapCloudSdk()` middleware |

Connectivity, destinations, resilience, OpenAPI and OData are next. See [docs/PORTING_GUIDE.md](docs/PORTING_GUIDE.md)
for the module map, conventions and recorded deviations from the Java SDK.

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

// Run code on behalf of a specific tenant (also flows into Task.Run and across await):
await TenantAccessor.ExecuteWithTenantAsync(new DefaultTenant("tenant-id", "subdomain"), async () => { /* ... */ });
```

## Build

```bash
dotnet build
dotnet test
```
