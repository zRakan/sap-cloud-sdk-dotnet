namespace Sap.Cloud.Sdk.CloudPlatform.Core.Tests;

public class SdkModulesTests
{
    [Fact]
    public void EnsureLoaded_SkipsModulesThatAreNotDeployed_AndIsIdempotent()
    {
        // Core.Tests deploys Core only (plus Security/Tenant are not referenced), so most modules are missing.
        var exception = Record.Exception(SdkModules.EnsureLoaded);
        Assert.Null(exception);
        SdkModules.EnsureLoaded();
    }

    [Fact]
    public async Task EnsureLoaded_IsSafeUnderConcurrency()
    {
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(SdkModules.EnsureLoaded)));
    }
}
