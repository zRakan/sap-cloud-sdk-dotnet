namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>Ports of Java's <c>NamingUtilsTest</c>, <c>NamingContextTest</c>, <c>S4HanaNamingStrategyTest</c>, <c>SimpleNamingStrategyTest</c> and <c>EdmServiceTest</c>.</summary>
public sealed class NamingTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // NamingUtils

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
    [InlineData("Product Api Service", "Product")]
    [InlineData("TripPinRESTierService", "TripPinRESTier")]
    public void ServiceNameToBaseClassName_converts_titles(string input, string expected) =>
        Assert.Equal(expected, NamingUtils.ServiceNameToBaseClassName(input));

    [Fact]
    public void Capitalize_upper_cases_the_first_letter_of_every_word()
    {
        Assert.Equal(string.Empty, NamingUtils.Capitalize(string.Empty));
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
    public void Uncapitalize_lower_cases_the_first_letter_of_every_word()
    {
        Assert.Equal(string.Empty, NamingUtils.Uncapitalize(string.Empty));
        Assert.Equal("all lower case words", NamingUtils.Uncapitalize("all lower case words"));
        Assert.Equal("aLL uPPER cASE wORDS", NamingUtils.Uncapitalize("ALL UPPER CASE WORDS"));
        Assert.Equal("words-Use-Whitespace", NamingUtils.Uncapitalize("Words-Use-Whitespace"));
        Assert.Equal("multiple     whitespaces    are  kept", NamingUtils.Uncapitalize("Multiple     Whitespaces    Are  Kept"));
        Assert.Equal(" whitespace as first", NamingUtils.Uncapitalize(" Whitespace As First"));
        Assert.Equal("whitespace as last ", NamingUtils.Uncapitalize("Whitespace As Last "));
    }

    [Theory]
    [InlineData("UserName", "userName")]
    [InlineData("ID", "id")]
    [InlineData("URLAddress", "urlAddress")]
    [InlineData("ABCIndicator", "abcIndicator")]
    [InlineData("A", "a")]
    [InlineData("already", "already")]
    [InlineData("_Leading", "_Leading")]
    public void ToCamelCase_lower_cases_a_leading_acronym(string input, string expected) =>
        Assert.Equal(expected, NamingUtils.ToCamelCase(input));

    [Theory]
    [InlineData("class", true)]
    [InlineData("object", true)]
    [InlineData("string", true)]
    [InlineData("Class", false)]
    [InlineData("var", false)]
    public void IsCSharpKeyword_knows_reserved_keywords_only(string name, bool expected) =>
        Assert.Equal(expected, NamingUtils.IsCSharpKeyword(name));

    // ---------------------------------------------------------------------------------------------------------------
    // NamingContext

    [Fact]
    public void Case_sensitive_context_keeps_names_that_differ_in_case()
    {
        var context = new NamingContext(NameEquality.CaseSensitive);

        Assert.Equal("abc", context.EnsureUniqueName("abc"));
        Assert.Equal("ABC", context.EnsureUniqueName("ABC"));
    }

    [Fact]
    public void Case_insensitive_context_renames_names_that_differ_in_case()
    {
        var context = new NamingContext(NameEquality.CaseInsensitive);

        Assert.Equal("abc", context.EnsureUniqueName("abc"));
        Assert.Equal("ABC_2", context.EnsureUniqueName("ABC"));
        Assert.Equal("abc_3", context.EnsureUniqueName("abc"));
    }

    [Fact]
    public void Context_numbers_repeated_names_and_skips_taken_suffixes()
    {
        var context = new NamingContext();

        Assert.Equal("Name", context.EnsureUniqueName("Name"));
        Assert.Equal("Name_2", context.EnsureUniqueName("Name"));
        Assert.Equal("Name_3", context.EnsureUniqueName("Name"));

        var taken = new NamingContext();
        taken.EnsureUniqueName("A_2");
        taken.EnsureUniqueName("A");
        Assert.Equal("A_3", taken.EnsureUniqueName("A"));
    }

    [Fact]
    public void Reserved_names_are_taken_without_being_renamed()
    {
        var context = new NamingContext();
        context.Reserve(["Fields", "All"]);

        Assert.True(context.AlreadyUses("Fields"));
        Assert.False(context.AlreadyUses("Other"));
        Assert.Equal("Fields_2", context.EnsureUniqueName("Fields"));
        Assert.Equal("All_2", context.EnsureUniqueName("All"));
    }

    [Fact]
    public void Context_reports_renames()
    {
        var messages = new List<string>();
        var context = new NamingContext(NameEquality.CaseSensitive, messages.Add);

        context.EnsureUniqueName("Name");
        context.EnsureUniqueName("Name");

        var message = Assert.Single(messages);
        Assert.Contains("Name_2", message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // DefaultNamingStrategy (S4Hana rules)

    [Fact]
    public void Default_strategy_uses_labels_by_default()
    {
        Assert.Equal(NameSource.Label, new DefaultNamingStrategy().NameSource);
        Assert.Equal(NameSource.Label, new SimpleNamingStrategy().NameSource);
    }

    [Fact]
    public void Default_strategy_class_names_from_names()
    {
        var sut = new DefaultNamingStrategy(NameSource.Name);

        Assert.Equal("AddressEmailAddress", sut.GenerateClassName("A_AddressEmailAddressType", null));
        Assert.Equal("ComparisonResult", sut.GenerateClassName("ComparisonResult", null));
        Assert.Equal("TRIALBALANCEResult", sut.GenerateClassName("C_TRIALBALANCEResult", null));
        Assert.Equal("GLAccount", sut.GenerateClassName("GLAccount", null));
        Assert.Equal("BusObjects", sut.GenerateClassName("I_BusObjectsType", null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateClassName("To_", null));
    }

    [Fact]
    public void Default_strategy_class_names_from_labels()
    {
        var sut = new DefaultNamingStrategy(NameSource.Label);

        Assert.Equal("SomeLabel", sut.GenerateClassName("SomeEntityName", "Some label"));
        Assert.Equal("SomeEntityName", sut.GenerateClassName("SomeEntityName", string.Empty));
        Assert.Equal("SomeEntityName", sut.GenerateClassName("SomeEntityName", null));
    }

    [Theory]
    [InlineData("A_SomeEntity", "SomeEntity")]
    [InlineData("C_SomeEntity", "SomeEntity")]
    [InlineData("D_SomeEntity", "SomeEntity")]
    [InlineData("E_SomeEntity", "SomeEntity")]
    [InlineData("I_SomeEntity", "SomeEntity")]
    [InlineData("P_SomeEntity", "SomeEntity")]
    [InlineData("R_SomeEntity", "SomeEntity")]
    [InlineData("S_SomeEntity", "SomeEntity")]
    [InlineData("to_SomeEntity", "SomeEntity")]
    [InlineData("To_SomeEntity", "SomeEntity")]
    [InlineData("X_SomeEntity", "SomeEntity")]
    [InlineData("X_A_SomeEntity", "A_SomeEntity")]
    [InlineData("YY1_SomeEntity", "SomeEntity")]
    [InlineData("Z_SomeEntity", "SomeEntity")]
    [InlineData("B_SomeEntity", "B_SomeEntity")]
    [InlineData("SAP_SomeEntity", "SAP_SomeEntity")]
    [InlineData("B_A_SomeEntity", "B_A_SomeEntity")]
    public void Default_strategy_removes_entity_prefixes_from_names_but_not_from_labels(string name, string expected)
    {
        Assert.Equal(expected, new DefaultNamingStrategy(NameSource.Name).GenerateClassName(name, null));
        Assert.Equal(name, new DefaultNamingStrategy(NameSource.Label).GenerateClassName("SomeEntityName", name), ignoreCase: true);
        Assert.Equal(expected, new DefaultNamingStrategy(NameSource.Name).GenerateOperationMethodName(name, null));
    }

    [Theory]
    [InlineData("SomeEntityType", "SomeEntity")]
    [InlineData("SomeEntity_", "SomeEntity")]
    [InlineData("SomeEntity_Type", "SomeEntity")]
    [InlineData("SomeEntityType_", "SomeEntityType")]
    public void Default_strategy_removes_class_name_suffixes(string name, string expected)
    {
        Assert.Equal(expected, new DefaultNamingStrategy(NameSource.Name).GenerateClassName(name, null));
        Assert.Equal(expected, new DefaultNamingStrategy(NameSource.Label).GenerateClassName("SomeEntityName", name));
    }

    [Theory]
    [InlineData("SAP_SomeEntity", "SomeEntity")]
    [InlineData("to_SomeEntity", "SomeEntity")]
    [InlineData("To_SomeEntity", "SomeEntity")]
    [InlineData("YY1_SomeEntity", "SomeEntity")]
    [InlineData("A_SomeEntity", "A_SomeEntity")]
    [InlineData("A_YY1_SomeEntity", "A_YY1_SomeEntity")]
    public void Default_strategy_removes_property_prefixes(string name, string expected)
    {
        var sut = new DefaultNamingStrategy(NameSource.Name);

        Assert.Equal(expected, sut.GeneratePropertyName(name, null));
        Assert.Equal(expected, sut.GenerateNavigationPropertyName(name));
        Assert.Equal(expected, sut.GenerateEnumMemberName(name));
        Assert.Equal(NamingUtils.ToCamelCase(expected), sut.GenerateParameterName(name, null));
    }

    [Fact]
    public void Default_strategy_property_names()
    {
        var sut = new DefaultNamingStrategy(NameSource.Name);

        Assert.Equal("ABCIndicator", sut.GeneratePropertyName("ABCIndicator", null));
        Assert.Equal("AdditionalCurrency1", sut.GeneratePropertyName("AdditionalCurrency1", null));
        Assert.Equal("AlternativeGLAccount_T", sut.GeneratePropertyName("AlternativeGLAccount_T", null));
        Assert.Equal("Cancel_ac", sut.GeneratePropertyName("Cancel_ac", null));
        Assert.Equal("Class", sut.GeneratePropertyName("Class", null));
        Assert.Equal("Something", sut.GeneratePropertyName("something", null));
        Assert.Throws<InvalidOperationException>(() => sut.GeneratePropertyName("SAP_", null));
        Assert.Equal("SomeLabel", new DefaultNamingStrategy(NameSource.Label).GeneratePropertyName("someEntityName", "Some label"));
    }

    [Fact]
    public void Default_strategy_navigation_property_names_have_no_prefix()
    {
        var sut = new DefaultNamingStrategy(NameSource.Name);

        Assert.Equal("CompanyCodeDetails", sut.GenerateNavigationPropertyName("CompanyCodeDetails"));
        Assert.Equal("DocStatusVH", sut.GenerateNavigationPropertyName("to_DocStatusVH"));
        Assert.Equal("Friends", sut.GenerateNavigationPropertyName("Friends"));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateNavigationPropertyName("SAP_"));
    }

    [Fact]
    public void Default_strategy_operation_method_names()
    {
        var sut = new DefaultNamingStrategy(NameSource.Name);

        Assert.Equal("Release", sut.GenerateOperationMethodName("Release", null));
        Assert.Equal("BOMComparison", sut.GenerateOperationMethodName("BOMComparison", null));
        Assert.Equal("SendMail", sut.GenerateOperationMethodName("sendMail", null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateOperationMethodName("To_", null));
        Assert.Equal("SomeLabel", new DefaultNamingStrategy(NameSource.Label).GenerateOperationMethodName("someEntityName", "Some label"));
    }

    [Fact]
    public void Default_strategy_parameter_names_are_camel_case_and_avoid_keywords()
    {
        var sut = new DefaultNamingStrategy(NameSource.Name);

        Assert.Equal("release", sut.GenerateParameterName("Release", null));
        Assert.Equal("bomComparison", sut.GenerateParameterName("BOMComparison", null));
        Assert.Equal("userName", sut.GenerateParameterName("userName", null));
        Assert.Equal("classParameter", sut.GenerateParameterName("Class", null));
        Assert.Equal("objectParameter", sut.GenerateParameterName("object", null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateParameterName("SAP_", null));
        Assert.Equal("someLabel", new DefaultNamingStrategy(NameSource.Label).GenerateParameterName("someEntityName", "Some label"));
    }

    [Fact]
    public void Default_strategy_enum_member_names()
    {
        var sut = new DefaultNamingStrategy(NameSource.Name);

        Assert.Equal("Male", sut.GenerateEnumMemberName("Male"));
        Assert.Equal("Feature1", sut.GenerateEnumMemberName("Feature1"));
        Assert.Equal("Draft", sut.GenerateEnumMemberName("draft"));
        Assert.Equal("Notavalid", sut.GenerateEnumMemberName("Not a-valid"));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // SimpleNamingStrategy

    [Fact]
    public void Simple_strategy_class_names_from_names()
    {
        var sut = new SimpleNamingStrategy(NameSource.Name);

        Assert.Equal("SimpleName", sut.GenerateClassName("SimpleName", null));
        Assert.Equal("_LeadingUnderscore", sut.GenerateClassName("_LeadingUnderscore", null));
        Assert.Equal("Type", sut.GenerateClassName("Type", null));
        Assert.Equal("Class", sut.GenerateClassName("class", null));
        Assert.Equal("UrlAddress", sut.GenerateClassName("URLAddress", null));
        Assert.Equal("C_TRIALBALANCEResult", sut.GenerateClassName("C_TRIALBALANCEResult", null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateClassName(string.Empty, null));
    }

    [Fact]
    public void Simple_strategy_class_names_from_labels()
    {
        var sut = new SimpleNamingStrategy(NameSource.Label);

        Assert.Equal("SimpleName", sut.GenerateClassName("SomeEntityName", "SimpleName"));
        Assert.Equal("SimpleName", sut.GenerateClassName("SomeEntityName", "Simple Name"));
        Assert.Equal("SomeEntityName", sut.GenerateClassName("SomeEntityName", string.Empty));
        Assert.Equal("SomeEntityName", sut.GenerateClassName("SomeEntityName", null));
    }

    [Fact]
    public void Simple_strategy_property_and_navigation_names()
    {
        var sut = new SimpleNamingStrategy(NameSource.Name);

        Assert.Equal("SimpleName", sut.GeneratePropertyName("SimpleName", null));
        Assert.Equal("_LeadingUnderscore", sut.GeneratePropertyName("_LeadingUnderscore", null));
        Assert.Equal("UrlAddress", sut.GeneratePropertyName("URLAddress", null));
        Assert.Equal("C_TRIALBALANCEResult", sut.GeneratePropertyName("C_TRIALBALANCEResult", null));
        Assert.Equal("UrlAddress", sut.GenerateNavigationPropertyName("URLAddress"));
        Assert.Throws<InvalidOperationException>(() => sut.GeneratePropertyName(string.Empty, null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateNavigationPropertyName(string.Empty));
    }

    [Fact]
    public void Simple_strategy_parameter_names_throw_for_keywords()
    {
        var sut = new SimpleNamingStrategy(NameSource.Name);

        Assert.Equal("simpleName", sut.GenerateParameterName("SimpleName", null));
        Assert.Equal("urlAddress", sut.GenerateParameterName("URLAddress", null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateParameterName("class", null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateParameterName(string.Empty, null));

        var labels = new SimpleNamingStrategy(NameSource.Label);
        Assert.Equal("simpleName", labels.GenerateParameterName("someParameterName", "Simple Name"));
        Assert.Equal("someParameterName", labels.GenerateParameterName("someParameterName", string.Empty));
        Assert.Throws<InvalidOperationException>(() => labels.GenerateParameterName("someParameterName", "class"));
    }

    [Fact]
    public void Simple_strategy_operation_names()
    {
        var sut = new SimpleNamingStrategy(NameSource.Label);

        Assert.Equal("SimpleName", sut.GenerateOperationMethodName("someMethodName", "Simple Name"));
        Assert.Equal("SomeMethodName", sut.GenerateOperationMethodName("someMethodName", null));
        Assert.Throws<InvalidOperationException>(() => sut.GenerateOperationMethodName(string.Empty, null));
    }

    [Fact]
    public void Custom_strategies_can_be_plugged_in()
    {
        INamingStrategy strategy = new UpperCaseStrategy();

        Assert.Equal("PERSON", strategy.GenerateClassName("Person", null));
        Assert.Equal(NameSource.Name, strategy.NameSource);
    }

    private sealed class UpperCaseStrategy : NamingStrategyBase
    {
        public UpperCaseStrategy()
            : base(NameSource.Name)
        {
        }

        public override string GenerateClassName(string name, string? label) => name.ToUpperInvariant();
    }
}
