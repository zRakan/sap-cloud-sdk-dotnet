using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>Thread-safe default implementation of <see cref="IThreadContext"/>.</summary>
public class DefaultThreadContext : IThreadContext
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultThreadContext>();

    private readonly ConcurrentDictionary<string, ThreadContextProperty> _properties = new(StringComparer.Ordinal);

    /// <summary>A snapshot of all properties.</summary>
    public IReadOnlyDictionary<string, ThreadContextProperty> Properties => new Dictionary<string, ThreadContextProperty>(_properties);

    /// <inheritdoc/>
    public Result<T> GetPropertyValue<T>(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_properties.TryGetValue(name, out var property))
        {
            return Result.Failure<T>(new ThreadContextPropertyNotFoundException(name));
        }
        return property.Value.Bind(value => value is T typed
            ? Result.Success(typed)
            : Result.Failure<T>(new ThreadContextPropertyException(
                $"ThreadContextProperty '{name}' is of type {value.GetType().FullName}, not {typeof(T).FullName}.")));
    }

    /// <inheritdoc/>
    public void SetPropertyIfAbsent(string name, ThreadContextProperty value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Setting property '{Name}' to value: {Value}.", name, value);
        }
        _properties.TryAdd(name, value);
    }

    /// <inheritdoc/>
    public void SetPropertyIfAbsent(string name, Func<ThreadContextProperty> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(valueFactory);
        if (_properties.ContainsKey(name))
        {
            return;
        }
        ThreadContextProperty value;
        try
        {
            value = valueFactory();
        }
        catch (Exception e) when (e is not ThreadContextPropertyException)
        {
            throw new ThreadContextPropertyException($"Failed to create value for property '{name}'.", e);
        }
        SetPropertyIfAbsent(name, value);
    }

    /// <inheritdoc/>
    public void SetProperty(string name, ThreadContextProperty value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug("Setting property '{Name}' to value: {Value}.", name, value);
        }
        _properties[name] = value;
    }

    /// <inheritdoc/>
    public ThreadContextProperty? RemoveProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _properties.TryRemove(name, out var removed) ? removed : null;
    }

    /// <inheritdoc/>
    public bool ContainsProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _properties.ContainsKey(name);
    }

    /// <inheritdoc/>
    public IThreadContext Duplicate()
    {
        var duplicate = new DefaultThreadContext();
        foreach (var (key, value) in _properties)
        {
            duplicate._properties[key] = value.Copy();
        }
        return duplicate;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"DefaultThreadContext(properties={{{string.Join(", ", _properties.Select(p => $"{p.Key}={p.Value}"))}}})";
}
