# Porting Guide: SAP Cloud SDK for Java → .NET

This guide is binding for everyone porting a module. The reference implementation is the set of modules already
ported (`Sap.Cloud.Sdk.CloudPlatform.Core`, `.Security`, `.Tenant`, `Sap.Cloud.Sdk.AspNetCore`). When in doubt, copy
what they do.

Primary source: [`SAP/cloud-sdk-java`](https://github.com/SAP/cloud-sdk-java) (Apache-2.0).
Secondary reference for async API shapes and the shared OData layer: [`SAP/cloud-sdk-js`](https://github.com/SAP/cloud-sdk-js).

## 1. Module map

| Java module | .NET project | Status |
|---|---|---|
| `cloudplatform/cloudplatform-core` | `Sap.Cloud.Sdk.CloudPlatform.Core` | ✅ ported |
| *(external)* `btp-environment-variable-access` | `Sap.Cloud.Sdk.CloudPlatform.Core` → `ServiceBindings` | ✅ minimal port (see §9) |
| `cloudplatform/security` | `Sap.Cloud.Sdk.CloudPlatform.Security` | ✅ ported |
| `cloudplatform/tenant` | `Sap.Cloud.Sdk.CloudPlatform.Tenant` | ✅ ported |
| `cloudplatform/servlet-jakarta` | `Sap.Cloud.Sdk.AspNetCore` | ✅ replaced by middleware |
| `cloudplatform/caching` | `…CloudPlatform.Caching` | ✅ ported |
| `cloudplatform/resilience-api`, `resilience4j` | `…CloudPlatform.Resilience` (Polly) | ✅ ported |
| `cloudplatform/cloudplatform-connectivity` | `…CloudPlatform.Connectivity` | ✅ ported |
| `cloudplatform/connectivity-oauth` | `…CloudPlatform.Connectivity.OAuth` | ✅ ported |
| `cloudplatform/connectivity-apache-httpclient5` (+ CSRF from `-httpclient4`) | `…CloudPlatform.Connectivity.Http` | ✅ ported |
| *(external)* SAP `token-client` | `…Connectivity.OAuth` → `OAuth2` namespace | ✅ ported (client-credentials, jwt-bearer) |
| `cloudplatform/connectivity-destination-service` | `…CloudPlatform.Connectivity.DestinationService` | ✅ ported |
| `datamodel/openapi/*` | `Sap.Cloud.Sdk.DataModel.OpenApi` | ✅ replaced by Kiota integration (see `docs/OPENAPI.md`) |
| `datamodel/fluent-result` | `Sap.Cloud.Sdk.DataModel.FluentResult` | ✅ ported |
| `datamodel/odata-client` | `Sap.Cloud.Sdk.DataModel.OData.Client` | ✅ ported |
| `datamodel/odata/odata-core` (v2) | `Sap.Cloud.Sdk.DataModel.OData.Core` | ✅ ported |
| `datamodel/odata-v4/odata-v4-core` | `Sap.Cloud.Sdk.DataModel.ODataV4.Core` | ✅ ported |
| OData generators (v2, v4) | `Sap.Cloud.Sdk.DataModel.OData.Generator` | ⏳ |

**Out of scope:** RFC/JCo, SOAP, DWC/Megaclite, ZTIS, FIPS sample, Apache HttpClient 4 modules, the duplicated
`odata-client-apache-httpclient5`, Spring archetypes, Maven plugins.

## 2. Project layout and build

- One Java Maven module → one .NET project under `src/`, with a test project `tests/<Project>.Tests`.
- `Directory.Build.props` sets `net10.0`, nullable reference types, implicit usings, **warnings as errors**, the
  `latest-recommended` analyzers and **mandatory XML docs** on public API. Do not weaken these per project; fix the
  code. A suppression needs a `#pragma` with a one-line justification (see `Result.cs`).
- Package versions are managed centrally in `Directory.Packages.props`. Add a package there, never in a `.csproj`.
- Test projects automatically compile everything in `tests/Shared/` (`TestJwt`, `StaticStateReset`, and the assembly
  attribute that turns off test parallelization).

## 3. Naming

| Java | .NET |
|---|---|
| package `com.sap.cloud.sdk.cloudplatform.tenant` | namespace `Sap.Cloud.Sdk.CloudPlatform.Tenant` (PascalCase) |
| subpackage `exception` | `Exceptions` (plural, `Exception` would clash) |
| package `thread` | `Threading` (avoid clash with `System.Threading.Thread`) |
| interface `Tenant` | `ITenant` |
| class `DefaultTenant` | `DefaultTenant` (unchanged) |
| `getTenantId()` | property `TenantId` |
| `isConfidential()` | property `IsConfidential` |
| method `executeWithTenant(...)` | `ExecuteWithTenant(...)`; async variant `ExecuteWithTenantAsync(...)` |
| constant `PROPERTY_TENANT` | `PropertyTenant` (`const` if the value is a compile-time string) |

**Clash rule:** rename a type if its .NET name would clash with a commonly imported BCL type or a VB keyword, and
record the rename in its XML `<remarks>`. Done so far: `Credentials` → `ISecurityCredentials`,
`Principal` → `ISecurityPrincipal`, `Property` → `ThreadContextProperty`.

Thread-context property keys use the .NET full type name of the listener plus the Java suffix, for example
`"Sap.Cloud.Sdk.CloudPlatform.Tenant.TenantThreadContextListener:tenant"`.

## 4. Type and library mapping

| Java | .NET |
|---|---|
| `io.vavr.control.Try<T>` | `Result<T>` (`Sap.Cloud.Sdk.CloudPlatform`). `Try.of` → `Result.Of`, `flatMap` → `Bind`, `getOrElseThrow(f)` → `GetValueOrThrow(f)`, `recover`, `orElse`, `onFailure` keep their names. |
| `io.vavr.control.Option<T>` / `Optional<T>` | nullable `T?` |
| `io.vavr` collections | BCL collections |
| `@Nonnull` / `@Nullable` | nullable reference types; add `ArgumentNullException.ThrowIfNull` on public entry points |
| Lombok `@Value` / `@Data` | `sealed record` (override `ToString` if any member is secret) |
| Lombok `@Getter` / `@Setter` | properties |
| Lombok `@Builder` | a builder class (see `RequestHeaderContainerBuilder`) or `init` properties for simple cases |
| Lombok `@RequiredArgsConstructor` | primary constructor |
| `@Slf4j` | `private static readonly ILogger Log = SdkLogging.CreateLogger<T>();` (`CreateLogger(typeof(X).FullName!)` in static classes) |
| `Callable<T>` / `Supplier<T>` | `Func<T>` |
| `Executable` / `Runnable` | `Action` |
| `Function<A,B>` | `Func<A,B>` |
| `Duration` / `Instant` | `TimeSpan` / `DateTimeOffset` |
| `URI` | `Uri` |
| `List<T>` / `Map<K,V>` (returned) | `IReadOnlyList<T>` / `IReadOnlyDictionary<K,V>` |
| Guava `Multimap` | `OrderedDictionary<K, V[]>` or `ILookup` |
| `ServiceLoader` / `FacadeLocator` / `META-INF/services` | settable static facade property with a default, plus a `[ModuleInitializer]` for registrations (see `ModuleInitializer.cs`). Add the assembly to `SdkModules.ModulesWithRegistrations`, and call `SdkModules.EnsureLoaded()` at the start of any registry read, so modules whose types are never referenced still register. |
| `ThreadLocal` | `AsyncLocal<T>` (already wrapped by `IThreadContext`; do not add new ones) |
| `ThreadContextExecutors`, `ThreadContextExecutorService` | not needed: the context flows into `Task.Run`, `await` and the thread pool |
| Jackson / Gson | `System.Text.Json` |
| auth0 `DecodedJWT` | `Microsoft.IdentityModel.JsonWebTokens.JsonWebToken` (via `AuthToken`) |
| Apache HttpClient | `HttpClient` from `IHttpClientFactory`; per-destination behavior goes into `DelegatingHandler`s |
| Resilience4j | Polly v8 (`ResiliencePipeline`) |
| Caffeine cache | `Microsoft.Extensions.Caching.Memory` |
| `KeyStore` | `X509Certificate2` / `X509Certificate2Collection` |

## 5. Exceptions

- One .NET exception per Java exception, same name, in the module's `Exceptions` namespace, deriving from the same
  base as in Java (`RuntimeException` → `Exception`). Provide the three standard constructors (see
  `TenantAccessException.cs`, which you can use as a template).
- Java's `addSuppressed(...)` for "tried several sources" → pass `new AggregateException(failures)` as
  `innerException` (see `DefaultTenantFacade.TryGetCurrentTenant`).
- Do not catch `Exception` broadly unless the result is captured into a `Result<T>` (analyzer CA1031 will flag it).
- Never wrap `OperationCanceledException`.

## 6. The accessor/facade pattern

Every Java `XyzAccessor` maps to a static class with this shape (reference: `TenantAccessor.cs`):

```csharp
public static class XyzAccessor
{
    public static IXyzFacade XyzFacade { get; set; }           // null restores the default
    public static Func<IXyz?>? FallbackXyz { get; set; }       // Supplier<Xyz> in Java
    public static IXyz GetCurrentXyz();                        // throws XyzAccessException
    public static Result<IXyz> TryGetCurrentXyz();
    public static T ExecuteWithXyz<T>(IXyz xyz, Func<T> func);
    public static void ExecuteWithXyz(IXyz xyz, Action action);
    public static Task<T> ExecuteWithXyzAsync<T>(IXyz xyz, Func<Task<T>> func);
    public static Task ExecuteWithXyzAsync(IXyz xyz, Func<Task> func);
    public static T ExecuteWithFallbackXyz<T>(Func<IXyz> fallback, Func<T> func);
    public static Task<T> ExecuteWithFallbackXyzAsync<T>(Func<IXyz> fallback, Func<Task<T>> func);
}
```

`ExecuteWith…` requires the default facade and throws `ThreadContextExecutionException` otherwise, as in Java.

## 7. Async rules

- **Every operation that performs I/O is async-only**: `Task<T> DoSomethingAsync(..., CancellationToken cancellationToken = default)`.
  Do not port Java's blocking variants and do not add sync-over-async wrappers.
- Pure in-memory APIs (context lookups, builders) stay synchronous.
- Use `ConfigureAwait(false)` on every `await` in library code.
- When the JS SDK has an async shape for a feature, prefer it over a blocking Java shape.

## 8. Security rules

- `ToString()` must never reveal secrets: tokens, passwords, client secrets, keys, certificates, credentials of
  service bindings, header values. Use `(hidden)` as Java does. Add a test for it.
- Thread-context properties holding secrets use `ThreadContextProperty.OfConfidential` / `CaptureConfidential`.
- The SDK **decodes JWTs without validating them**, as the Java SDK does. Do not add code that makes authorization
  decisions based on an `AuthToken` alone.

## 9. Recorded deviations from the Java SDK

| Area | Java | .NET | Reason |
|---|---|---|---|
| Basic Auth parsing | splits at every `:` (truncates passwords containing `:`) | splits at the first `:` | RFC 7617 |
| Basic Auth thread-context property | not confidential | confidential | it holds a password |
| `ExecuteWithFallback…` when a value exists | runs the callable directly | runs it in a duplicated context without default listeners | same observable result, but the callable cannot mutate the caller's context |
| Fallback that returns `null` (principal, tenant) | `new XyzAccessException()` without message | message plus the original failure as inner exception | diagnosability |
| Thread executors / `ThreadContextDecorator` / Spring `SecurityContext` | present | omitted | `AsyncLocal` flows by itself; no Spring |
| `LocaleAccessor` | present | omitted | `CultureInfo.CurrentCulture` already flows per request |
| Service bindings | full SAP library (several K8s layouts, Spring properties) | `VCAP_SERVICES` + servicebinding.io with `.metadata` | add layouts when a real deployment needs them |
| `ThreadContextExecutor` | always wraps exceptions | `WithoutExceptionWrapping()` opt-out | middleware must not change exception types |
| Cache keys | `GenericCacheKey`, `CacheKey`, `SerializableCacheKey` | single `CacheKey` | no serialization needed |
| `CacheKey.ToString`, `CacheConfiguration.ToString` | print components / parameters | print only their count | components may hold secrets |
| `CacheStatsMXBean`, cache `serializable` flag | present | omitted; `CacheStats.From(MemoryCache)` | no JMX; in-memory only |
| `queueCallable` / `queueSupplier` | present | not ported | use `ExecuteAsync` / `Task.Run` |
| Isolation key equality | compares tenant/principal objects | compares tenant/principal IDs | safer |
| Circuit breaker | count-based sliding window, slow-call detection, half-open buffer | Polly time window (1 min, min. 2 calls, min. break 500 ms), no slow-call detection | Polly limits |
| Resilience state on config change | first configuration per identifier wins | state rebuilt when the configuration for an identifier changes | simplicity |
| Timeout | `shouldCancelRunningFuture` | always cancels via `CancellationToken`; sync operations that ignore the token cannot be interrupted | .NET cancellation model |
| Resilience failures | everything wrapped in `ResilienceRuntimeException` | caller-token `OperationCanceledException` is never wrapped, retried or passed to the fallback | guide §5 |
| Resilience cache | JCache expiry policies; `put(null)` throws | created/modified → absolute, accessed/touched → sliding; null results and zero duration are not cached | `MemoryCache` model |
| Rate limiter / bulkhead | resilience4j | custom Polly strategies (rate limiter ports `AtomicRateLimiter`) | not in Polly.Core |
| Token client retry | `Thread.sleep` | `Task.Delay` with cancellation | async |
| Token client correlation id | MDC correlation id | `Activity.Current.TraceId`, else a new GUID | .NET tracing |
| RFC destinations | `RfcDestination`, `asRfc`/`isRfc` | omitted; RFC env-var destinations become plain `DefaultDestination`s | JCo out of scope |
| `UriBuilder` (connectivity) | public class | not ported; `UriPathMerger` is static | `System.Uri` suffices |
| Key and trust stores | `KeyStore` + BouncyCastle | `X509Certificate2Collection` (first entry carries the key) | guide §4 |
| `IDestinationProperties.get` | `get` | `GetProperty`; typed `TryGet`/`TryGetAs`/`GetOrDefault` extensions | CA1716, value-type nullability |
| Package-private connectivity members | package-private | public or `internal` + `InternalsVisibleTo` (e.g. `BuildInternal` for the OAuth assembly) | cross-assembly use |
| `DefaultDestination`/`DefaultHttpDestination`/`Header` `ToString` | hide names with `password`/`secret`; print URI and header values | also hide keys, tokens, certificates, authorization; strip URI user info; hide header values | §8 |
| Env-var destinations JSON | json-sanitizer repairs malformed JSON | `System.Text.Json` with comments and trailing commas | no equivalent |
| `BtpServiceOptions` | enums implementing `OptionsEnhancer` | sealed classes with static instances; deprecated members omitted | .NET enums can't implement interfaces |
| Destination header providers | synchronous `getHeaders` | additional `GetHeadersAsync` (default calls the sync method); HTTP pipeline always uses async; OAuth provider's sync `GetHeaders` blocks on the async one | token retrieval is I/O |
| OAuth token request parameters | one map shared by all requests of a destination, mutated per request (`app_tid`) | copied per request | the Java map is a cross-tenant race |
| OAuth user token tenant | `Token.getAppTid()` | `app_tid` claim, falling back to `zid` | no java-security `Token` type |
| OAuth credential type `X509_ATTESTED` (ZTIS) | supported via `connectivity-ztis` | throws `DestinationAccessException` | ZTIS out of scope |
| OAuth destination names | `clientId.hashCode()` | the same 31-based hash (`StableHash`) | `string.GetHashCode` is randomized |
| OAuth token HTTP client | Apache HttpClient per identity | shared `SocketsHttpHandler`; one per certificate identity (mTLS) | connection reuse |
| fluent-result Gson types | `GsonResultObject`, `@ElementName` | `JsonResultObject`, `ElementNameAttribute`; `get`/`as` → `GetElement`/`AsType` | System.Text.Json; CA1716 |
| OData expression names | `Collection…`, `ValueEnum`, `getOperator`, `Double/BigDecimal` strategies | `Array…`, `IValueEnumMember`, `FilterOperator`, `AsDouble/AsDecimal`; `And/Or/Not/In/Date` as extension methods | CA1711/CA1716/CA1720 |
| OData request execution | `execute(HttpClient)` (Apache, sync) | `ExecuteAsync(HttpClient, CancellationToken)`; responses buffered unless `WithoutResponseBuffering()` | async; System.Net.Http |
| OData CSRF failure details | `addSuppressed` | `Exception.Data["CsrfTokenRetrievalFailure"]` | no suppressed exceptions |
| OData temporal precision | nanoseconds (`java.time`) | 100 ns (`DateTime`, `DateTimeOffset`, `TimeOnly`, `DateOnly`) | .NET types |
| OData decimal literals | `BigDecimal` keeps scale (`1.1E+2`) | `decimal` prints `110` | no scale exponent in .NET |
| OData JSON converters | Gson returns `null` for unparsable GUID/date | throws `JsonException` | fail fast |
| HTTP client cache | accessor caches whole clients; `setHttpClientCache` | default factory caches only the primary `SocketsHttpHandler` (1 h sliding, `CacheManager`), delegating handlers per call; evicted handlers disposed after timeout + 1 min | per-call destination instance |
| Destination header merge | `addHeader` appends everything | multi-valued headers appended; for single-valued and content headers the caller's value wins | `HttpClient` can't send them twice |
| Absolute request URIs | override the destination path | same host → treated as relative; other host → `DestinationPathsNotMergeableException` | `BaseAddress` resolution happens before handlers |
| CSRF `HEAD` | sent without the request's headers | carries the request's (non-content) headers incl. destination auth | SAP systems need `Authorization` on the `HEAD` |
| HTTP retry strategy, TLS `Upgrade`, `maxConnectionsTotal` | present | not ported; use the Resilience module for retry | no `SocketsHttpHandler` equivalent |
| TLS protocol versions | any JDK protocol name | `TLSv1.2`, `TLSv1.3`, `TLS` only | TLS 1.0/1.1 obsolete |
| CF platform TLS | `SSLContext` from instance-identity files | client certificate via callback, reloaded when the files change | rotation |
| Proxy credentials for HTTPS targets | `Proxy-Authorization` on `CONNECT` | header not sent on `CONNECT` | `SocketsHttpHandler` limitation |
| `IHttpClientFactory` integration | n/a | `IHttpClientBuilder.AddSapDestination(...)`, destination looked up per request | .NET addition |
| OpenAPI generator and runtime | Maven plugin over OpenAPI Generator + `openapi-core` `ApiClient` | Kiota-generated clients + `OpenApiRequestAdapterFactory` on the destination HTTP pipeline | maintained .NET generator; no Java at build time |
| Destination service `getAllDestinations` | `tryGetAllDestinations` (deprecated) | `TryGetAllDestinationsAsync` returning `Result<IReadOnlyList<IDestination>>`, not obsolete | async |
| Destination service JKS stores | key and trust stores | JKS trust stores read for certificates only; JKS key stores unsupported (`DestinationAccessException`) | no JKS in .NET |
| Destination service caches | Caffeine with eviction | `MemoryCache`; a size limit rejects new entries instead of evicting; zero expiry stores nothing; per-key `SemaphoreSlim` locks (30 min sliding) | `MemoryCache` model |
| Destination service pre-lookup check failure | throws | failed `Result` | consistent `Try…` semantics |
| `TransparentProxy.register` | sync | `RegisterAsync` only | opens a TCP connection |
| Destination-service request custom headers | map order | sorted by key | deterministic |
| Module discovery | `ServiceLoader` scans the classpath | `SdkModules.EnsureLoaded()` loads deployed SDK assemblies by name before first registry use | .NET loads assemblies lazily |
| OData VDM (de)serialization | Gson/Jackson reflection over annotated fields | static metadata tables + System.Text.Json converters; `@ODataField`/`@Key` kept as documentation attributes (v2) | no runtime reflection |
| OData VDM base classes | generic `VdmObject<T>`; `getType`, `fromMap`, `getEntityClass`, `Class<T>` arguments | non-generic `VdmObject` with typed subclasses; those members dropped, type arguments carry the class | generics |
| OData static field descriptors | `Entity.FIELD_NAME`, `Entity.ALL_FIELDS` | `Entity.Fields.FieldName`, `Entity.Fields.All` | clash with instance properties |
| OData paging/streaming | `iteratingPages`/`iteratingEntities` (Iterable), `streamingEntities` | `IAsyncEnumerable`; `streamingEntities` dropped | async |
| OData batch responses | `Try`, blocking getters | `Result` + async getters | async |
| OData `Void` results | `Void.class` | `VoidResult` (v2) / `VdmVoid` (v4) | no `Void` generic argument |
| OData v4 enums | Java enums | sealed classes deriving from `VdmEnum` with static `Values` | enum metadata |
| OData v4 typed filter functions | `FilterableBoolean<EntityT>` | client's untyped `IValueBoolean` | cross-entity filters not compile-time checked |
| OData v4 `ServiceWithNavigableEntities` | interface with default methods | abstract base class | default interface members aren't visible on implementers |
| OData v4 `EntityCollection.forEntity` | mutates the path (fails on second call) | copies the path | bug fix |
| OData deserialization and custom fields | custom fields stay marked as changed | change tracking reset for all fields | consistency |
| OData JSON leniency | Gson tolerates trailing data | rejected | STJ strictness |
| OData Calendar converters | `Calendar` | `DateTimeOffset` (UTC) | no `Calendar` |

Add a row whenever you deviate on purpose. Unrecorded deviations are bugs.

## 9a. Generated OData code (v2 and v4)

The hand-written samples in `tests/Sap.Cloud.Sdk.DataModel.OData.Core.Tests/Sample` (v2) and
`tests/Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests/Sample` (v4) are the reference output of the code generators.
Shared rules:

- Files start with `// <auto-generated/>` and `#nullable enable`.
- Static field descriptors live in a nested static class `Fields` (`Product.Fields.Name`, `Product.Fields.All` for
  `$select=*`); Java's `Product.NAME` would clash with the instance property `Name`. Navigation descriptors use the
  plain EDM name (no `To` prefix).
- No reflection: each type declares a static metadata table (`VdmTypeMetadata` in v2, `VdmTypeInfo` in v4) that drives
  JSON mapping, keys and change tracking; property setters call `RememberChangedField`.
- Execution is async: `ExecuteAsync(HttpClient)` / `ExecuteAsync(IHttpDestinationProperties)`; destinations go through
  `ODataHttpClientProvider.Provider`, which defaults to `HttpClientAccessor.GetHttpClient`.

## 10. Tests

- Port the behavior that the Java tests check, not their structure. Name tests `Method_Condition_Expectation` or a
  sentence in PascalCase with underscores.
- xUnit 2, plain `Assert`. No mocking library: write small fakes (see `FixedFacade` in `TenantTests`).
- Tests that touch static accessors derive from `StaticStateReset` (or a subclass that resets their module's state).
- Use `TestJwt.Create(new { ... })` for tokens.
- Every module adds tests for: secret-hiding `ToString`, async flow across `await`/`Task.Run` where relevant, and
  isolation between parallel flows where it holds per-request state.

## 11. Definition of done for a module

1. `dotnet build` passes with zero warnings (warnings are errors).
2. `dotnet test` passes.
3. Public API has XML docs; types that differ from Java say so in `<remarks>`.
4. The module map (§1) and deviation table (§9) are updated.
