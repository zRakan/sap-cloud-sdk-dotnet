using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>Base class for named OData parameters: entity keys and function parameters.</summary>
public abstract class AbstractODataParameters
{
    private readonly OrderedDictionary<string, IOperandSingle> _parameters = [];

    /// <summary>Initializes a new instance of the <see cref="AbstractODataParameters"/> class.</summary>
    /// <param name="protocol">The protocol.</param>
    internal AbstractODataParameters(IODataProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        Protocol = protocol;
    }

    /// <summary>The protocol.</summary>
    public IODataProtocol Protocol { get; }

    internal IReadOnlyDictionary<string, IOperandSingle> Parameters => _parameters;

    /// <summary>Serializes the parameters percent-encoded.</summary>
    /// <returns>The encoded string, for example <c>(Id='1')</c>.</returns>
    public virtual string ToEncodedString() => ToStringInternal(UriEncodingStrategy.Regular, ParameterFormat.PathShort);

    /// <summary>Serializes the parameters percent-encoded with the given strategy.</summary>
    /// <param name="strategy">The encoding strategy.</param>
    /// <returns>The encoded string.</returns>
    public virtual string ToEncodedString(UriEncodingStrategy strategy) => ToStringInternal(strategy, ParameterFormat.PathShort);

    /// <summary>Serializes the parameters without encoding.</summary>
    /// <returns>The string.</returns>
    public override string ToString() => ToStringInternal(UriEncodingStrategy.None, ParameterFormat.PathShort);

    internal void AddParameterInternal(string parameterName, object? value)
    {
        ArgumentNullException.ThrowIfNull(parameterName);
        if (_parameters.ContainsKey(parameterName))
        {
            throw new ArgumentException(
                "Cannot add parameter \"" + parameterName + "\": A parameter by that name already exists.",
                nameof(parameterName));
        }
        _parameters[parameterName] = Expressions.CreateOperand(value);
    }

    internal void AddParameterSetInternal(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        foreach (var (name, value) in properties)
        {
            AddParameterInternal(name, value);
        }
    }

    internal string ToStringInternal(UriEncodingStrategy strategy, ParameterFormat format)
    {
        Func<string, string> encoder = format == ParameterFormat.Query ? v => strategy.EscapeQuery(v) : v => strategy.EscapePath(v);
        if (format == ParameterFormat.PathShort && _parameters.Count == 1)
        {
            var singleValue = _parameters.Values.First();
            return "(" + encoder(singleValue.GetExpression(Protocol)) + ")";
        }
        var keyDelimiter = format == ParameterFormat.Query ? "&" : ",";
        var keys = string.Join(
            keyDelimiter,
            _parameters.Select(param => param.Key + "=" + encoder(param.Value.GetExpression(Protocol))));
        return format == ParameterFormat.Query ? keys : "(" + keys + ")";
    }

    internal enum ParameterFormat
    {
        Path,
        PathShort,
        Query,
    }
}

/// <summary>The key of an entity: a set of named key properties.</summary>
public class ODataEntityKey : AbstractODataParameters
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataEntityKey>();

    /// <summary>Initializes a new instance of the <see cref="ODataEntityKey"/> class.</summary>
    /// <param name="protocol">The protocol.</param>
    public ODataEntityKey(IODataProtocol protocol)
        : base(protocol)
    {
    }

    /// <summary>The names of the key properties.</summary>
    public IReadOnlyCollection<string> FieldNames => Parameters.Keys.ToList();

    /// <summary>Creates a key from a map of property names and values.</summary>
    /// <param name="key">The key properties.</param>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The key.</returns>
    public static ODataEntityKey Of(IEnumerable<KeyValuePair<string, object?>> key, IODataProtocol protocol) =>
        new ODataEntityKey(protocol).AddKeyProperties(key);

    /// <summary>Adds a key property.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value (supported types: see <see cref="Expressions.CreateOperand(object?)"/>).</param>
    /// <returns>This key.</returns>
    /// <exception cref="ArgumentException">The property already exists.</exception>
    public ODataEntityKey AddKeyProperty(string propertyName, object? value)
    {
        AddParameterInternal(propertyName, value);
        return this;
    }

    /// <summary>Adds several key properties.</summary>
    /// <param name="properties">The properties.</param>
    /// <returns>This key.</returns>
    public ODataEntityKey AddKeyProperties(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        AddParameterSetInternal(properties);
        return this;
    }

    /// <inheritdoc />
    public override string ToEncodedString(UriEncodingStrategy strategy)
    {
        WarnIfEmpty();
        return base.ToEncodedString(strategy);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        WarnIfEmpty();
        return base.ToString();
    }

    private void WarnIfEmpty()
    {
        if (Parameters.Count == 0)
        {
            Log.Warn(
                "The current entity key is empty. Using it within a request will cause the request to fail as empty entity keys are not allowed.");
        }
    }
}

/// <summary>The parameters of a function import or function.</summary>
public class ODataFunctionParameters : AbstractODataParameters
{
    private readonly ParameterFormat _parameterFormat;

    /// <summary>Initializes a new instance of the <see cref="ODataFunctionParameters"/> class.</summary>
    /// <param name="protocol">The protocol.</param>
    public ODataFunctionParameters(IODataProtocol protocol)
        : base(protocol)
    {
        _parameterFormat = protocol.IsEqualTo(ODataProtocol.V2) ? ParameterFormat.Query : ParameterFormat.Path;
    }

    /// <summary>Creates parameters from a map.</summary>
    /// <param name="parameters">The parameter names and values.</param>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The parameters.</returns>
    public static ODataFunctionParameters Of(IEnumerable<KeyValuePair<string, object?>> parameters, IODataProtocol protocol)
    {
        var functionParameters = new ODataFunctionParameters(protocol);
        functionParameters.AddParameterSetInternal(parameters);
        return functionParameters;
    }

    /// <summary>Creates parameters without any entry.</summary>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The parameters.</returns>
    public static ODataFunctionParameters Empty(IODataProtocol protocol) => new(protocol);

    /// <summary>Adds a parameter.</summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>These parameters.</returns>
    public ODataFunctionParameters AddParameter(string parameterName, object? value)
    {
        AddParameterInternal(parameterName, value);
        return this;
    }

    /// <inheritdoc />
    public override string ToEncodedString() => ToStringInternal(UriEncodingStrategy.Regular, _parameterFormat);

    /// <inheritdoc />
    public override string ToEncodedString(UriEncodingStrategy strategy) => ToStringInternal(strategy, _parameterFormat);

    /// <inheritdoc />
    public override string ToString() => ToStringInternal(UriEncodingStrategy.None, _parameterFormat);
}
