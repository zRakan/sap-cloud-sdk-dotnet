namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>Ports of <c>S4HanaNamingStrategyTest</c> and <c>SimpleNamingStrategyTest</c>. The expected values are PascalCase instead of Java's camelCase.</summary>
public class NamingStrategyTests
{
    [Fact]
    public void DefaultNameSource_IsLabel()
    {
        Assert.Equal(NameSource.Label, new S4HanaNamingStrategy().NameSource);
        Assert.Equal(NameSource.Label, new SimpleNamingStrategy().NameSource);
        Assert.Equal(NameSource.Label, new ODataGeneratorOptions().NameSource);
        Assert.IsType<S4HanaNamingStrategy>(new ODataGeneratorOptions().NamingStrategy);
    }

    // ----- S/4HANA --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("A_AddressEmailAddressType", "AddressEmailAddress")]
    [InlineData("ComparisonResult", "ComparisonResult")]
    [InlineData("C_TRIALBALANCEResult", "TRIALBALANCEResult")]
    [InlineData("GLAccount", "GLAccount")]
    [InlineData("I_BusObjectsType", "BusObjects")]
    [InlineData("Package", "Package")]
    [InlineData("Class", "ClassEntity")]
    [InlineData("class", "ClassEntity")]
    [InlineData("Namespace", "NamespaceEntity")]
    public void S4_ClassNameFromName(string name, string expected) =>
        Assert.Equal(expected, new S4HanaNamingStrategy(NameSource.Name).GenerateClassName(name, null));

    [Fact]
    public void S4_ClassNameFromName_ThrowsIfNothingRemains()
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Name);
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateClassName("To_", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateClassName("Type", null));
    }

    [Fact]
    public void S4_ClassNameFromLabel()
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Label);
        Assert.Equal("SomeLabel", strategy.GenerateClassName("SomeEntityName", "Some label"));
        Assert.Equal("SomeEntityName", strategy.GenerateClassName("SomeEntityName", ""));
        Assert.Equal("SomeEntityName", strategy.GenerateClassName("SomeEntityName", null));
        Assert.Equal("SomeEntityName", strategy.GenerateClassName("SomeEntityName", "   "));
        Assert.Equal("CaseEntity", strategy.GenerateClassName("Package", "case"));
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
    public void S4_EntityPrefixes_AreRemovedFromNames(string name, string expected)
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Name);
        Assert.Equal(expected, strategy.GenerateClassName(name, null), ignoreCase: true);
        Assert.Equal(expected, strategy.GenerateOperationMethodName(name, null), ignoreCase: true);
        Assert.Equal(expected + "FluentHelper", strategy.GenerateFluentHelperClassName(name, null), ignoreCase: true);
    }

    [Theory]
    [InlineData("A_SomeEntity")]
    [InlineData("to_SomeEntity")]
    [InlineData("X_A_SomeEntity")]
    public void S4_EntityPrefixes_AreKeptInLabels(string label)
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Label);
        Assert.Equal(label, strategy.GenerateClassName("SomeEntityName", label), ignoreCase: true);
        Assert.Equal(label + "FluentHelper", strategy.GenerateFluentHelperClassName("SomeEntityName", label), ignoreCase: true);
    }

    [Theory]
    [InlineData("SomeEntityType", "SomeEntity")]
    [InlineData("SomeEntity_", "SomeEntity")]
    [InlineData("SomeEntity_Type", "SomeEntity")]
    [InlineData("SomeEntityType_", "SomeEntityType")]
    public void S4_ClassSuffixes_AreRemoved(string name, string expected)
    {
        Assert.Equal(expected, new S4HanaNamingStrategy(NameSource.Name).GenerateClassName(name, null), ignoreCase: true);
        Assert.Equal(expected, new S4HanaNamingStrategy(NameSource.Label).GenerateClassName(name, null), ignoreCase: true);
        Assert.Equal(expected + "FluentHelper", new S4HanaNamingStrategy(NameSource.Name).GenerateFluentHelperClassName(name, null), ignoreCase: true);
    }

    [Theory]
    [InlineData("ABCIndicator", "ABCIndicator")]
    [InlineData("APARToleranceGroup", "APARToleranceGroup")]
    [InlineData("AdditionalCurrency1", "AdditionalCurrency1")]
    [InlineData("AlternativeGLAccount_T", "AlternativeGLAccount_T")]
    [InlineData("Cancel_ac", "Cancel_ac")]
    [InlineData("Class", "Class")]
    [InlineData("SAP_SomeEntity", "SomeEntity")]
    [InlineData("to_SomeEntity", "SomeEntity")]
    [InlineData("To_SomeEntity", "SomeEntity")]
    [InlineData("YY1_SomeEntity", "SomeEntity")]
    [InlineData("A_SomeEntity", "A_SomeEntity")]
    [InlineData("A_YY1_SomeEntity", "A_YY1_SomeEntity")]
    [InlineData("lowerCase", "LowerCase")]
    public void S4_PropertyAndNavigationNames(string name, string expected)
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Name);
        Assert.Equal(expected, strategy.GeneratePropertyName(name, null));
        Assert.Equal(expected, strategy.GenerateNavigationPropertyName(name));
    }

    [Fact]
    public void S4_PropertyNameFromLabel()
    {
        Assert.Equal("SomeLabel", new S4HanaNamingStrategy(NameSource.Label).GeneratePropertyName("someEntityName", "Some label"));
        Assert.Equal("SomeEntityName", new S4HanaNamingStrategy(NameSource.Name).GeneratePropertyName("someEntityName", "Some label"));
    }

    [Fact]
    public void S4_NamesThatAreEmptyAfterPrefixRemoval_Throw()
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Name);
        Assert.Throws<InvalidOperationException>(() => strategy.GeneratePropertyName("SAP_", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateNavigationPropertyName("SAP_"));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateParameterName("SAP_", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateOperationMethodName("To_", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateFluentHelperClassName("To_", null));
    }

    [Theory]
    [InlineData("Release", "Release")]
    [InlineData("BOMComparison", "BOMComparison")]
    [InlineData("Class", "Class")]
    public void S4_OperationMethodNameAndFluentHelper(string name, string expected)
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Name);
        Assert.Equal(expected == "Class" ? "Class" : expected, strategy.GenerateOperationMethodName(name, null));
        Assert.Equal(expected + "FluentHelper", strategy.GenerateFluentHelperClassName(name, null));
    }

    [Fact]
    public void S4_OperationMethodAndFluentHelperFromLabel()
    {
        var strategy = new S4HanaNamingStrategy(NameSource.Label);
        Assert.Equal("SomeLabel", strategy.GenerateOperationMethodName("someEntityName", "Some label"));
        Assert.Equal("SomeLabelFluentHelper", strategy.GenerateFluentHelperClassName("someEntityName", "Some label"));
    }

    [Theory]
    [InlineData("Release", "release")]
    [InlineData("BOMComparison", "bOMComparison")]
    [InlineData("Class", "classParameter")]
    [InlineData("Event", "eventParameter")]
    [InlineData("to_Item", "item")]
    public void S4_ParameterNames_AreCamelCaseAndNeverKeywords(string name, string expected) =>
        Assert.Equal(expected, new S4HanaNamingStrategy(NameSource.Name).GenerateParameterName(name, null));

    [Fact]
    public void S4_ParameterNameFromLabel() =>
        Assert.Equal("someLabel", new S4HanaNamingStrategy(NameSource.Label).GenerateParameterName("someEntityName", "Some label"));

    // ----- Simple ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("SimpleName", "SimpleName")]
    [InlineData("_LeadingUnderscore", "_LeadingUnderscore")]
    [InlineData("Type", "Type")]
    [InlineData("class", "Class")]
    [InlineData("URLAddress", "UrlAddress")]
    [InlineData("C_TRIALBALANCEResult", "C_TRIALBALANCEResult")]
    [InlineData("A_Entity", "A_Entity")]
    public void Simple_NamesFromName(string name, string expected)
    {
        var strategy = new SimpleNamingStrategy(NameSource.Name);
        Assert.Equal(expected, strategy.GenerateClassName(name, null));
        Assert.Equal(expected, strategy.GeneratePropertyName(name, null));
        Assert.Equal(expected, strategy.GenerateNavigationPropertyName(name));
        Assert.Equal(expected, strategy.GenerateOperationMethodName(name, null));
        Assert.Equal(expected + "FluentHelper", strategy.GenerateFluentHelperClassName(name, null));
    }

    [Theory]
    [InlineData("SimpleName", "simpleName")]
    [InlineData("_LeadingUnderscore", "_LeadingUnderscore")]
    [InlineData("Type", "type")]
    [InlineData("URLAddress", "urlAddress")]
    [InlineData("C_TRIALBALANCEResult", "c_TRIALBALANCEResult")]
    public void Simple_ParameterNamesFromName(string name, string expected) =>
        Assert.Equal(expected, new SimpleNamingStrategy(NameSource.Name).GenerateParameterName(name, null));

    [Fact]
    public void Simple_NamesFromLabel()
    {
        var strategy = new SimpleNamingStrategy(NameSource.Label);
        const string entity = "SomeEntityName";
        Assert.Equal("SimpleName", strategy.GenerateClassName(entity, "SimpleName"));
        Assert.Equal("SimpleName", strategy.GenerateClassName(entity, "Simple Name"));
        Assert.Equal(entity, strategy.GenerateClassName(entity, ""));
        Assert.Equal(entity, strategy.GenerateClassName(entity, null));
        Assert.Equal("SimpleName", strategy.GeneratePropertyName("someFieldName", "Simple Name"));
        Assert.Equal("simpleName", strategy.GenerateParameterName("someParameterName", "Simple Name"));
        Assert.Equal("someParameterName", strategy.GenerateParameterName("someParameterName", null));
    }

    [Fact]
    public void Simple_EmptyNames_Throw()
    {
        var strategy = new SimpleNamingStrategy(NameSource.Name);
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateClassName("", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GeneratePropertyName("", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateNavigationPropertyName(""));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateOperationMethodName("", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateParameterName("", null));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateFluentHelperClassName("", null));
        Assert.Throws<InvalidOperationException>(() => new SimpleNamingStrategy(NameSource.Label).GenerateClassName("", ""));
        Assert.Throws<InvalidOperationException>(() => strategy.GenerateClassName("123", null));
    }

    [Fact]
    public void Simple_KeywordParameterNames_Throw() =>
        Assert.Throws<InvalidOperationException>(() => new SimpleNamingStrategy(NameSource.Name).GenerateParameterName("class", null));

    [Fact]
    public void Strategies_RecognizeCSharpKeywords()
    {
        var strategy = new SimpleNamingStrategy();
        Assert.True(strategy.IsReservedKeyword("class"));
        Assert.True(strategy.IsReservedKeyword("event"));
        Assert.False(strategy.IsReservedKeyword("Class"));
        Assert.False(strategy.IsReservedKeyword("record"));
    }
}
