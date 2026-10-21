namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>Ports of <c>NamingUtilsTest</c> and <c>NamingContextTest</c>, plus tests of the case format conversion and the service name mappings.</summary>
public class NamingUtilsTests
{
    [Theory]
    [InlineData("Something with special – unicode stuff", "SomethingWithSpecialUnicodeStuff")]
    [InlineData("Something with other — unicode stuff", "SomethingWithOtherUnicodeStuff")]
    [InlineData("Something with an & symbol", "SomethingWithAnAndSymbol")]
    [InlineData("Something with an / symbol", "SomethingWithAnOrSymbol")]
    [InlineData("Northwind.svc", "NorthwindSvc")]
    [InlineData("01 Some Service", "A01Some")]
    [InlineData("01234", "A01234")]
    [InlineData("Some,Enumeration", "SomeEnumeration")]
    [InlineData("Lower,enumeration", "LowerEnumeration")]
    [InlineData("KeepCamelCase", "KeepCamelCase")]
    [InlineData("handle_snake_case", "HandleSnakeCase")]
    [InlineData("OData Service for Business Partner", "BusinessPartner")]
    [InlineData("API MATERIAL DOCUMENT SRV", "MATERIALDOCUMENTSRV")]
    [InlineData("MATERIALDOCUMENTSRV", "MATERIALDOCUMENTSRV")]
    [InlineData("SDK Grocery Store", "SDKGroceryStore")]
    [InlineData("Product_Api_Service", "Product")]
    public void ServiceNameToBaseClassName(string input, string expected) =>
        Assert.Equal(expected, NamingUtils.ServiceNameToBaseClassName(input));

    [Theory]
    [InlineData("API_MATERIAL_DOCUMENT_SRV", "API MATERIAL DOCUMENT SRV")]
    [InlineData("Some Name", "Some Name")]
    public void ApiNameToServiceTitle(string input, string expected) => Assert.Equal(expected, NamingUtils.ApiNameToServiceTitle(input));

    [Fact]
    public void Capitalize()
    {
        Assert.Equal("", NamingUtils.Capitalize(""));
        Assert.Equal("All Lower Case Words", NamingUtils.Capitalize("all lower case words"));
        Assert.Equal("ALL UPPER CASE WORDS", NamingUtils.Capitalize("ALL UPPER CASE WORDS"));
        Assert.Equal("Words-use-whitespace", NamingUtils.Capitalize("words-use-whitespace"));
        Assert.Equal("Special Chars / ? : – ; \"", NamingUtils.Capitalize("special chars / ? : – ; \""));
        Assert.Equal("Multiple     Whitespaces    Are  Kept", NamingUtils.Capitalize("multiple     whitespaces    are  kept"));
        Assert.Equal("Different Kinds\tOf\nWhitespace", NamingUtils.Capitalize("different kinds\tof\nwhitespace"));
        Assert.Equal(" Whitespace As First", NamingUtils.Capitalize(" whitespace as first"));
        Assert.Equal("Whitespace As Last ", NamingUtils.Capitalize("whitespace as last "));
        Assert.Equal("Last Word Is Character A", NamingUtils.Capitalize("last word is character a"));
    }

    [Fact]
    public void Uncapitalize()
    {
        Assert.Equal("", NamingUtils.Uncapitalize(""));
        Assert.Equal("all lower case words", NamingUtils.Uncapitalize("all lower case words"));
        Assert.Equal("aLL uPPER cASE wORDS", NamingUtils.Uncapitalize("ALL UPPER CASE WORDS"));
        Assert.Equal("words-Use-Whitespace", NamingUtils.Uncapitalize("Words-Use-Whitespace"));
        Assert.Equal("multiple     whitespaces    are  kept", NamingUtils.Uncapitalize("Multiple     Whitespaces    Are  Kept"));
        Assert.Equal("different kinds\tof\nwhitespace", NamingUtils.Uncapitalize("Different Kinds\tOf\nWhitespace"));
        Assert.Equal(" whitespace as first", NamingUtils.Uncapitalize(" Whitespace As First"));
        Assert.Equal("last word is character a", NamingUtils.Uncapitalize("Last Word Is Character A"));
    }

    [Theory]
    [InlineData("URLAddress", "urlAddress")]
    [InlineData("Address", "address")]
    [InlineData("ABC", "abC")]
    [InlineData("", "")]
    public void UncapitalizeLeadingAcronym(string input, string expected) => Assert.Equal(expected, NamingUtils.UncapitalizeLeadingAcronym(input));

    [Fact]
    public void ReplaceInvalidIdentifierCharacters()
    {
        Assert.Equal("Abc_1", NamingUtils.ReplaceInvalidIdentifierCharacters("Abc-1", "_"));
        Assert.Equal("Abc1", NamingUtils.ReplaceInvalidIdentifierCharacters("Abc-1", null));
        Assert.Equal("a1", NamingUtils.ReplaceInvalidIdentifierCharacters("1a1", null));
        Assert.Equal("_x", NamingUtils.ReplaceInvalidIdentifierCharacters("$_x", null));
        Assert.Equal("Größe", NamingUtils.ReplaceInvalidIdentifierCharacters("Größe", null));
    }

    [Theory]
    [InlineData("class", "classParameter")]
    [InlineData("Class", "classParameter")]
    [InlineData("Name", "name")]
    [InlineData("_Name", "_Name")]
    public void ToParameterName(string input, string expected) => Assert.Equal(expected, NamingUtils.ToParameterName(input));

    [Theory]
    [InlineData("UpperCamel", "LowerUnderscore", "SDK_Grocery_Store", "s_d_k__grocery__store")]
    [InlineData("UpperCamel", "LowerUnderscore", "FooBar", "foo_bar")]
    [InlineData("UpperCamel", "LowerUnderscore", "fooBar", "foo_bar")]
    [InlineData("LowerUnderscore", "UpperCamel", "foo_bar", "FooBar")]
    [InlineData("LowerUnderscore", "UpperCamel", "s_d_k__grocery", "SDKGrocery")]
    [InlineData("UpperCamel", "LowerCamel", "FooBar", "fooBar")]
    [InlineData("UpperCamel", "UpperUnderscore", "FooBar", "FOO_BAR")]
    [InlineData("UpperCamel", "UpperCamel", "FooBar", "FooBar")]
    public void CaseFormats_FollowGuava(string from, string to, string input, string expected) =>
        Assert.Equal(expected, CaseFormats.Convert(Enum.Parse<CaseFormat>(from), Enum.Parse<CaseFormat>(to), input));

    // ----- NamingContext --------------------------------------------------------------------------------------------

    [Fact]
    public void NamingContext_CaseSensitive_DoesNotRenameNamesDifferingInCase()
    {
        var context = new NamingContext(NameEquality.CaseSensitive);
        Assert.Equal("abc", context.EnsureUniqueName("abc"));
        Assert.Equal("ABC", context.EnsureUniqueName("ABC"));
        Assert.Equal("abc_2", context.EnsureUniqueName("abc"));
    }

    [Fact]
    public void NamingContext_CaseInsensitive_RenamesNamesDifferingInCase()
    {
        var context = new NamingContext(NameEquality.CaseInsensitive);
        Assert.Equal("abc", context.EnsureUniqueName("abc"));
        Assert.Equal("ABC_2", context.EnsureUniqueName("ABC"));
    }

    [Fact]
    public void NamingContext_CaseInsensitive_CountsOccurrences()
    {
        var context = new NamingContext(NameEquality.CaseInsensitive);
        Assert.Equal("eMailAddress", context.EnsureUniqueName("eMailAddress"));
        Assert.Equal("emailAddress_2", context.EnsureUniqueName("emailAddress"));
        Assert.Equal("emailAddress_3", context.EnsureUniqueName("emailAddress"));
        Assert.Equal("EMAILADDRESS_4", context.EnsureUniqueName("EMAILADDRESS"));
    }

    [Fact]
    public void NamingContext_RenamedNames_AreReserved()
    {
        var context = new NamingContext();
        Assert.Equal("a", context.EnsureUniqueName("a"));
        Assert.Equal("a_2", context.EnsureUniqueName("a"));
        Assert.Equal("a_2_2", context.EnsureUniqueName("a_2"));
        Assert.Equal("a_3", context.EnsureUniqueName("a"));
    }

    [Fact]
    public void NamingContext_ReservedNames_AreTaken()
    {
        var context = new NamingContext(NameEquality.CaseInsensitive);
        context.Reserve(["Fields", "ToString"]);
        Assert.True(context.AlreadyUses("fields"));
        Assert.Equal("Fields_2", context.EnsureUniqueName("Fields"));
        Assert.False(context.AlreadyUses("Other"));
    }

    // ----- ServiceNameMappings --------------------------------------------------------------------------------------

    [Fact]
    public void ServiceNameMappings_ReadsJavaFilesAndKeepsAllEntries()
    {
        var directory = Support.Harness.NewTemporaryDirectory();
        var file = Path.Combine(directory, "mappings.properties");
        File.WriteAllText(file, "# SDK Grocery Store\nSDK_Grocery_Store.className = SdkGroceryStore\nSDK_Grocery_Store.packageName = sdkgrocerystore\n\nignored line\n");

        var mappings = ServiceNameMappings.Load(file);
        Assert.Equal("SdkGroceryStore", mappings.GetString("SDK_Grocery_Store.className"));
        Assert.Equal("sdkgrocerystore", mappings.GetString("SDK_Grocery_Store.packageName"));
        Assert.Null(mappings.GetString("Other.className"));

        mappings.PutString("Other.className", "Other", "Another service");
        Assert.True(mappings.Save());
        var text = File.ReadAllText(file);
        Assert.Equal("\n# SDK Grocery Store\nSDK_Grocery_Store.className = SdkGroceryStore\nSDK_Grocery_Store.packageName = sdkgrocerystore\n\n# Another service\nOther.className = Other\n", text);
        Assert.False(ServiceNameMappings.Load(file).Save());
    }

    [Fact]
    public void ServiceNameMappings_WithoutFile_AreKeptInMemory()
    {
        var mappings = ServiceNameMappings.Load(null);
        mappings.PutString("a.className", "A");
        Assert.Equal("A", mappings.GetString("a.className"));
        Assert.False(mappings.Save());
    }
}
