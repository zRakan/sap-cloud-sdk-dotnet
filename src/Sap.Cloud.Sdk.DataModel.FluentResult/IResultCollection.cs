namespace Sap.Cloud.Sdk.Result;

/// <summary>A collection result element.</summary>
public interface IResultCollection : IResultElement, IEnumerable<IResultElement>
{
    /// <summary>Collects the child with the given name from every object element.</summary>
    /// <param name="filterName">The name of the child to collect.</param>
    /// <returns>The collected view.</returns>
    ICollectedResult Collect(string filterName);

    /// <summary>Deserializes every element to <typeparamref name="T"/> and returns them as a list.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<T> AsList<T>();

    /// <summary>Deserializes every element to <typeparamref name="T"/> and returns them as a set.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<T> AsSet<T>();

    /// <summary>Converts every element to <see cref="bool"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<bool> AsBooleanList();

    /// <summary>Converts every element to <see cref="bool"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<bool> AsBooleanSet();

    /// <summary>Converts every element to <see cref="byte"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<byte> AsByteList();

    /// <summary>Converts every element to <see cref="byte"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<byte> AsByteSet();

    /// <summary>Converts every element to <see cref="char"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<char> AsCharacterList();

    /// <summary>Converts every element to <see cref="char"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<char> AsCharacterSet();

    /// <summary>Converts every element to <see cref="string"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<string> AsStringList();

    /// <summary>Converts every element to <see cref="string"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<string> AsStringSet();

    /// <summary>Converts every element to <see cref="int"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<int> AsIntegerList();

    /// <summary>Converts every element to <see cref="int"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<int> AsIntegerSet();

    /// <summary>Converts every element to <see cref="short"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<short> AsShortList();

    /// <summary>Converts every element to <see cref="short"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<short> AsShortSet();

    /// <summary>Converts every element to <see cref="long"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<long> AsLongList();

    /// <summary>Converts every element to <see cref="long"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<long> AsLongSet();

    /// <summary>Converts every element to <see cref="float"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<float> AsFloatList();

    /// <summary>Converts every element to <see cref="float"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<float> AsFloatSet();

    /// <summary>Converts every element to <see cref="double"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<double> AsDoubleList();

    /// <summary>Converts every element to <see cref="double"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<double> AsDoubleSet();

    /// <summary>Converts every element to <see cref="System.Numerics.BigInteger"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<System.Numerics.BigInteger> AsBigIntegerList();

    /// <summary>Converts every element to <see cref="System.Numerics.BigInteger"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<System.Numerics.BigInteger> AsBigIntegerSet();

    /// <summary>Converts every element to <see cref="decimal"/> and returns them as a list.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlyList<decimal> AsBigDecimalList();

    /// <summary>Converts every element to <see cref="decimal"/> and returns them as a set.</summary>
    /// <returns>The set.</returns>
    /// <exception cref="NotSupportedException">An element cannot be converted.</exception>
    IReadOnlySet<decimal> AsBigDecimalSet();
}
