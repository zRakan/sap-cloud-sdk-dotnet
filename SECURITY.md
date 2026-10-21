# Security policy

This SDK handles OAuth tokens, client secrets, certificates and tenant isolation. Please report vulnerabilities
privately via [GitHub security advisories](https://github.com/zRakan/sap-cloud-sdk-dotnet/security/advisories/new)
rather than in public issues.

Note: like the SAP Cloud SDK for Java, this SDK **decodes** JWTs from incoming requests but does not **validate** them.
Validate tokens with your application's authentication middleware before `UseSapCloudSdk()`.

Vulnerabilities in the upstream SAP SDKs should be reported to SAP as described in their repositories.
