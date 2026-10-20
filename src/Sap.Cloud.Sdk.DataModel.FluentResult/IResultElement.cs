namespace Sap.Cloud.Sdk.Result;

/// <summary>
/// A node of a generic result tree: a primitive, an object or a collection.
/// </summary>
/// <remarks>Port of <c>ResultElement</c>. Java's <c>UnsupportedOperationException</c> is <see cref="NotSupportedException"/>; Java <c>char</c>/<c>byte</c> map to <see cref="char"/>/<see cref="byte"/>.</remarks>
public interface IResultElement
{
    /// <summary>Whether this element is a primitive.</summary>
    bool IsResultPrimitive { get; }

    /// <summary>Whether this element is a collection.</summary>
    bool IsResultCollection { get; }

    /// <summary>Whether this element is an object.</summary>
    bool IsResultObject { get; }

    /// <summary>Gets this element as a primitive.</summary>
    /// <returns>The primitive.</returns>
    /// <exception cref="NotSupportedException">The element is not a primitive.</exception>
    IResultPrimitive GetAsPrimitive();

    /// <summary>Gets this element as a collection.</summary>
    /// <returns>The collection.</returns>
    /// <exception cref="NotSupportedException">The element is not a collection.</exception>
    IResultCollection GetAsCollection();

    /// <summary>Gets this element as an object.</summary>
    /// <returns>The object.</returns>
    /// <exception cref="NotSupportedException">The element is not an object.</exception>
    IResultObject GetAsObject();

    /// <summary>Gets the value as <see cref="bool"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    bool AsBoolean();

    /// <summary>Gets the value as <see cref="byte"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    byte AsByte();

    /// <summary>Gets the value as <see cref="char"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    char AsCharacter();

    /// <summary>Gets the value as <see cref="string"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    string AsString();

    /// <summary>Gets the value as <see cref="int"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    int AsInteger();

    /// <summary>Gets the value as <see cref="short"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    short AsShort();

    /// <summary>Gets the value as <see cref="long"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    long AsLong();

    /// <summary>Gets the value as <see cref="float"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    float AsFloat();

    /// <summary>Gets the value as <see cref="double"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    double AsDouble();

    /// <summary>Gets the value as <see cref="System.Numerics.BigInteger"/>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    System.Numerics.BigInteger AsBigInteger();

    /// <summary>Gets the value as <see cref="decimal"/> (Java: <c>BigDecimal</c>).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">The conversion is not possible.</exception>
    decimal AsBigDecimal();
}
