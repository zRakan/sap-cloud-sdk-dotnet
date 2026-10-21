using System.Text;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// A minimal, indentation-aware source writer. Lines end with <c>\n</c>; blank lines are never doubled, never follow an opening
/// brace and never precede a closing brace, so that emitters can request separation freely.
/// </summary>
internal sealed class CodeWriter
{
    private const string IndentUnit = "    ";

    private readonly StringBuilder _text = new();
    private int _indent;
    private bool _blankPending;
    private bool _suppressBlank = true;

    /// <summary>Writes a line at the current indentation. An empty text requests a blank line.</summary>
    internal CodeWriter Line(string text = "")
    {
        if (text.Length == 0)
        {
            _blankPending = true;
            return this;
        }

        var closes = text[0] == '}';
        if (_blankPending && !_suppressBlank && !closes)
        {
            _text.Append('\n');
        }
        _blankPending = false;
        _suppressBlank = text == "{" || text.EndsWith('{');

        for (var i = 0; i < _indent; i++)
        {
            _text.Append(IndentUnit);
        }
        _text.Append(text).Append('\n');
        return this;
    }

    /// <summary>Requests a blank line before the next line.</summary>
    internal CodeWriter Blank()
    {
        _blankPending = true;
        return this;
    }

    /// <summary>Writes <paramref name="header"/>, an opening brace and indents; disposing the scope writes the closing brace.</summary>
    internal Scope Block(string header, string closing = "}")
    {
        Line(header);
        Line("{");
        _indent++;
        return new Scope(this, closing);
    }

    /// <summary>Writes an opening brace without a header (for a block that follows a header written separately).</summary>
    internal Scope Braces(string closing = "}")
    {
        Line("{");
        _indent++;
        return new Scope(this, closing);
    }

    /// <summary>Increases the indentation until the scope is disposed.</summary>
    internal Scope Indented()
    {
        _indent++;
        return new Scope(this, null);
    }

    public override string ToString() => _text.ToString();

    internal readonly struct Scope(CodeWriter writer, string? closing) : IDisposable
    {
        public void Dispose()
        {
            writer._indent--;
            if (closing is not null)
            {
                writer.Line(closing);
            }
        }
    }
}
