namespace Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;

/// <summary>
/// A service instance bound to the application, as provided by the platform (Cloud Foundry <c>VCAP_SERVICES</c> or a
/// Kubernetes service binding).
/// </summary>
/// <remarks>
/// Port of <c>com.sap.cloud.environment.servicebinding.api.ServiceBinding</c>. Nested JSON values are represented as
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> (objects), <see cref="IReadOnlyList{T}"/> (arrays),
/// <see cref="string"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/> or <see langword="null"/>.
/// </remarks>
public interface IServiceBinding
{
    /// <summary>The name of the binding, if known.</summary>
    string? Name { get; }

    /// <summary>The name of the bound service (for example <c>xsuaa</c> or <c>destination</c>), if known.</summary>
    string? ServiceName { get; }

    /// <summary>The service plan, if known.</summary>
    string? ServicePlan { get; }

    /// <summary>The tags of the binding.</summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>The credentials of the binding.</summary>
    IReadOnlyDictionary<string, object?> Credentials { get; }
}

/// <summary>Default implementation of <see cref="IServiceBinding"/>.</summary>
/// <param name="Name">The binding name.</param>
/// <param name="ServiceName">The service name.</param>
/// <param name="ServicePlan">The service plan.</param>
/// <param name="Tags">The tags.</param>
/// <param name="Credentials">The credentials.</param>
public sealed record DefaultServiceBinding(
    string? Name,
    string? ServiceName,
    string? ServicePlan,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, object?> Credentials) : IServiceBinding
{
    /// <summary>Hides the credentials, which are confidential.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() =>
        $"DefaultServiceBinding(Name={Name}, ServiceName={ServiceName}, ServicePlan={ServicePlan}, "
        + $"Tags=[{string.Join(", ", Tags)}], Credentials=(hidden))";
}
