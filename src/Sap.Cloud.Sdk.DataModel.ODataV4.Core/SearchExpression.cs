namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>A <c>$search</c> expression built from quoted terms and <c>AND</c>, <c>OR</c> and <c>NOT</c>.</summary>
/// <remarks>Port of <c>SearchExpression</c> (Java package <c>core</c>).</remarks>
public class SearchExpression
{
    private readonly string _term;

    /// <summary>Initializes a new instance of the <see cref="SearchExpression"/> class from an already formatted term.</summary>
    /// <param name="term">The term.</param>
    protected SearchExpression(string term)
    {
        _term = term;
    }

    internal string Term => _term;

    /// <summary>Creates an expression that searches for a literal.</summary>
    /// <param name="term">The text; it is put in double quotes.</param>
    /// <returns>The expression.</returns>
    /// <exception cref="ArgumentException">The text contains <c>&amp;</c>.</exception>
    public static SearchExpression Of(string term) => new(GetDoubleQuotedString(term));

    /// <summary>Combines this expression with a literal using <c>AND</c>.</summary>
    /// <param name="term">The text.</param>
    /// <returns>The combined expression.</returns>
    public SearchExpression And(string term) => new("(" + _term + " AND " + GetDoubleQuotedString(term) + ")");

    /// <summary>Combines this expression with another one using <c>AND</c>.</summary>
    /// <param name="searchExpression">The other expression.</param>
    /// <returns>The combined expression.</returns>
    public SearchExpression And(SearchExpression searchExpression)
    {
        ArgumentNullException.ThrowIfNull(searchExpression);
        return new("(" + _term + " AND " + searchExpression._term + ")");
    }

    /// <summary>Combines this expression with a literal using <c>OR</c>.</summary>
    /// <param name="term">The text.</param>
    /// <returns>The combined expression.</returns>
    public SearchExpression Or(string term) => new("(" + _term + " OR " + GetDoubleQuotedString(term) + ")");

    /// <summary>Combines this expression with another one using <c>OR</c>.</summary>
    /// <param name="searchExpression">The other expression.</param>
    /// <returns>The combined expression.</returns>
    public SearchExpression Or(SearchExpression searchExpression)
    {
        ArgumentNullException.ThrowIfNull(searchExpression);
        return new("(" + _term + " OR " + searchExpression._term + ")");
    }

    /// <summary>Negates this expression.</summary>
    /// <returns>The negated expression.</returns>
    public SearchExpression Not() => new("NOT " + _term);

    /// <inheritdoc />
    public override string ToString() => _term;

    internal static string GetDoubleQuotedString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('&', StringComparison.Ordinal))
        {
            throw new ArgumentException("Search literal contains a forbidden character '&'.", nameof(text));
        }

        var encoded = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + encoded + "\"";
    }
}
