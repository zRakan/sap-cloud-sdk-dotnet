using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sap.Cloud.Sdk.CloudPlatform;

/// <summary>
/// The logger factory used by the SDK's static accessors.
/// </summary>
/// <remarks>
/// Replaces SLF4J's static <c>LoggerFactory</c>. Defaults to <see cref="NullLoggerFactory"/>; set
/// <see cref="LoggerFactory"/> at startup (the ASP.NET Core integration does this automatically).
/// </remarks>
public static class SdkLogging
{
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    /// <summary>The logger factory. Setting <see langword="null"/> restores the no-op default.</summary>
    public static ILoggerFactory LoggerFactory
    {
        get => _loggerFactory;
        set => _loggerFactory = value ?? NullLoggerFactory.Instance;
    }

    /// <summary>Creates a logger for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type whose name is used as the logger category.</typeparam>
    /// <returns>The logger.</returns>
    public static ILogger CreateLogger<T>() => new ForwardingLogger(typeof(T).FullName ?? typeof(T).Name);

    /// <summary>Creates a logger for the given category.</summary>
    /// <param name="categoryName">The category name.</param>
    /// <returns>The logger.</returns>
    public static ILogger CreateLogger(string categoryName) => new ForwardingLogger(categoryName);

    /// <summary>
    /// Resolves the underlying logger from the factory that is current at call time, so loggers held in static fields
    /// pick up a factory configured later at startup.
    /// </summary>
    private sealed class ForwardingLogger(string categoryName) : ILogger
    {
        private (ILoggerFactory Factory, ILogger Logger)? _cached;

        private ILogger Current
        {
            get
            {
                var factory = _loggerFactory;
                var cached = _cached;
                if (cached is { } c && ReferenceEquals(c.Factory, factory))
                {
                    return c.Logger;
                }
                var logger = factory.CreateLogger(categoryName);
                _cached = (factory, logger);
                return logger;
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => Current.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => Current.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Current.Log(logLevel, eventId, state, exception, formatter);
    }
}
