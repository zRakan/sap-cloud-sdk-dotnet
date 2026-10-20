namespace Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

/// <summary>
/// The application-wide <see cref="IServiceBindingAccessor"/>. By default it combines the Kubernetes
/// (<see cref="ServiceBindingIoServiceBindingAccessor"/>) and Cloud Foundry
/// (<see cref="VcapServicesServiceBindingAccessor"/>) sources and caches the result.
/// </summary>
public static class DefaultServiceBindingAccessor
{
    private static IServiceBindingAccessor? _instance;

    /// <summary>
    /// The accessor. Setting <see langword="null"/> restores the default. Replace it in tests or to add sources.
    /// </summary>
    public static IServiceBindingAccessor Instance
    {
        get => _instance ??= CreateDefault();
        set => _instance = value;
    }

    /// <summary>Creates the default, cached accessor.</summary>
    /// <returns>The accessor.</returns>
    public static IServiceBindingAccessor CreateDefault() =>
        new CachingServiceBindingAccessor(new CombiningServiceBindingAccessor(
            new ServiceBindingIoServiceBindingAccessor(),
            new VcapServicesServiceBindingAccessor()));
}

/// <summary>Combines the bindings of several accessors, in order.</summary>
/// <param name="accessors">The accessors.</param>
public sealed class CombiningServiceBindingAccessor(params IReadOnlyList<IServiceBindingAccessor> accessors)
    : IServiceBindingAccessor
{
    /// <inheritdoc/>
    public IReadOnlyList<IServiceBinding> GetServiceBindings() =>
        [.. accessors.SelectMany(a => a.GetServiceBindings())];
}

/// <summary>Caches the bindings of another accessor. Bindings do not change while the application runs.</summary>
/// <param name="inner">The accessor to cache.</param>
public sealed class CachingServiceBindingAccessor(IServiceBindingAccessor inner) : IServiceBindingAccessor
{
    private readonly Lazy<IReadOnlyList<IServiceBinding>> _bindings = new(inner.GetServiceBindings);

    /// <inheritdoc/>
    public IReadOnlyList<IServiceBinding> GetServiceBindings() => _bindings.Value;
}
