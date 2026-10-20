using System.Collections;
using System.Globalization;
using System.Text;
using Sap.Cloud.Sdk.DataModel.OData.Exceptions;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>
/// Base class of all entities and complex types of the typed OData V2 model (the "VDM"). It holds the custom fields (properties
/// that are not part of the model) and tracks which fields have been changed.
/// </summary>
/// <remarks>
/// <para>
/// Port of <c>VdmObject</c>. Differences: the type is not generic (typed custom field access lives on <see cref="VdmEntity{TEntity}"/>
/// and <see cref="VdmComplex{TObject}"/>); the abstract <c>getType()</c> is replaced by <see cref="TypeMetadata"/>, from which
/// <c>toMapOfFields</c>, <c>toMapOfNavigationProperties</c>, <c>getKey</c>, equality and JSON conversion are derived; <c>fromMap</c> is
/// omitted.
/// </para>
/// <para>
/// Equality is by value: same type, equal properties (including navigation properties) and equal custom fields. For entities the
/// version identifier takes part; the service path and destination used for lazy loading do not.
/// </para>
/// </remarks>
public abstract class VdmObject
{
    private readonly OrderedDictionary<string, object?> _customFields = [];
    private readonly Dictionary<string, object?> _changedOriginalFields = [];

    /// <summary>The description of the OData properties of this type.</summary>
    protected abstract VdmTypeMetadata TypeMetadata { get; }

    internal VdmTypeMetadata GetMetadata() => TypeMetadata;

    /// <summary>The names of the custom fields.</summary>
    public IReadOnlyCollection<string> CustomFieldNames => _customFields.Keys;

    /// <summary>The custom fields: values of properties that are not part of the model, by name.</summary>
    public IReadOnlyDictionary<string, object?> CustomFields => _customFields;

    internal OrderedDictionary<string, object?> CustomFieldsInternal => _customFields;

    /// <summary>Sets a custom field and remembers the previous value for change tracking.</summary>
    /// <param name="customFieldName">The name of the field.</param>
    /// <param name="value">The new value.</param>
    public void SetCustomField(string customFieldName, object? value)
    {
        ArgumentNullException.ThrowIfNull(customFieldName);
        RememberChangedField(customFieldName, _customFields.TryGetValue(customFieldName, out var previous) ? previous : null);
        _customFields[customFieldName] = value;
    }

    /// <summary>Whether a custom field with the given name exists.</summary>
    /// <param name="customFieldName">The name of the field.</param>
    /// <returns><see langword="true"/> if the field exists.</returns>
    public bool HasCustomField(string customFieldName)
    {
        ArgumentNullException.ThrowIfNull(customFieldName);
        return _customFields.ContainsKey(customFieldName);
    }

    /// <summary>Gets the value of a custom field.</summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="customFieldName">The name of the field.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NoSuchEntityFieldException">There is no custom field with that name.</exception>
    /// <exception cref="InvalidCastException">The value is not of the requested type.</exception>
    public T? GetCustomField<T>(string customFieldName)
    {
        ArgumentNullException.ThrowIfNull(customFieldName);
        if (!_customFields.TryGetValue(customFieldName, out var value))
        {
            throw new NoSuchEntityFieldException("Object has no field with name '" + customFieldName + "'.");
        }
        return value is null ? default : (T)value;
    }

    internal void SetCustomField<TField>(string name, IFieldValueConverter<TField>? converter, TField? value) =>
        SetCustomField(name, converter is null ? value : converter.ToDomain(value));

    internal TField? GetCustomField<TField>(string name, IFieldValueConverter<TField>? converter)
    {
        if (converter is null)
        {
            return GetCustomField<TField>(name);
        }
        if (!_customFields.TryGetValue(name, out var value))
        {
            throw new NoSuchEntityFieldException("Object has no field with name '" + name + "'.");
        }
        return converter.FromDomain(value);
    }

    /// <summary>Gets the values of the model properties that are not navigation properties, by OData name.</summary>
    /// <returns>The values; complex properties are the complex objects.</returns>
    protected internal IReadOnlyDictionary<string, object?> ToMapOfFields()
    {
        var values = new OrderedDictionary<string, object?>();
        foreach (var property in TypeMetadata.FieldProperties)
        {
            values[property.Name] = property.GetValue(this);
        }
        return values;
    }

    /// <summary>Gets the names of the model properties that are not navigation properties.</summary>
    /// <returns>The names.</returns>
    protected internal IReadOnlySet<string> GetSetOfFields() => TypeMetadata.FieldProperties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>Gets the values of the navigation properties that are set, by OData name.</summary>
    /// <returns>The related entity or list of entities.</returns>
    protected internal IReadOnlyDictionary<string, object?> ToMapOfNavigationProperties()
    {
        var values = new OrderedDictionary<string, object?>();
        foreach (var property in TypeMetadata.NavigationProperties)
        {
            var value = property.GetValue(this);
            if (value is not null)
            {
                values[property.Name] = value;
            }
        }
        return values;
    }

    /// <summary>Gets the names of the navigation properties.</summary>
    /// <returns>The names.</returns>
    protected internal IReadOnlySet<string> GetSetOfNavigationProperties() =>
        TypeMetadata.NavigationProperties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>Gets the values of the custom fields.</summary>
    /// <returns>A copy of the custom fields.</returns>
    protected internal IReadOnlyDictionary<string, object?> ToMapOfCustomFields() => new Dictionary<string, object?>(_customFields);

    /// <summary>Gets the names of the custom fields.</summary>
    /// <returns>The names.</returns>
    protected internal IReadOnlySet<string> GetSetOfCustomFields() => _customFields.Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>Gets the values of fields, navigation properties and custom fields.</summary>
    /// <returns>The values by name.</returns>
    protected internal IReadOnlyDictionary<string, object?> ToMap()
    {
        var values = new Dictionary<string, object?>();
        foreach (var (name, value) in ToMapOfFields())
        {
            values[name] = value;
        }
        foreach (var (name, value) in ToMapOfNavigationProperties())
        {
            values[name] = value;
        }
        foreach (var (name, value) in _customFields)
        {
            values[name] = value;
        }
        return values;
    }

    internal IReadOnlyDictionary<string, object?> GetKeyValues() => GetKey();

    /// <summary>Gets the key properties of the object by OData name; empty for complex types.</summary>
    /// <returns>The key.</returns>
    protected virtual IReadOnlyDictionary<string, object?> GetKey()
    {
        var key = new OrderedDictionary<string, object?>();
        foreach (var property in TypeMetadata.KeyProperties)
        {
            key[property.Name] = property.GetValue(this);
        }
        return key;
    }

    /// <summary>
    /// Gets the fields (including custom fields) that have been changed since the object was created or <see cref="ResetChangedFields"/>
    /// was called, with their current value. Fields that were set back to their original value are not reported.
    /// </summary>
    /// <returns>The changed fields by name.</returns>
    public IReadOnlyDictionary<string, object?> GetChangedFields()
    {
        var changedFields = new Dictionary<string, object?>();
        if (_changedOriginalFields.Count == 0)
        {
            return changedFields;
        }
        var currentFields = new Dictionary<string, object?>(ToMapOfFields());
        foreach (var (name, value) in _customFields)
        {
            currentFields[name] = value;
        }
        foreach (var (name, originalValue) in _changedOriginalFields)
        {
            currentFields.TryGetValue(name, out var currentValue);
            if ((originalValue is not null && !ValueEquals(originalValue, currentValue)) || (originalValue is null && currentValue is not null))
            {
                changedFields[name] = currentValue;
            }
        }
        return changedFields;
    }

    /// <summary>Remembers the value of a field before its first change.</summary>
    /// <param name="fieldName">The OData name of the field.</param>
    /// <param name="valueBeforeChange">The value before the change.</param>
    protected void RememberChangedField(string fieldName, object? valueBeforeChange)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        _changedOriginalFields.TryAdd(fieldName, valueBeforeChange);
    }

    /// <summary>Forgets all changes: the current values become the original values.</summary>
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
        foreach (var property in TypeMetadata.Properties)
        {
            if (!ValueEquals(property.GetValue(this), property.GetValue(other)))
            {
                return false;
            }
        }
        if (_customFields.Count != other._customFields.Count)
        {
            return false;
        }
        foreach (var (name, value) in _customFields)
        {
            if (!other._customFields.TryGetValue(name, out var otherValue) || !ValueEquals(value, otherValue))
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetType());
        foreach (var property in TypeMetadata.Properties)
        {
            hash.Add(ValueHashCode(property.GetValue(this)));
        }
        hash.Add(_customFields.Count);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var text = new StringBuilder(GetType().Name).Append('(');
        var first = true;
        foreach (var property in TypeMetadata.Properties)
        {
            text.Append(first ? string.Empty : ", ").Append(property.Name).Append('=').Append(Format(property.GetValue(this)));
            first = false;
        }
        foreach (var (name, value) in _customFields)
        {
            text.Append(first ? string.Empty : ", ").Append(name).Append('=').Append(Format(value));
            first = false;
        }
        return text.Append(')').ToString();
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        string s => s,
        byte[] bytes => Convert.ToBase64String(bytes),
        IDictionary dictionary => "{" + string.Join(", ", dictionary.Keys.Cast<object>().Select(k => k + "=" + Format(dictionary[k]))) + "}",
        IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(Format)) + "]",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    internal static bool ValueEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null || right is null)
        {
            return false;
        }
        if (left is byte[] leftBytes && right is byte[] rightBytes)
        {
            return leftBytes.AsSpan().SequenceEqual(rightBytes);
        }
        if (left is IDictionary leftDictionary && right is IDictionary rightDictionary)
        {
            return leftDictionary.Count == rightDictionary.Count
                && leftDictionary.Keys.Cast<object>().All(k => rightDictionary.Contains(k) && ValueEquals(leftDictionary[k], rightDictionary[k]));
        }
        if (left is IEnumerable leftItems && right is IEnumerable rightItems && left is not string)
        {
            var leftList = leftItems.Cast<object?>().ToList();
            var rightList = rightItems.Cast<object?>().ToList();
            return leftList.Count == rightList.Count && leftList.Zip(rightList).All(pair => ValueEquals(pair.First, pair.Second));
        }
        return left.Equals(right);
    }

    private static int ValueHashCode(object? value) => value switch
    {
        null => 0,
        byte[] bytes => bytes.Length,
        string s => s.GetHashCode(StringComparison.Ordinal),
        IDictionary dictionary => dictionary.Count,
        IEnumerable items => items.Cast<object?>().Aggregate(17, (hash, item) => unchecked((hash * 31) + ValueHashCode(item))),
        _ => value.GetHashCode(),
    };
}
