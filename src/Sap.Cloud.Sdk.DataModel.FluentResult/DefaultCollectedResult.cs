namespace Sap.Cloud.Sdk.Result;

/// <summary>Default implementation of <see cref="ICollectedResult"/>.</summary>
public class DefaultCollectedResult : ICollectedResult
{
    private readonly IReadOnlyList<IResultElement> _resultElements;

    /// <summary>Initializes a new instance of the <see cref="DefaultCollectedResult"/> class.</summary>
    /// <param name="collectedElementName">The name of the child to collect from each object element.</param>
    /// <param name="resultElements">The elements.</param>
    public DefaultCollectedResult(string collectedElementName, IEnumerable<IResultElement> resultElements)
    {
        ArgumentNullException.ThrowIfNull(collectedElementName);
        ArgumentNullException.ThrowIfNull(resultElements);
        CollectedElementName = collectedElementName;
        _resultElements = resultElements.ToList();
    }

    /// <summary>The name of the collected child.</summary>
    public string CollectedElementName { get; }

    /// <summary>The elements.</summary>
    public IReadOnlyList<IResultElement> ResultElements => _resultElements;

    /// <inheritdoc />
    public override string ToString() =>
        $"DefaultCollectedResult(CollectedElementName={CollectedElementName}, ResultElements=[{string.Join(", ", _resultElements)}])";

    private List<T> Collect<T>(Func<IResultElement, T> extractor) =>
        CollectionExtraction.CollectNested(_resultElements, CollectedElementName, extractor);

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
