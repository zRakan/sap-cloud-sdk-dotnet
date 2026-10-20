namespace Sap.Cloud.Sdk.CloudPlatform.Resilience;

/// <summary>
/// Describes which resilience patterns are applied to an operation and how they are configured. Configurations with
/// the same <see cref="Identifier"/> and isolation share circuit breaker, bulkhead, rate limiter and cache state.
/// </summary>
/// <remarks>
/// Java uses mutable fluent setters; here the configuration is an immutable record, modify it with
/// <c>with</c> expressions, for example <c>ResilienceConfiguration.Of("x") with { IsolationMode = ... }</c>.
/// Defaults are identical to Java: timeout, circuit breaker and bulkhead enabled; retry, rate limiter and cache
/// disabled; isolation <see cref="ResilienceIsolationMode.TenantAndUserOptional"/>.
/// </remarks>
public record ResilienceConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="ResilienceConfiguration"/> class.</summary>
    /// <param name="identifier">The identifier that names the resilience state.</param>
    protected ResilienceConfiguration(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        Identifier = identifier;
    }

    /// <summary>The identifier that names the resilience state (and the cache).</summary>
    public string Identifier { get; }

    /// <summary>How state is separated between tenants and principals.</summary>
    public ResilienceIsolationMode IsolationMode { get; init; } = ResilienceIsolationMode.TenantAndUserOptional;

    /// <summary>The timeout configuration.</summary>
    public TimeLimiterConfiguration TimeLimiter { get; init; } = TimeLimiterConfiguration.Of();

    /// <summary>The circuit breaker configuration.</summary>
    public CircuitBreakerConfiguration CircuitBreaker { get; init; } = CircuitBreakerConfiguration.Of();

    /// <summary>The bulkhead configuration.</summary>
    public BulkheadConfiguration Bulkhead { get; init; } = BulkheadConfiguration.Of();

    /// <summary>The cache configuration.</summary>
    public CacheConfiguration Cache { get; init; } = CacheConfiguration.Disabled();

    /// <summary>The retry configuration.</summary>
    public RetryConfiguration Retry { get; init; } = RetryConfiguration.Disabled();

    /// <summary>The rate limiter configuration.</summary>
    public RateLimiterConfiguration RateLimiter { get; init; } = RateLimiterConfiguration.Disabled();

    /// <summary>Creates a configuration with default settings.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The configuration.</returns>
    public static ResilienceConfiguration Of(string identifier) => new(identifier);

    /// <summary>Creates a configuration with default settings, named after a type.</summary>
    /// <param name="serviceClass">The type whose full name is the identifier.</param>
    /// <returns>The configuration.</returns>
    public static ResilienceConfiguration Of(Type serviceClass)
    {
        ArgumentNullException.ThrowIfNull(serviceClass);
        return Of(serviceClass.FullName ?? serviceClass.Name);
    }

    /// <summary>Creates a configuration with all patterns disabled.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The configuration.</returns>
    public static ResilienceConfiguration Empty(string identifier) => new(identifier)
    {
        RateLimiter = RateLimiterConfiguration.Disabled(),
        TimeLimiter = TimeLimiterConfiguration.Disabled(),
        CircuitBreaker = CircuitBreakerConfiguration.Disabled(),
        Bulkhead = BulkheadConfiguration.Disabled(),
        Retry = RetryConfiguration.Disabled(),
        Cache = CacheConfiguration.Disabled(),
    };

    /// <summary>Creates a configuration with all patterns disabled, named after a type.</summary>
    /// <param name="serviceClass">The type whose full name is the identifier.</param>
    /// <returns>The configuration.</returns>
    public static ResilienceConfiguration Empty(Type serviceClass)
    {
        ArgumentNullException.ThrowIfNull(serviceClass);
        return Empty(serviceClass.FullName ?? serviceClass.Name);
    }

    /// <summary>Configuration of the rate limiter.</summary>
    public sealed record RateLimiterConfiguration
    {
        /// <summary>The default time to wait for a permit.</summary>
        public static readonly TimeSpan DefaultTimeoutDuration = TimeSpan.FromSeconds(5);

        /// <summary>The default length of a refresh period.</summary>
        public static readonly TimeSpan DefaultLimitRefreshPeriod = TimeSpan.FromSeconds(1);

        /// <summary>The default number of permits per refresh period.</summary>
        public const int DefaultLimitForPeriod = 20;

        private RateLimiterConfiguration()
        {
        }

        /// <summary>Whether the rate limiter is applied.</summary>
        public bool IsEnabled { get; private init; } = true;

        /// <summary>How long a call waits for a permit before it is rejected.</summary>
        public TimeSpan TimeoutDuration { get; init; } = DefaultTimeoutDuration;

        /// <summary>The length of a refresh period.</summary>
        public TimeSpan LimitRefreshPeriod { get; init; } = DefaultLimitRefreshPeriod;

        /// <summary>The number of permits available per refresh period.</summary>
        public int LimitForPeriod { get; init; } = DefaultLimitForPeriod;

        /// <summary>Creates a disabled configuration.</summary>
        /// <returns>The configuration.</returns>
        public static RateLimiterConfiguration Disabled() => new() { IsEnabled = false };

        /// <summary>Creates an enabled configuration with default values.</summary>
        /// <returns>The configuration.</returns>
        public static RateLimiterConfiguration Of() => new();

        /// <summary>Creates an enabled configuration.</summary>
        /// <param name="timeoutDuration">How long a call waits for a permit.</param>
        /// <param name="limitRefreshPeriod">The length of a refresh period.</param>
        /// <param name="limitForPeriod">The number of permits per refresh period.</param>
        /// <returns>The configuration.</returns>
        public static RateLimiterConfiguration Of(TimeSpan timeoutDuration, TimeSpan limitRefreshPeriod, int limitForPeriod) =>
            new() { TimeoutDuration = timeoutDuration, LimitRefreshPeriod = limitRefreshPeriod, LimitForPeriod = limitForPeriod };
    }

    /// <summary>Configuration of the cache.</summary>
    /// <remarks>
    /// Java's <c>serializable</c> flag and the <c>Serializable...</c> overload of <c>withParameters</c> are not ported:
    /// cached values are kept in memory and never serialized.
    /// </remarks>
    public sealed class CacheConfiguration : IEquatable<CacheConfiguration>
    {
        /// <summary>The default expiration strategy.</summary>
        public const CacheExpirationStrategy DefaultExpirationStrategy = CacheExpirationStrategy.WhenLastModified;

        private CacheConfiguration(
            bool isEnabled,
            TimeSpan expirationDuration,
            CacheExpirationStrategy expirationStrategy,
            IReadOnlyList<object> parameters)
        {
            IsEnabled = isEnabled;
            ExpirationDuration = expirationDuration;
            ExpirationStrategy = expirationStrategy;
            Parameters = parameters;
        }

        /// <summary>Whether caching is applied.</summary>
        public bool IsEnabled { get; }

        /// <summary>How long a value is kept. A value of zero or less disables storing.</summary>
        public TimeSpan ExpirationDuration { get; }

        /// <summary>When the expiration timer starts.</summary>
        public CacheExpirationStrategy ExpirationStrategy { get; }

        /// <summary>Additional components of the cache key, in order. Values with equal parameters share an entry.</summary>
        public IReadOnlyList<object> Parameters { get; }

        /// <summary>Starts building an enabled cache configuration.</summary>
        /// <param name="expirationDuration">How long a value is kept.</param>
        /// <returns>The builder.</returns>
        public static CacheConfigurationBuilder Of(TimeSpan expirationDuration) => new(expirationDuration);

        /// <summary>Creates a disabled cache configuration.</summary>
        /// <returns>The configuration.</returns>
        public static CacheConfiguration Disabled() =>
            new(false, TimeSpan.Zero, DefaultExpirationStrategy, []);

        /// <inheritdoc/>
        public bool Equals(CacheConfiguration? other) =>
            other is not null
            && IsEnabled == other.IsEnabled
            && ExpirationDuration == other.ExpirationDuration
            && ExpirationStrategy == other.ExpirationStrategy
            && Parameters.SequenceEqual(other.Parameters);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as CacheConfiguration);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(IsEnabled);
            hash.Add(ExpirationDuration);
            hash.Add(ExpirationStrategy);
            foreach (var parameter in Parameters)
            {
                hash.Add(parameter);
            }
            return hash.ToHashCode();
        }

        /// <summary>Returns a description that does not reveal the parameters, which may be confidential.</summary>
        /// <returns>The description.</returns>
        public override string ToString() =>
            $"CacheConfiguration {{ IsEnabled = {IsEnabled}, ExpirationDuration = {ExpirationDuration}, "
            + $"ExpirationStrategy = {ExpirationStrategy}, Parameters = {Parameters.Count} }}";

        /// <summary>Builds an enabled <see cref="CacheConfiguration"/>.</summary>
        public sealed class CacheConfigurationBuilder
        {
            private readonly TimeSpan _expirationDuration;
            private CacheExpirationStrategy _expirationStrategy = DefaultExpirationStrategy;

            internal CacheConfigurationBuilder(TimeSpan expirationDuration)
            {
                _expirationDuration = expirationDuration;
            }

            /// <summary>Sets the expiration strategy.</summary>
            /// <param name="expirationStrategy">The strategy.</param>
            /// <returns>This builder.</returns>
            public CacheConfigurationBuilder WithExpirationStrategy(CacheExpirationStrategy expirationStrategy)
            {
                _expirationStrategy = expirationStrategy;
                return this;
            }

            /// <summary>Finishes the configuration with parameters that become part of the cache key.</summary>
            /// <param name="component">The first parameter.</param>
            /// <param name="otherComponents">More parameters.</param>
            /// <returns>The configuration.</returns>
            public CacheConfiguration WithParameters(object component, params object[] otherComponents)
            {
                ArgumentNullException.ThrowIfNull(component);
                ArgumentNullException.ThrowIfNull(otherComponents);
                return new CacheConfiguration(true, _expirationDuration, _expirationStrategy, [component, .. otherComponents]);
            }

            /// <summary>Finishes the configuration without cache key parameters.</summary>
            /// <returns>The configuration.</returns>
            public CacheConfiguration WithoutParameters() =>
                new(true, _expirationDuration, _expirationStrategy, []);
        }
    }

    /// <summary>Configuration of the timeout.</summary>
    public sealed record TimeLimiterConfiguration
    {
        /// <summary>The default timeout.</summary>
        public static readonly TimeSpan DefaultTimeoutDuration = TimeSpan.FromSeconds(30);

        /// <summary>The default for <see cref="ShouldCancelRunningFuture"/>.</summary>
        public const bool DefaultShouldCancelRunningFuture = true;

        /// <summary>Whether the timeout is applied.</summary>
        public bool IsEnabled { get; private init; } = true;

        /// <summary>The maximum duration of one attempt.</summary>
        public TimeSpan TimeoutDuration { get; init; } = DefaultTimeoutDuration;

        /// <summary>
        /// Kept for API compatibility. The operation is always cancelled through its <see cref="CancellationToken"/>
        /// when the timeout elapses; operations that ignore the token keep running in the background.
        /// </summary>
        public bool ShouldCancelRunningFuture { get; init; } = DefaultShouldCancelRunningFuture;

        /// <summary>Creates an enabled configuration with the default timeout.</summary>
        /// <returns>The configuration.</returns>
        public static TimeLimiterConfiguration Of() => new();

        /// <summary>Creates an enabled configuration.</summary>
        /// <param name="timeoutDuration">The timeout.</param>
        /// <returns>The configuration.</returns>
        public static TimeLimiterConfiguration Of(TimeSpan timeoutDuration) => new() { TimeoutDuration = timeoutDuration };

        /// <summary>Creates a disabled configuration.</summary>
        /// <returns>The configuration.</returns>
        public static TimeLimiterConfiguration Disabled() => new() { IsEnabled = false };
    }

    /// <summary>Configuration of the circuit breaker.</summary>
    public sealed record CircuitBreakerConfiguration
    {
        /// <summary>The default failure rate (percent) at which the circuit opens.</summary>
        public const float DefaultFailureRateThreshold = 50;

        /// <summary>The default number of calls that are considered.</summary>
        public const int DefaultClosedBufferSize = 10;

        /// <summary>The default number of probe calls in half-open state.</summary>
        public const int DefaultHalfOpenBufferSize = 5;

        /// <summary>The default time the circuit stays open.</summary>
        public static readonly TimeSpan DefaultWaitDuration = TimeSpan.FromSeconds(10);

        /// <summary>The default duration above which a call is slow.</summary>
        public static readonly TimeSpan DefaultSlowCallDurationThreshold = TimeSpan.FromSeconds(60);

        /// <summary>The default rate (percent) of slow calls at which the circuit opens.</summary>
        public const float DefaultSlowCallRateThreshold = 100;

        /// <summary>Whether the circuit breaker is applied.</summary>
        public bool IsEnabled { get; private init; } = true;

        /// <summary>The failure rate in percent at which the circuit opens.</summary>
        public float FailureRateThreshold { get; init; } = DefaultFailureRateThreshold;

        /// <summary>How long the circuit stays open. The Polly implementation enforces a minimum of 500 ms.</summary>
        public TimeSpan WaitDuration { get; init; } = DefaultWaitDuration;

        /// <summary>The minimum number of calls before the failure rate is evaluated.</summary>
        public int ClosedBufferSize { get; init; } = DefaultClosedBufferSize;

        /// <summary>The number of probe calls in half-open state. Not used by the Polly implementation.</summary>
        public int HalfOpenBufferSize { get; init; } = DefaultHalfOpenBufferSize;

        /// <summary>The duration above which a call is slow. Not used by the Polly implementation.</summary>
        public TimeSpan SlowCallDurationThreshold { get; init; } = DefaultSlowCallDurationThreshold;

        /// <summary>The rate of slow calls in percent at which the circuit opens. Not used by the Polly implementation.</summary>
        public float SlowCallRateThreshold { get; init; } = DefaultSlowCallRateThreshold;

        /// <summary>Creates an enabled configuration with default values.</summary>
        /// <returns>The configuration.</returns>
        public static CircuitBreakerConfiguration Of() => new();

        /// <summary>Creates a disabled configuration.</summary>
        /// <returns>The configuration.</returns>
        public static CircuitBreakerConfiguration Disabled() => new() { IsEnabled = false };
    }

    /// <summary>Configuration of the bulkhead.</summary>
    public sealed record BulkheadConfiguration
    {
        /// <summary>The default maximum number of concurrent calls.</summary>
        public const int DefaultMaxConcurrentCalls = 50;

        /// <summary>The default time a call waits for a free slot.</summary>
        public static readonly TimeSpan DefaultMaxWaitDuration = TimeSpan.FromSeconds(60);

        /// <summary>Whether the bulkhead is applied.</summary>
        public bool IsEnabled { get; private init; } = true;

        /// <summary>The maximum number of concurrent calls.</summary>
        public int MaxConcurrentCalls { get; init; } = DefaultMaxConcurrentCalls;

        /// <summary>How long a call waits for a free slot before it is rejected.</summary>
        public TimeSpan MaxWaitDuration { get; init; } = DefaultMaxWaitDuration;

        /// <summary>Creates an enabled configuration with default values.</summary>
        /// <returns>The configuration.</returns>
        public static BulkheadConfiguration Of() => new();

        /// <summary>Creates a disabled configuration.</summary>
        /// <returns>The configuration.</returns>
        public static BulkheadConfiguration Disabled() => new() { IsEnabled = false };
    }

    /// <summary>Configuration of retries.</summary>
    public sealed record RetryConfiguration
    {
        /// <summary>The default number of attempts, including the first.</summary>
        public const int DefaultMaxAttempts = 3;

        /// <summary>The default wait between attempts.</summary>
        public static readonly TimeSpan DefaultWaitDuration = TimeSpan.FromMilliseconds(500);

        /// <summary>The default predicate: retry on every exception.</summary>
        public static readonly Func<Exception, bool> DefaultRetryOnExceptionPredicate = _ => true;

        private RetryConfiguration()
        {
        }

        /// <summary>Whether retries are applied.</summary>
        public bool IsEnabled { get; private init; }

        /// <summary>The maximum number of attempts, including the first.</summary>
        public int MaxAttempts { get; private init; } = 1;

        /// <summary>The wait between attempts.</summary>
        public TimeSpan WaitDuration { get; init; } = DefaultWaitDuration;

        /// <summary>Decides whether a failed attempt is retried.</summary>
        public Func<Exception, bool> RetryOnExceptionPredicate { get; init; } = DefaultRetryOnExceptionPredicate;

        /// <summary>Creates an enabled configuration.</summary>
        /// <param name="maxAttempts">The maximum number of attempts, including the first.</param>
        /// <param name="waitDuration">The wait between attempts.</param>
        /// <returns>The configuration.</returns>
        public static RetryConfiguration Of(int maxAttempts, TimeSpan waitDuration) =>
            new() { IsEnabled = true, MaxAttempts = maxAttempts, WaitDuration = waitDuration };

        /// <summary>Creates an enabled configuration with the default wait.</summary>
        /// <param name="maxAttempts">The maximum number of attempts, including the first.</param>
        /// <returns>The configuration.</returns>
        public static RetryConfiguration Of(int maxAttempts) => new() { IsEnabled = true, MaxAttempts = maxAttempts };

        /// <summary>Creates a disabled configuration.</summary>
        /// <returns>The configuration.</returns>
        public static RetryConfiguration Disabled() => new();
    }
}
