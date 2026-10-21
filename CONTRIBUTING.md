# Contributing

Thanks for helping! This project ports the [SAP Cloud SDK for Java](https://github.com/SAP/cloud-sdk-java) to .NET.

## Before you start

- Read [docs/PORTING_GUIDE.md](docs/PORTING_GUIDE.md). It defines naming, the Java-to-.NET type mapping, async and
  security rules, and the table of intentional deviations from Java (§9). Every deliberate deviation needs a row there.
- Port behavior from the Java sources and tests; keep the public API shape recognizable for Java SDK users.

## Workflow

1. Fork the repository and create a branch.
2. `dotnet build` must pass with zero warnings (warnings are errors) and `dotnet test` must pass.
3. Add tests for new behavior. Never print secrets (tokens, passwords, keys) from `ToString()` or logs, and test that.
4. If you change the OData runtime or generators, regenerate the samples and commit them:
   `SAP_SDK_UPDATE_GOLDEN=1 dotnet test tests/Sap.Cloud.Sdk.DataModel.OData.Generator.Tests` (v2) and
   `UPDATE_GOLDEN=1 dotnet test tests/Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests` (v4).
5. Open a pull request describing what you ported or changed and which Java classes it corresponds to.

By contributing you agree that your contributions are licensed under the Apache License 2.0.
