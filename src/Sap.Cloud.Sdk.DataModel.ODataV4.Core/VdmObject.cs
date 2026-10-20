using System.Collections;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>
/// Base class of all generated entities and complex types.
/// </summary>
/// <remarks>
/// Port of <c>VdmObject&lt;ObjectT&gt;</c>. Java's wildcard <c>VdmObject&lt;?&gt;</c> is this non-generic class.
/// Instead of reflecting over <c>@ElementName</c> fields, a generated type describes its properties once in a
/// <see cref="VdmTypeInfo"/>. Custom fields are exposed as read-only views. Equality compares the declared properties
/// and navigation properties (custom fields are excluded, as they are <c>transient</c> in Java).
/// </remarks>
public abstract class VdmObject
{
    /// <summary>The names of the annotation that carries the OData type of an object.</summary>
    public static readonly IReadOnlyList<string> ODataTypeAnnotations = ["@odata.type", "@type"];

    /// <summary>The names of the annotation that carries the ETag of an entity.</summary>
    public static readonly IReadOnlyList<string> ODataVersionAnnotations = ["@odata.etag", "@etag"];

    private readonly OrderedDictionary<string, object?> _customFields = [];
    private readonly Dictionary<string, object?> _changedOriginalFields = [];

    /// <summary>The description of the generated type.</summary>
    protected internal abstract VdmTypeInfo TypeInfo { get; }

    /// <summary>The qualified OData type name, for example <c>Trippin.Person</c>.</summary>
    public string ODataType => TypeInfo.ODataType;

    /// <summary>Gets the names of the custom fields (properties unknown to the generated type).</summary>
    /// <returns>The names.</returns>
    public IReadOnlyCollection<string> GetCustomFieldNames() => _customFields.Keys;

    /// <summary>Gets the custom fields.</summary>
    /// <returns>A read-only view on the custom fields.</returns>
    public IReadOnlyDictionary<string, object?> GetCustomFields() => _customFields;

    /// <summary>Sets a custom field. The change is recorded.</summary>
    /// <param name="customFieldName">The name of the field.</param>
    /// <param name="value">The value.</param>
    public void SetCustomField(string customFieldName, object? value)
    {
        ArgumentNullException.ThrowIfNull(customFieldName);
        _customFields.TryGetValue(customFieldName, out var previous);
        RememberChangedField(customFieldName, previous);
        _customFields[customFieldName] = value;
    }

    /// <summary>Sets a custom field. The change is recorded.</summary>
    /// <param name="customField">A reference to the field.</param>
    /// <param name="value">The value.</param>
    public void SetCustomField(IFieldReference customField, object? value)
    {
        ArgumentNullException.ThrowIfNull(customField);
        SetCustomField(customField.FieldName, value);
    }

    /// <summary>Checks whether the custom field exists.</summary>
    /// <param name="customFieldName">The name of the field.</param>
    /// <returns><see langword="true"/> if the field exists.</returns>
    public bool HasCustomField(string customFieldName) => _customFields.ContainsKey(customFieldName);

    /// <summary>Checks whether the custom field exists.</summary>
    /// <param name="customField">A reference to the field.</param>
    /// <returns><see langword="true"/> if the field exists.</returns>
    public bool HasCustomField(IFieldReference customField)
    {
        ArgumentNullException.ThrowIfNull(customField);
        return HasCustomField(customField.FieldName);
    }

    /// <summary>Gets the value of a custom field.</summary>
    /// <typeparam name="TField">The expected type of the value.</typeparam>
    /// <param name="customFieldName">The name of the field.</param>
    /// <returns>The value.</returns>
    /// <exception cref="KeyNotFoundException">The field does not exist (Java: <c>NoSuchElementException</c>).</exception>
    public TField? GetCustomField<TField>(string customFieldName)
    {
        if (!_customFields.TryGetValue(customFieldName, out var value))
        {
            throw new KeyNotFoundException($"Object has no field with name '{customFieldName}'.");
        }

        return (TField?)value;
    }

    /// <summary>Gets the value of a custom field.</summary>
    /// <typeparam name="TField">The expected type of the value.</typeparam>
    /// <param name="customField">A reference to the field.</param>
    /// <returns>The value.</returns>
    /// <exception cref="KeyNotFoundException">The field does not exist.</exception>
    public TField? GetCustomField<TField>(IFieldReference customField)
    {
        ArgumentNullException.ThrowIfNull(customField);
        return GetCustomField<TField>(customField.FieldName);
    }

    /// <summary>Gets the annotations that are sent with the object, such as <c>@odata.type</c>.</summary>
    /// <returns>The annotations.</returns>
    public virtual IReadOnlyDictionary<string, string> GetAnnotationProperties() =>
        new Dictionary<string, string> { [ODataTypeAnnotations[0]] = "#" + ODataType };

    /// <summary>Gets the key of the object. Entities override this to add their key properties.</summary>
    /// <returns>The key.</returns>
    protected internal virtual ODataEntityKey GetKey() => new(ODataProtocol.V4);

    /// <summary>Gets the values of all simple and complex properties.</summary>
    /// <returns>The values by OData name, in declaration order.</returns>
    internal OrderedDictionary<string, object?> ToMapOfFields()
    {
        var values = new OrderedDictionary<string, object?>();
        foreach (var property in TypeInfo.Fields)
        {
            values[property.Name] = property.GetValue(this);
        }

        return values;
    }

    /// <summary>Gets the values of the navigation properties that are set.</summary>
    /// <returns>The values by OData name, in declaration order.</returns>
    internal OrderedDictionary<string, object?> ToMapOfNavigationProperties()
    {
        var values = new OrderedDictionary<string, object?>();
        foreach (var property in TypeInfo.Navigations)
        {
            var value = property.GetValue(this);
            if (value is not null)
            {
                values[property.Name] = value;
            }
        }

        return values;
    }

    /// <summary>Gets the values of all properties, navigation properties and custom fields.</summary>
    /// <returns>The values by OData name.</returns>
    internal OrderedDictionary<string, object?> ToMap()
    {
        var values = ToMapOfFields();
        foreach (var (key, value) in ToMapOfNavigationProperties())
        {
            values[key] = value;
        }

        foreach (var (key, value) in _customFields)
        {
            values[key] = value;
        }

        return values;
    }

    /// <summary>Gets the simple, complex and custom fields that were changed since the object was read or created.</summary>
    /// <returns>The current values of the changed fields.</returns>
    public IReadOnlyDictionary<string, object?> GetChangedFields()
    {
        var changed = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in ToMapOfFields())
        {
            if (IsFieldChanged(key, value))
            {
                changed[key] = value;
            }
        }

        foreach (var (key, value) in _customFields)
        {
            if (IsFieldChanged(key, value))
            {
                changed[key] = value;
            }
        }

        return changed;
    }

    private bool IsFieldChanged(string fieldName, object? currentValue)
    {
        _changedOriginalFields.TryGetValue(fieldName, out var overriddenValue);
        if (currentValue is null && overriddenValue is null)
        {
            return false;
        }

        if (_changedOriginalFields.ContainsKey(fieldName) && !ValueEquals(currentValue, overriddenValue))
        {
            return true;
        }

        if (currentValue is VdmObject nested)
        {
            return nested.GetChangedFields().Count > 0;
        }

        if (currentValue is IEnumerable items and not string)
        {
            return items.OfType<VdmObject>().Any(item => item.GetChangedFields().Count > 0);
        }

        return false;
    }

    /// <summary>Remembers the value of a field before its first change.</summary>
    /// <param name="fieldName">The OData name of the field.</param>
    /// <param name="valueBeforeChange">The value before the change.</param>
    protected void RememberChangedField(string fieldName, object? valueBeforeChange) =>
        _changedOriginalFields.TryAdd(fieldName, valueBeforeChange);

    /// <summary>Forgets all recorded changes, so that the object counts as unchanged.</summary>
    public void ResetChangedFields() => _changedOriginalFields.Clear();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (VdmObject)obj;
        return PropertyMapsEqual(ToMapOfFields(), other.ToMapOfFields())
            && PropertyMapsEqual(ToMapOfNavigationProperties(), other.ToMapOfNavigationProperties());
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetType());
        foreach (var (key, value) in ToMapOfFields())
        {
            hash.Add(key);
            if (value is not (null or VdmObject or IEnumerable) || value is string)
            {
                hash.Add(value);
            }
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var fields = string.Join(", ", ToMapOfFields().Select(f => $"{f.Key}={Format(f.Value)}"));
        return $"{GetType().Name}({fields})";
    }

    internal static bool ValueEquals(object? first, object? second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first is null || second is null)
        {
            return false;
        }

        if (first is string || second is string)
        {
            return first.Equals(second);
        }

        if (first is IEnumerable firstItems && second is IEnumerable secondItems)
        {
            var left = firstItems.Cast<object?>().ToList();
            var right = secondItems.Cast<object?>().ToList();
            return left.Count == right.Count && left.Zip(right).All(pair => ValueEquals(pair.First, pair.Second));
        }

        return first.Equals(second);
    }

    private static bool PropertyMapsEqual(OrderedDictionary<string, object?> first, OrderedDictionary<string, object?> second) =>
        first.Count == second.Count && first.All(entry => second.TryGetValue(entry.Key, out var other) && ValueEquals(entry.Value, other));

    private static string Format(object? value) => value switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(Format)) + "]",
        _ => value.ToString() ?? string.Empty,
    };
}
