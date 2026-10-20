using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.DataModel.OData.Client;

/// <summary>Logging helpers that evaluate nothing unless the level is enabled.</summary>
internal static class LogExtensions
{
#pragma warning disable CA2254 // The message templates are static at all call sites.
    internal static void Debug(this ILogger log, string message, params object?[] args)
    {
        if (log.IsEnabled(LogLevel.Debug))
        {
            log.Log(LogLevel.Debug, message, args);
        }
    }

    internal static void Debug(this ILogger log, Exception exception, string message, params object?[] args)
    {
        if (log.IsEnabled(LogLevel.Debug))
        {
            log.Log(LogLevel.Debug, exception, message, args);
        }
    }

    internal static void Warn(this ILogger log, string message, params object?[] args)
    {
        if (log.IsEnabled(LogLevel.Warning))
        {
            log.Log(LogLevel.Warning, message, args);
        }
    }

    internal static void Warn(this ILogger log, Exception exception, string message, params object?[] args)
    {
        if (log.IsEnabled(LogLevel.Warning))
        {
            log.Log(LogLevel.Warning, exception, message, args);
        }
    }

    internal static void Error(this ILogger log, string message, params object?[] args)
    {
        if (log.IsEnabled(LogLevel.Error))
        {
            log.Log(LogLevel.Error, message, args);
        }
    }
#pragma warning restore CA2254
}
