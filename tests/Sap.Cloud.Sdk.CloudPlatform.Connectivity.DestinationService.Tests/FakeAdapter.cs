using System.Text.Json;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationServiceTests;

/// <summary>
/// A hand-written fake of the destination service REST API: answers requests that match a registered rule, records every
/// call, and fails on calls that no rule matches.
/// </summary>
internal sealed class FakeAdapter : IDestinationServiceAdapter
{
    private readonly List<(Func<string, DestinationRetrievalStrategy, bool> Matches, Func<CancellationToken, Task<string>> Respond)> _rules = [];
    private readonly Lock _gate = new();
    private readonly List<(string Path, DestinationRetrievalStrategy Strategy)> _calls = [];

    public string ProviderTenant { get; set; } = "provider-tenant";

    public IReadOnlyList<(string Path, DestinationRetrievalStrategy Strategy)> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public string GetProviderTenantId() => ProviderTenant;

    /// <summary>Answers requests for the path on behalf of the identity (any token handling) with the given JSON.</summary>
    public FakeAdapter Respond(string path, OnBehalfOf behalf, string json) =>
        Respond((p, s) => p == path && s.Behalf == behalf, json);

    /// <summary>Answers requests for the path with exactly the given strategy.</summary>
    public FakeAdapter Respond(string path, DestinationRetrievalStrategy strategy, string json) =>
        Respond((p, s) => p == path && s.Equals(strategy), json);

    public FakeAdapter Respond(string path, string json) => Respond((p, _) => p == path, json);

    /// <summary>Answers requests for the path that the predicate accepts.</summary>
    public FakeAdapter Respond(string path, Func<DestinationRetrievalStrategy, bool> strategyMatches, string json) =>
        Respond((p, s) => p == path && strategyMatches(s), json);

    /// <summary>Answers requests for the path on behalf of the identity with a computed response.</summary>
    public FakeAdapter Respond(string path, Func<CancellationToken, Task<string>> respond, OnBehalfOf behalf) =>
        Respond((p, s) => p == path && s.Behalf == behalf, respond);

    public FakeAdapter Respond(Func<string, DestinationRetrievalStrategy, bool> matches, string json) =>
        Respond(matches, _ => Task.FromResult(json));

    public FakeAdapter Respond(Func<string, DestinationRetrievalStrategy, bool> matches, Func<CancellationToken, Task<string>> respond)
    {
        lock (_gate)
        {
            // later rules take precedence, like later stubs in a mock
            _rules.Insert(0, (matches, respond));
        }
        return this;
    }

    /// <summary>Makes matching requests fail.</summary>
    public FakeAdapter Fail(Func<string, DestinationRetrievalStrategy, bool> matches, Exception failure) =>
        Respond(matches, _ => Task.FromException<string>(failure));

    public int CountCalls(string path, DestinationRetrievalStrategy? strategy = null) =>
        Calls.Count(c => c.Path == path && (strategy is null || c.Strategy.Equals(strategy)));

    public int CountCalls(string path, OnBehalfOf behalf) => Calls.Count(c => c.Path == path && c.Strategy.Behalf == behalf);

    public async Task<string> GetConfigurationAsJsonAsync(
        string servicePath,
        DestinationRetrievalStrategy strategy,
        CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task<string>>? respond;
        lock (_gate)
        {
            _calls.Add((servicePath, strategy));
            respond = _rules.FirstOrDefault(r => r.Matches(servicePath, strategy)).Respond;
        }
        return respond is null
            ? throw new InvalidOperationException($"Unexpected invocation of the fake adapter: {servicePath} with {strategy}")
            : await respond(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Canned responses of the destination service, copied from the Java tests.</summary>
internal static class Responses
{
    public const string SubaccountDestinations = """
        [{
            "Name": "CC8-HTTP-BASIC",
            "Type": "HTTP",
            "URL": "https://a.s4hana.ondemand.com",
            "Authentication": "BasicAuthentication",
            "ProxyType": "Internet",
            "TrustAll": "TRUE",
            "User": "USER",
            "Password": "pass"
          },
          {
            "Name": "CC8-HTTP-CERT",
            "Type": "HTTP",
            "URL": "https://a.s4hana.ondemand.com",
            "Authentication": "ClientCertificateAuthentication",
            "ProxyType": "Internet",
            "KeyStorePassword": "password",
            "KeyStoreLocation": "aaa"
          },
          {
            "Name": "SomeDestinationName",
            "Type": "HTTP",
            "URL": "https://a.s4hana.ondemand.com",
            "Authentication": "ClientCertificateAuthentication",
            "ProxyType": "Internet",
            "KeyStorePassword": "password",
            "KeyStoreLocation": "aaa"
          }]
        """;

    public const string BrokenSubaccountDestinations = """
        [{
            "Name": "CC8-HTTP-BASIC",
            "Type": "HTTP",
            "URL": "https://a.s4hana.ondemand.com",
            "Authentication": "BasicAuthentication",
            "ProxyType": "Internet",
            "TrustAll": "TRUE",
            "User": "USER",
            "Password": "pass"
          },
          {
            "Type": "BROKEN_TYPE",
            "URL": "https:/brok en URL!"
          }]
        """;

    public const string InstanceDestinations = """
        [{
            "Name": "CC8-HTTP-BASIC",
            "Type": "HTTP",
            "URL": "https://a.s4hana.ondemand.com",
            "Authentication": "BasicAuthentication",
            "ProxyType": "Internet",
            "TrustAll": "TRUE",
            "User": "USER1",
            "Password": "pass"
          },
          {
            "Name": "CC8-HTTP-CERT1",
            "Type": "HTTP",
            "URL": "https://a.s4hana.ondemand.com",
            "Authentication": "ClientCertificateAuthentication",
            "ProxyType": "Internet",
            "KeyStorePassword": "password",
            "KeyStoreLocation": "aaa"
          }]
        """;

    public const string OAuthWithoutAuthToken = """
        {
            "owner": { "SubaccountId": "00000000-0000-0000-0000-000000000000", "InstanceId": null },
            "destinationConfiguration": {
                "Name": "CC8-HTTP-OAUTH",
                "Type": "HTTP",
                "URL": "https://a.s4hana.ondemand.com/",
                "Authentication": "OAuth2SAMLBearerAssertion",
                "ProxyType": "Internet"
            },
            "authTokens": [
                {
                    "type": "",
                    "value": "",
                    "error": "org.apache.http.HttpException: Request to the /userinfo endpoint ended with status code 403",
                    "expires_in": ""
                }
            ]
        }
        """;

    public const string OAuthWithAuthToken = """
        {
            "owner": { "SubaccountId": "00000000-0000-0000-0000-000000000000", "InstanceId": null },
            "destinationConfiguration": {
                "Name": "CC8-HTTP-OAUTH",
                "Type": "HTTP",
                "URL": "https://a.s4hana.ondemand.com/",
                "Authentication": "OAuth2SAMLBearerAssertion",
                "ProxyType": "Internet"
            },
            "authTokens": [
                {
                    "type": "Bearer",
                    "value": "bearer_token",
                    "http_header": { "key": "Authorization", "value": "Bearer bearer_token" },
                    "expires_in": "3600",
                    "scope": "API_BUSINESS_PARTNER_0001"
                }
            ]
        }
        """;

    public const string OAuthWithExpiredAuthToken = """
        {
            "owner": { "SubaccountId": "00000000-0000-0000-0000-000000000000", "InstanceId": null },
            "destinationConfiguration": {
                "Name": "CC8-HTTP-OAUTH",
                "Type": "HTTP",
                "URL": "https://a.s4hana.ondemand.com/",
                "Authentication": "OAuth2SAMLBearerAssertion",
                "ProxyType": "Internet"
            },
            "authTokens": [
                {
                    "type": "Bearer",
                    "value": "bearer_token",
                    "http_header": { "key": "Authorization", "value": "Bearer bearer_token" },
                    "expires_in": "5",
                    "scope": "API_BUSINESS_PARTNER_0001"
                }
            ]
        }
        """;

    // Fictional use-case where the destination service responds with multiple auth tokens
    public const string OAuthWithMultipleAuthTokens = """
        {
            "owner": { "SubaccountId": "00000000-0000-0000-0000-000000000000", "InstanceId": null },
            "destinationConfiguration": {
                "Name": "CC8-HTTP-OAUTH",
                "Type": "HTTP",
                "URL": "https://a.s4hana.ondemand.com/",
                "Authentication": "OAuth2SAMLBearerAssertion",
                "ProxyType": "Internet"
            },
            "authTokens": [
                {
                    "type": "Bearer",
                    "value": "bearer_token",
                    "http_header": { "key": "Authorization", "value": "Bearer bearer_token" },
                    "expires_in": "5",
                    "scope": "API_BUSINESS_PARTNER_0001"
                },
                {
                    "type": "Bearer",
                    "value": "bearer_token",
                    "http_header": { "key": "Authorization", "value": "Bearer bearer_token" },
                    "expires_in": "50",
                    "scope": "API_BUSINESS_PARTNER_0001"
                }
            ]
        }
        """;

    public const string BasicWithAuthToken = """
        {
            "owner": { "SubaccountId": "00000000-0000-0000-0000-000000000000", "InstanceId": null },
            "destinationConfiguration": {
                "Name": "CC8-HTTP-BASIC",
                "Type": "HTTP",
                "URL": "https://a.s4hana.ondemand.com",
                "Authentication": "BasicAuthentication",
                "ProxyType": "Internet",
                "TrustAll": "TRUE",
                "User": "USER",
                "Password": "pass"
            },
            "authTokens": [
                {
                    "type": "Basic",
                    "value": "dGVzdDpwYgXNzMTIzNDU=",
                    "http_header": { "key": "Authorization", "value": "Basic dGVzdDpwYgXNzMTIzNDU=" }
                }
            ]
        }
        """;

    public const string NoAuth = """
        {
            "owner": { "SubaccountId": "00000000-0000-0000-0000-000000000000", "InstanceId": null },
            "destinationConfiguration": {
                "Name": "Subscriber-CCT",
                "Type": "HTTP",
                "URL": "https://wrong.com",
                "Authentication": "NoAuthentication",
                "ProxyType": "Internet",
                "Description": "Dummy destination that should be overwritten by a subscriber."
            }
        }
        """;

    public static string Http(string name, string url) => $$"""
        {
            "owner": { "SubaccountId": "someId", "InstanceId": null },
            "destinationConfiguration": {
                "Name": "{{name}}",
                "Type": "HTTP",
                "URL": "{{url}}",
                "Authentication": "NoAuthentication",
                "ProxyType": "Internet",
                "Description": "Test destination"
            }
        }
        """;

    /// <summary>Creates a response from properties and tokens, like the helper of the Java authentication tests.</summary>
    public static string Of(IReadOnlyDictionary<string, string> configuration, params object[] authTokens) =>
        JsonSerializer.Serialize(new
        {
            owner = new { SubaccountId = "a89ea924-d9c2-4eab", InstanceId = "foobar" },
            destinationConfiguration = configuration,
            authTokens,
        });
}
