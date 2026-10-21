using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sap.Cloud.Sdk.DataModel.OData.Helper;
using Sap.Cloud.Sdk.DataModel.OData.Generator.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Tests;

/// <summary>
/// The generator keeps lists of names that generated members and types may not take. These tests keep the lists in sync with the
/// runtime library and with the code the generator emits.
/// </summary>
public class ReservedNamesTests
{
    private const BindingFlags Visible = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Theory]
    [InlineData(typeof(VdmObject))]
    [InlineData(typeof(VdmEntity))]
    [InlineData(typeof(VdmEntity<>))]
    [InlineData(typeof(VdmMediaEntity<>))]
    [InlineData(typeof(VdmComplex<>))]
    public void EveryMemberVisibleToGeneratedTypes_IsReserved(Type baseType)
    {
        var missing = new List<string>();
        for (var type = baseType; type is not null && type != typeof(object); type = type.BaseType)
        {
            foreach (var member in type.GetMembers(Visible))
            {
                var isVisible = member switch
                {
                    MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
                    PropertyInfo property => (property.GetMethod?.IsPublic ?? false) || (property.GetMethod?.IsFamily ?? false) || (property.GetMethod?.IsFamilyOrAssembly ?? false),
                    FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
                    Type nested => nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamORAssem,
                    _ => false,
                };
                if (isVisible && member is not ConstructorInfo && !member.Name.Contains('<', StringComparison.Ordinal) && !CSharp.ReservedMemberNames.Contains(member.Name)
                    && !(member is MethodBase { IsSpecialName: true }))
                {
                    missing.Add($"{type.Name}.{member.Name}");
                }
            }
        }
        Assert.True(missing.Count == 0, "Not reserved: " + string.Join(", ", missing.Distinct()));
    }

    [Fact]
    public void ReservedMemberNames_IncludeObjectMembers()
    {
        foreach (var member in typeof(object).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (member is MethodInfo { IsPrivate: false, IsAssembly: false, IsSpecialName: false })
            {
                Assert.Contains(member.Name, CSharp.ReservedMemberNames);
            }
        }
    }

    [Theory]
    [InlineData("oDataGeneratorIntegrationTest/testService/input/test_service.edmx")]
    [InlineData("oDataGeneratorIntegrationTest/groceryStore/input/SDK_Grocery_Store.edmx")]
    public async Task EveryExternalTypeUsedByGeneratedCode_IsAReservedTypeName(string input)
    {
        var (_, output) = await Harness.GenerateAsync(
            Path.Combine(Harness.JavaData, input),
            options => options.NameSource = NameSource.Name,
            Path.Combine(Path.GetDirectoryName(Path.Combine(Harness.JavaData, input))!, "serviceNameMappings.properties"));
        var compilation = Harness.CreateCompilation(output);
        var generated = compilation.Assembly;
        var unreserved = new SortedSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var identifier in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(identifier).Symbol;
                // an attribute usage binds to the constructor
                var type = symbol as INamedTypeSymbol ?? (identifier.Parent is AttributeSyntax ? (symbol as IMethodSymbol)?.ContainingType : null);
                if (type is not null && !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, generated))
                {
                    seen.Add(type.Name);
                    var name = type.Name.EndsWith("Attribute", StringComparison.Ordinal) ? type.Name[..^"Attribute".Length] : type.Name;
                    if (!CSharp.ReservedTypeNames.Contains(name) && !CSharp.ReservedTypeNames.Contains(type.Name))
                    {
                        unreserved.Add(type.Name);
                    }
                }
            }
        }
        Assert.Contains("Task", seen);
        Assert.Contains("VdmEntity", seen);
        Assert.Contains("ODataKeyAttribute", seen);
        Assert.True(unreserved.Count == 0, "Types used by the generated code that an entity could shadow: " + string.Join(", ", unreserved));
    }
}
