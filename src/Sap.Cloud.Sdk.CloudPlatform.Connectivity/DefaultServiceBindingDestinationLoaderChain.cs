using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Tries the delegate loaders in order. A loader that reports <see cref="DestinationNotFoundException"/> is skipped; any
/// other failure ends the chain and is wrapped in a <see cref="DestinationAccessException"/>.
/// </summary>
internal sealed class DefaultServiceBindingDestinationLoaderChain(
    Func<IReadOnlyList<IServiceBindingDestinationLoader>> delegateLoaders) : IServiceBindingDestinationLoader
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultServiceBindingDestinationLoaderChain>();

    public IReadOnlyList<IServiceBindingDestinationLoader> DelegateLoaders => delegateLoaders();

    public Result<IHttpDestination> TryGetDestination(ServiceBindingDestinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var loaders = delegateLoaders();
        if (loaders.Count == 0)
        {
            return NoDelegateLoaders();
        }
        var suppressed = new List<Exception>();
        foreach (var loader in loaders)
        {
            var outcome = Evaluate(loader, loader.TryGetDestination(options), options, suppressed);
            if (outcome is { } done)
            {
                return done;
            }
        }
        return NothingFound(loaders.Count, options, suppressed);
    }

    public async Task<Result<IHttpDestination>> TryGetDestinationAsync(
        ServiceBindingDestinationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var loaders = delegateLoaders();
        if (loaders.Count == 0)
        {
            return NoDelegateLoaders();
        }
        var suppressed = new List<Exception>();
        foreach (var loader in loaders)
        {
            var result = await loader.TryGetDestinationAsync(options, cancellationToken).ConfigureAwait(false);
            if (Evaluate(loader, result, options, suppressed) is { } done)
            {
                return done;
            }
        }
        return NothingFound(loaders.Count, options, suppressed);
    }

    private static Result<IHttpDestination>? Evaluate(
        IServiceBindingDestinationLoader loader,
        Result<IHttpDestination> result,
        ServiceBindingDestinationOptions options,
        List<Exception> suppressed)
    {
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Transformation of service binding {Binding} to a Destination using an instance of {Loader} {State}.",
                BindingToString(options.ServiceBinding),
                loader.GetType().FullName,
                result.IsSuccess ? "succeeded" : "failed");
        }
        if (result.IsSuccess)
        {
            return result;
        }
        var cause = result.Exception;
        if (!HasCause<DestinationAccessException>(cause) && HasCause<DestinationNotFoundException>(cause))
        {
            suppressed.Add(cause);
            return null;
        }
        var message =
            $"Service Binding Destination loader {loader.GetType().FullName} returned an exception when loading destination "
            + $"for service '{options.ServiceBinding.GetServiceIdentifier()}' using service binding {BindingToString(options.ServiceBinding)}.";
        return Result<IHttpDestination>.Failure(new DestinationAccessException(message, cause));
    }

    private static Result<IHttpDestination> NothingFound(
        int loaderCount,
        ServiceBindingDestinationOptions options,
        List<Exception> suppressed) =>
        Result<IHttpDestination>.Failure(
            new DestinationNotFoundException(
                null,
                $"None of the {loaderCount} loaders could transform the service binding {BindingToString(options.ServiceBinding)} "
                + "into a destination. Check the inner exceptions and logs for further details.",
                new AggregateException(suppressed)));

    private static Result<IHttpDestination> NoDelegateLoaders()
    {
        if (Log.IsEnabled(LogLevel.Warning))
        {
            Log.LogWarning("No delegate loaders. As a consequence, the transformation will not be attempted.");
        }
        return Result<IHttpDestination>.Failure(
            new DestinationAccessException(
                "There are no delegate loaders that could perform the IServiceBinding to IHttpDestination transformation. "
                + "Make sure at least one loader is registered with ServiceBindingDestinationLoader.RegisterDefaultLoader, "
                + "for example by referencing the Connectivity.OAuth package."));
    }

    internal static bool HasCause<T>(Exception exception)
        where T : Exception
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is T)
            {
                return true;
            }
        }
        return false;
    }

    private static string BindingToString(IServiceBinding binding) =>
        $"{{ name: '{binding.Name ?? "<NULL>"}', serviceName: '{binding.ServiceName ?? "<NULL>"}', "
        + $"servicePlan: '{binding.ServicePlan ?? "<NULL>"}', tags: [{string.Join(", ", binding.Tags)}] }}";
}
