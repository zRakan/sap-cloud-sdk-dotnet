using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.ServiceBindings;
using OnBehalf = Sap.Cloud.Sdk.CloudPlatform.Connectivity.OnBehalfOf;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>An additional option for a <see cref="ServiceBindingDestinationOptions"/>.</summary>
/// <typeparam name="T">The type of the option value.</typeparam>
/// <remarks>Port of <c>ServiceBindingDestinationOptions.OptionsEnhancer</c>. Options are identified by their implementing type, and each type can be used once.</remarks>
public interface IOptionsEnhancer<out T>
{
    /// <summary>The option value.</summary>
    T Value { get; }
}

/// <summary>What is needed to create a destination from a service binding.</summary>
public sealed class ServiceBindingDestinationOptions
{
    private readonly IReadOnlyDictionary<Type, object> _furtherOptions;

    private ServiceBindingDestinationOptions(
        IServiceBinding serviceBinding,
        OnBehalfOf onBehalfOf,
        IReadOnlyDictionary<Type, object> furtherOptions)
    {
        ServiceBinding = serviceBinding;
        OnBehalfOf = onBehalfOf;
        _furtherOptions = furtherOptions;
    }

    /// <summary>The service binding.</summary>
    public IServiceBinding ServiceBinding { get; }

    /// <summary>On whose behalf the destination is created.</summary>
    public OnBehalfOf OnBehalfOf { get; }

    /// <summary>Starts building options for the only service binding of a service.</summary>
    /// <param name="identifier">The service.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="DestinationAccessException">There is no or more than one binding for the service.</exception>
    public static Builder ForService(ServiceIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var bindings = DefaultServiceBindingAccessor.Instance.GetServiceBindings()
            .Where(binding => identifier.Equals(binding.GetServiceIdentifier()))
            .ToList();
        if (bindings.Count == 0)
        {
            throw new DestinationAccessException(
                $"Could not find any matching service bindings for service identifier '{identifier}'");
        }
        if (bindings.Count > 1)
        {
            throw new DestinationAccessException(
                $"'{identifier}' is ambiguous in this context since multiple service bindings were found matching this identifier. "
                + "Please make sure only a single instance is bound, or use ForService(IServiceBinding) to pass a service binding explicitly.");
        }
        return ForService(bindings[0]);
    }

    /// <summary>Starts building options for a specific service binding.</summary>
    /// <param name="serviceBinding">The binding.</param>
    /// <returns>The builder.</returns>
    public static Builder ForService(IServiceBinding serviceBinding)
    {
        ArgumentNullException.ThrowIfNull(serviceBinding);
        return new Builder(serviceBinding);
    }

    /// <summary>Gets the value of a further option.</summary>
    /// <typeparam name="TEnhancer">The type of the option.</typeparam>
    /// <typeparam name="TValue">The type of the option's value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> if the option has been set.</returns>
    public bool TryGetOption<TEnhancer, TValue>(out TValue value)
        where TEnhancer : IOptionsEnhancer<TValue>
    {
        if (_furtherOptions.TryGetValue(typeof(TEnhancer), out var option))
        {
            value = ((IOptionsEnhancer<TValue>)option).Value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Lists the binding and the option types, never the credentials.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() =>
        $"ServiceBindingDestinationOptions(serviceBinding={ServiceBinding}, onBehalfOf={OnBehalfOf}, "
        + $"options=[{string.Join(", ", _furtherOptions.Keys.Select(t => t.Name))}])";

    /// <summary>Builds <see cref="ServiceBindingDestinationOptions"/>.</summary>
    public sealed class Builder
    {
        private readonly IServiceBinding _binding;
        private readonly Dictionary<Type, object> _furtherOptions = [];
        private OnBehalf _behalf = OnBehalf.TechnicalUserCurrentTenant;

        internal Builder(IServiceBinding binding) => _binding = binding;

        /// <summary>Sets on whose behalf the destination is created. Defaults to <c>TechnicalUserCurrentTenant</c>.</summary>
        /// <param name="behalf">The behalf.</param>
        /// <returns>This builder.</returns>
        public Builder OnBehalfOf(OnBehalfOf behalf)
        {
            _behalf = behalf;
            return this;
        }

        /// <summary>Adds a further option.</summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        /// <param name="option">The option.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">An option of the same type has already been added.</exception>
        public Builder WithOption<T>(IOptionsEnhancer<T> option)
        {
            ArgumentNullException.ThrowIfNull(option);
            var type = option.GetType();
            if (!_furtherOptions.TryAdd(type, option))
            {
                throw new ArgumentException($"The given option {type.Name} is already in use.", nameof(option));
            }
            return this;
        }

        /// <summary>Creates the options.</summary>
        /// <returns>The options.</returns>
        public ServiceBindingDestinationOptions Build() =>
            new(_binding, _behalf, new Dictionary<Type, object>(_furtherOptions));
    }

    /// <summary>Options that are used within this SDK.</summary>
    public static class Options
    {
        /// <summary>Carries the destination for which a proxy is resolved. Public because the OAuth module lives in another assembly.</summary>
        public sealed class ProxyOptions : IOptionsEnhancer<IHttpDestination>
        {
            private ProxyOptions(IHttpDestination value) => Value = value;

            /// <inheritdoc/>
            public IHttpDestination Value { get; }

            /// <summary>Creates the option.</summary>
            /// <param name="destination">The destination to be proxied.</param>
            /// <returns>The option.</returns>
            public static ProxyOptions DestinationToBeProxied(IHttpDestination destination)
            {
                ArgumentNullException.ThrowIfNull(destination);
                return new ProxyOptions(destination);
            }
        }

        // TODO(resilience): add ResilienceOptions : IOptionsEnhancer<ResilienceConfiguration> once the Resilience module
        // provides ResilienceConfiguration (Java: ServiceBindingDestinationOptions.Options.ResilienceOptions).
    }
}
