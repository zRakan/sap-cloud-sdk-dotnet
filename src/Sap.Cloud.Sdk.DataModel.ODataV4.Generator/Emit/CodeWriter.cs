using System.Text;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Emit;

/// <summary>An indentation-aware writer for C# source. Output uses <c>\n</c> line breaks and four-space indentation.</summary>
internal sealed class CodeWriter
{
    private const string IndentUnit = "    ";

    private readonly StringBuilder _text = new();
    private int _indent;
    private bool _gapPending;
    private bool _atBlockStart = true;

    /// <summary>Writes a line at the current indentation; an empty line is written without indentation.</summary>
    /// <param name="line">The text.</param>
    public void Line(string line = "")
    {
        if (line.Length == 0)
        {
            Blank();
            return;
        }

        FlushGap();
        for (var i = 0; i < _indent; i++)
        {
            _text.Append(IndentUnit);
        }

        _text.Append(line).Append('\n');
        _atBlockStart = false;
    }

    /// <summary>Writes an empty line unless the previous line is empty or opens a block.</summary>
    public void Blank() => _gapPending = true;

    /// <summary>Writes <paramref name="header"/>, then <c>{</c>, the body indented, and <c>}</c>.</summary>
    /// <param name="header">The line before the brace; may be empty for a bare block.</param>
    /// <param name="body">Writes the content of the block.</param>
    public void Block(string header, Action body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (header.Length > 0)
        {
            Line(header);
        }

        Line("{");
        _indent++;
        _atBlockStart = true;
        body();
        _indent--;
        _gapPending = false;
        Line("}");
    }

    /// <summary>Like <see cref="Block"/> but closes the brace with a semicolon.</summary>
    /// <param name="header">The line before the brace.</param>
    /// <param name="body">Writes the content of the block.</param>
    public void BlockWithSemicolon(string header, Action body)
    {
        ArgumentNullException.ThrowIfNull(body);
        Line(header);
        Line("{");
        _indent++;
        _atBlockStart = true;
        body();
        _indent--;
        _gapPending = false;
        Line("};");
    }

    /// <summary>Writes a block of lines at one more level of indentation without braces.</summary>
    /// <param name="body">Writes the lines.</param>
    public void Indented(Action body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _indent++;
        body();
        _indent--;
    }

    /// <inheritdoc />
    public override string ToString() => _text.ToString();

    private void FlushGap()
    {
        if (_gapPending && !_atBlockStart && _text.Length > 0)
        {
            _text.Append('\n');
        }

        _gapPending = false;
    }
}
