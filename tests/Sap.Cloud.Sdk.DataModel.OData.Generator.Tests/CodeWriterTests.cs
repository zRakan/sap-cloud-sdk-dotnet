namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

public class CodeWriterTests
{
    [Fact]
    public void Blocks_IndentAndCloseWithBraces()
    {
        var w = new CodeWriter();
        using (w.Block("public class A"))
        {
            w.Line("int x;");
            using (w.Block("void M()"))
            {
                w.Line("x++;");
            }
        }

        Assert.Equal("public class A\n{\n    int x;\n    void M()\n    {\n        x++;\n    }\n}\n", w.ToString());
    }

    [Fact]
    public void BlankLines_AreNeverDoubled_NorLeadingNorBeforeClosingBraces()
    {
        var w = new CodeWriter();
        w.Blank().Blank();
        w.Line("a;");
        w.Blank().Blank().Blank();
        using (w.Block("class B"))
        {
            w.Blank();
            w.Line("b;");
            w.Blank();
        }
        w.Blank();

        Assert.Equal("a;\n\nclass B\n{\n    b;\n}\n", w.ToString());
    }

    [Fact]
    public void EmptyBlocks_AndIndentedContinuations_AreSupported()
    {
        var w = new CodeWriter();
        w.Line("public A(int x)");
        using (w.Indented())
        {
            w.Line(": base(x)");
        }
        using (w.Braces())
        {
        }

        Assert.Equal("public A(int x)\n    : base(x)\n{\n}\n", w.ToString());
    }

    [Fact]
    public void ClosingText_IsConfigurable()
    {
        var w = new CodeWriter();
        using (w.Block("var x = new[]", "};"))
        {
            w.Line("1,");
        }

        Assert.Equal("var x = new[]\n{\n    1,\n};\n", w.ToString());
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("a\"b\\c", "\"a\\\"b\\\\c\"")]
    [InlineData("line\nbreak\ttab", "\"line\\nbreak\\ttab\"")]
    [InlineData("\u0001", "\"\\u0001\"")]
    [InlineData("Größe", "\"Größe\"")]
    public void Literal_EscapesStrings(string value, string expected) => Assert.Equal(expected, CSharp.Literal(value));

    [Theory]
    [InlineData("a & b < c > d", "a &amp; b &lt; c &gt; d")]
    [InlineData("  many \n  spaces\t here ", "many spaces here")]
    public void Xml_EscapesAndCollapsesWhitespace(string value, string expected) => Assert.Equal(expected, CSharp.Xml(value));

    [Fact]
    public void XmlAttribute_EscapesQuotes() => Assert.Equal("a &quot;b&quot; &amp;", CSharp.XmlAttribute("a \"b\" &"));

    [Fact]
    public void Identifier_PrefixesKeywords()
    {
        Assert.Equal("@class", CSharp.Identifier("class"));
        Assert.Equal("name", CSharp.Identifier("name"));
    }

    [Theory]
    [InlineData("Binary", "byte[]", true)]
    [InlineData("String", "string", true)]
    [InlineData("Int32", "int", false)]
    [InlineData("Time", "TimeOnly", false)]
    [InlineData("DateTimeOffset", "DateTimeOffset", false)]
    [InlineData("Decimal", "decimal", false)]
    [InlineData("Guid", "Guid", false)]
    public void SimpleTypes_MapToClrTypes(string kind, string expected, bool isReference)
    {
        var parsed = Enum.Parse<EdmSimpleKind>(kind);
        Assert.Equal(expected, CSharp.TypeName(parsed));
        Assert.Equal(isReference, CSharp.IsReferenceType(parsed));
    }

    [Fact]
    public void EveryPrimitiveType_HasAClrType()
    {
        foreach (var kind in Enum.GetValues<EdmSimpleKind>())
        {
            Assert.False(string.IsNullOrEmpty(CSharp.TypeName(kind)), kind.ToString());
        }
    }

    [Theory]
    [InlineData("GET", "HttpMethod.Get")]
    [InlineData("Post", "HttpMethod.Post")]
    [InlineData("patch", "HttpMethod.Patch")]
    [InlineData("MERGE", "new HttpMethod(\"MERGE\")")]
    public void HttpMethods_MapToExpressions(string method, string expected) => Assert.Equal(expected, ServiceModelBuilder.HttpMethodExpression(method));
}
