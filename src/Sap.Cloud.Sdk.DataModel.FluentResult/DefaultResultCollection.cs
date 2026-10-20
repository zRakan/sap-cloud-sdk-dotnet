using System.Collections;

namespace Sap.Cloud.Sdk.Result;

/// <summary>Default implementation of <see cref="IResultCollection"/>.</summary>
public class DefaultResultCollection : IResultCollection
{
    private const string NotPrimitive = "Cannot get collection as primitive.";
    private readonly IReadOnlyList<IResultElement> _resultElements;

    /// <summary>Initializes a new instance of the <see cref="DefaultResultCollection"/> class.</summary>
    /// <param name="resultElements">The elements.</param>
    public DefaultResultCollection(IEnumerable<IResultElement> resultElements)
    {
        ArgumentNullException.ThrowIfNull(resultElements);
        _resultElements = resultElements.ToList();
    }

    /// <summary>The elements.</summary>
    public IReadOnlyList<IResultElement> ResultElements => _resultElements;

    /// <inheritdoc />
    public bool IsResultPrimitive => false;

    /// <inheritdoc />
    public bool IsResultCollection => true;

    /// <inheritdoc />
    public bool IsResultObject => false;

    /// <inheritdoc />
    public IResultPrimitive GetAsPrimitive() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public IResultCollection GetAsCollection() => this;

    /// <inheritdoc />
    public IResultObject GetAsObject() => throw new NotSupportedException("Cannot get collection as object.");

    /// <inheritdoc />
    public ICollectedResult Collect(string filterName) => new DefaultCollectedResult(filterName, _resultElements);

    /// <inheritdoc />
    public IEnumerator<IResultElement> GetEnumerator() => _resultElements.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() => $"DefaultResultCollection(ResultElements=[{string.Join(", ", _resultElements)}])";

    private List<T> Collect<T>(Func<IResultElement, T> extractor) =>
        CollectionExtraction.Collect(_resultElements, "Failed to collect nested elements.", extractor);

    /// <inheritdoc />
    public bool AsBoolean() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public byte AsByte() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public char AsCharacter() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public string AsString() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public int AsInteger() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public short AsShort() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public long AsLong() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public float AsFloat() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public double AsDouble() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public System.Numerics.BigInteger AsBigInteger() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public decimal AsBigDecimal() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public IReadOnlyList<T> AsList<T>() => Collect(new GenericObjectExtractor<T>().Extract);

    /// <inheritdoc />
    public IReadOnlySet<T> AsSet<T>() => CollectionExtraction.ToSet(Collect(new GenericObjectExtractor<T>().Extract));

    /// <inheritdoc />
    public IReadOnlyList<bool> AsBooleanList() => Collect(e => e.AsBoolean());

    /// <inheritdoc />
    public IReadOnlySet<bool> AsBooleanSet() => CollectionExtraction.ToSet(Collect(e => e.AsBoolean()));

    /// <inheritdoc />
    public IReadOnlyList<byte> AsByteList() => Collect(e => e.AsByte());

    /// <inheritdoc />
    public IReadOnlySet<byte> AsByteSet() => CollectionExtraction.ToSet(Collect(e => e.AsByte()));

    /// <inheritdoc />
    public IReadOnlyList<char> AsCharacterList() => Collect(e => e.AsCharacter());

    /// <inheritdoc />
    public IReadOnlySet<char> AsCharacterSet() => CollectionExtraction.ToSet(Collect(e => e.AsCharacter()));

    /// <inheritdoc />
    public IReadOnlyList<string> AsStringList() => Collect(e => e.AsString());

    /// <inheritdoc />
    public IReadOnlySet<string> AsStringSet() => CollectionExtraction.ToSet(Collect(e => e.AsString()));

    /// <inheritdoc />
    public IReadOnlyList<int> AsIntegerList() => Collect(e => e.AsInteger());

    /// <inheritdoc />
    public IReadOnlySet<int> AsIntegerSet() => CollectionExtraction.ToSet(Collect(e => e.AsInteger()));

    /// <inheritdoc />
    public IReadOnlyList<short> AsShortList() => Collect(e => e.AsShort());

    /// <inheritdoc />
    public IReadOnlySet<short> AsShortSet() => CollectionExtraction.ToSet(Collect(e => e.AsShort()));

    /// <inheritdoc />
    public IReadOnlyList<long> AsLongList() => Collect(e => e.AsLong());

    /// <inheritdoc />
    public IReadOnlySet<long> AsLongSet() => CollectionExtraction.ToSet(Collect(e => e.AsLong()));

    /// <inheritdoc />
    public IReadOnlyList<float> AsFloatList() => Collect(e => e.AsFloat());

    /// <inheritdoc />
    public IReadOnlySet<float> AsFloatSet() => CollectionExtraction.ToSet(Collect(e => e.AsFloat()));

    /// <inheritdoc />
    public IReadOnlyList<double> AsDoubleList() => Collect(e => e.AsDouble());

    /// <inheritdoc />
    public IReadOnlySet<double> AsDoubleSet() => CollectionExtraction.ToSet(Collect(e => e.AsDouble()));

    /// <inheritdoc />
    public IReadOnlyList<System.Numerics.BigInteger> AsBigIntegerList() => Collect(e => e.AsBigInteger());

    /// <inheritdoc />
    public IReadOnlySet<System.Numerics.BigInteger> AsBigIntegerSet() => CollectionExtraction.ToSet(Collect(e => e.AsBigInteger()));

    /// <inheritdoc />
    public IReadOnlyList<decimal> AsBigDecimalList() => Collect(e => e.AsBigDecimal());

    /// <inheritdoc />
    public IReadOnlySet<decimal> AsBigDecimalSet() => CollectionExtraction.ToSet(Collect(e => e.AsBigDecimal()));
}
