using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity.Tests;

internal sealed class FakeLoader(Func<string, Result<IDestination>> lookup) : IDestinationLoader
{
    public List<string> Requests { get; } = [];

    public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options)
    {
        Requests.Add(destinationName);
        return lookup(destinationName);
    }

    public static FakeLoader NotFound() =>
        new(name => Result<IDestination>.Failure(new DestinationNotFoundException(name)));

    public static FakeLoader Returning(string name, IDestination destination) =>
        new(n => n == name
            ? Result<IDestination>.Success(destination)
            : Result<IDestination>.Failure(new DestinationNotFoundException(n)));

    public static FakeLoader Failing(Exception exception) => new(_ => Result<IDestination>.Failure(exception));
}

/// <summary>A loader whose async method is the real one; the sync method must not be used.</summary>
internal sealed class AsyncOnlyLoader(IDestination destination) : IDestinationLoader
{
    public int AsyncCalls { get; private set; }

    public Result<IDestination> TryGetDestination(string destinationName, DestinationOptions options) =>
        throw new InvalidOperationException("The synchronous method must not be called.");

    public async Task<Result<IDestination>> TryGetDestinationAsync(
        string destinationName,
        DestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        AsyncCalls++;
        await Task.Yield();
        return Result<IDestination>.Success(destination);
    }
}

internal sealed class FakeBindingLoader(Func<ServiceBindingDestinationOptions, Result<IHttpDestination>> load)
    : IServiceBindingDestinationLoader
{
    public int Calls { get; private set; }

    public Result<IHttpDestination> TryGetDestination(ServiceBindingDestinationOptions options)
    {
        Calls++;
        return load(options);
    }
}

internal sealed class FixedBindingAccessor(params IServiceBinding[] bindings) : IServiceBindingAccessor
{
    public IReadOnlyList<IServiceBinding> GetServiceBindings() => bindings;
}

internal sealed class StaticHeaderProvider(params Header[] headers) : IDestinationHeaderProvider
{
    public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) => headers;
}

internal sealed class ThrowingHeaderProvider : IDestinationHeaderProvider
{
    public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) =>
        throw new InvalidOperationException("boom");
}

internal static class Bindings
{
    public static IServiceBinding Of(string serviceName) =>
        new DefaultServiceBinding(
            serviceName + "-instance",
            serviceName,
            "standard",
            [],
            new Dictionary<string, object?> { ["clientsecret"] = "binding-secret" });
}
