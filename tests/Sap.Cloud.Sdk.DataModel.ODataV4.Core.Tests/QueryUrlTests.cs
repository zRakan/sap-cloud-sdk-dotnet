using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Namespaces.Trippin;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample.Services;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests;

/// <summary>
/// URL generation of the read request builders. Ports the reference service tests of Java
/// (<c>ODataReferenceServiceTest</c>, <c>ODataReferenceServiceByKeyTest</c>, <c>EncodedQueryTest</c>, ...).
/// </summary>
public class QueryUrlTests
{
    private const string TestUserName = "testuser";
    private static readonly DefaultTrippinService Service = new();

    private static string FullQuery(string query) => ITrippinService.DefaultServicePath + "/People('" + TestUserName + "')?" + query;

    [Fact]
    public void GetAll_SelectAndExpand()
    {
        var request = Service.GetAllPeople()
            .Select(Person.Fields.FirstName, Person.Fields.LastName)
            .Select(Person.Fields.Trips)
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$select", "FirstName,LastName");
        Asserts.HasParameter(request.GetRelativeUri(), "$expand", "Trips");
        Assert.StartsWith(ITrippinService.DefaultServicePath + "/People?", request.GetRelativeUri().OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public void GetAll_MultipleExpandsWithNestedSelect()
    {
        var request = Service.GetAllPeople()
            .Select(Person.Fields.Trips.Select(Trip.Fields.Name))
            .Select(Person.Fields.BestFriend.Select(Person.Fields.LastName))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$expand", "Trips($select=Name),BestFriend($select=LastName)");
    }

    [Fact]
    public void GetAll_AllSelectionVariants()
    {
        var request = Service.GetAllPeople()
            .Select(Person.Fields.Gender, Person.Fields.UserName)
            .Select(Person.Fields.BestFriend, Person.Fields.Trips, Person.Fields.Friends.Top(2).Skip(1))
            .Filter(Person.Fields.Emails.Contains(new[] { "ASD" }))
            .ToRequest();

        var uri = request.GetRelativeUri();
        Asserts.HasParameter(uri, "$select", "Gender,UserName");
        Asserts.HasParameter(uri, "$expand", "BestFriend,Trips,Friends($top=2;$skip=1)");
        Asserts.HasParameter(uri, "$filter", "contains(Emails,['ASD'])");
    }

    [Fact]
    public void GetAll_FiltersWithSpecialCharacters()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.FirstName.Contains("' +&#\\")).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "contains(FirstName,''' +&#\\')");
    }

    [Fact]
    public void GetAll_NestedFiltersWithAllSpecialCharacters()
    {
        var request = Service.GetAllPeople()
            .Filter(Person.Fields.FirstName.Contains("% $&#?\"\\+'"))
            .Select(Person.Fields.BestFriend.Select(Person.Fields.Trips.Filter(Trip.Fields.Name.Contains("% $&#?\"\\+'"))))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$expand", "BestFriend($expand=Trips($filter=contains(Name,'% $&#?\"\\+''')))");
        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "contains(FirstName,'% $&#?\"\\+''')");
    }

    [Fact]
    public void GetAll_NestedFiltersWithSpecialCharacters()
    {
        var request = Service.GetAllPeople()
            .Filter(Person.Fields.FirstName.Contains("' +&#\\"))
            .Select(Person.Fields.Trips.Filter(Trip.Fields.Name.EqualTo("Trip in '&USA'#")))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$expand", "Trips($filter=(Name eq 'Trip in ''&USA''#'))");
        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "contains(FirstName,''' +&#\\')");
    }

    [Fact]
    public void GetAll_DuplicateSelectionsAreMerged()
    {
        var request = Service.GetAllPeople()
            .Select(Person.Fields.Gender, Person.Fields.UserName)
            .Select(Person.Fields.Gender, Person.Fields.UserName)
            .Select(Person.Fields.BestFriend, Person.Fields.Trips)
            .Select(Person.Fields.BestFriend, Person.Fields.BestFriend.Select(Person.Fields.Friends))
            .Select(Person.Fields.Friends, Person.Fields.Friends.Top(2).Skip(1))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$select", "Gender,UserName");
        Asserts.HasParameter(request.GetRelativeUri(), "$expand", "Trips,BestFriend($expand=Friends),Friends($top=2;$skip=1)");
    }

    [Fact]
    public void GetAll_OrderBy()
    {
        var request = Service.GetAllPeople().OrderBy(Person.Fields.FirstName.Asc(), Person.Fields.LastName.Desc()).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$orderby", "FirstName asc,LastName desc");
    }

    [Fact]
    public void GetAll_OrderByTheSameFieldTwiceKeepsTheLastDirection()
    {
        var request = Service.GetAllPeople()
            .OrderBy(Person.Fields.FirstName.Desc(), Person.Fields.LastName.Asc(), Person.Fields.FirstName.Desc())
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$orderby", "FirstName desc,LastName asc");
    }

    [Fact]
    public void GetAll_TopSkipAndPreferredPageSize()
    {
        var request = Service.GetAllPeople().Top(5).Skip(10).WithPreferredPageSize(3).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$top", "5");
        Asserts.HasParameter(request.GetRelativeUri(), "$skip", "10");
        Assert.Equal(["odata.maxpagesize=3"], request.GetHeaders()["Prefer"]);
    }

    [Fact]
    public void GetAll_SelectAllFields()
    {
        var request = Service.GetAllPeople().Select(Person.Fields.All).ToRequest();

        Assert.DoesNotContain("$select=Person", request.GetRelativeUri().OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public void GetByKey_SelectAndExpand()
    {
        var request = Service.GetPeopleByKey(TestUserName)
            .Select(Person.Fields.FirstName, Person.Fields.LastName)
            .Select(Person.Fields.Trips)
            .ToRequest();

        const string expected = "$select=FirstName,LastName&$expand=Trips";
        Assert.Equal(expected, request.QueryString);
        Assert.Equal(FullQuery(expected), request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void GetByKey_MultipleExpandsWithNestedSelect()
    {
        var request = Service.GetPeopleByKey(TestUserName)
            .Select(Person.Fields.Trips.Select(Trip.Fields.Name))
            .Select(Person.Fields.BestFriend.Select(Person.Fields.LastName))
            .ToRequest();

        const string expected = "$expand=Trips($select=Name),BestFriend($select=LastName)";
        Assert.Equal(expected, request.QueryString);
        Assert.Equal(FullQuery(expected), request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void GetByKey_AllSelectionVariants()
    {
        var request = Service.GetPeopleByKey(TestUserName)
            .Select(Person.Fields.Gender, Person.Fields.UserName)
            .Select(Person.Fields.Emails)
            .Select(Person.Fields.BestFriend, Person.Fields.Trips, Person.Fields.Friends.Top(2).Skip(1))
            .ToRequest();

        const string expected = "$select=Gender,UserName,Emails&$expand=BestFriend,Trips,Friends($top=2;$skip=1)";
        Assert.Equal(expected, request.QueryString);
        Assert.Equal(FullQuery(expected), request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void GetByKey_DuplicateSelectionsAreMerged()
    {
        var request = Service.GetPeopleByKey(TestUserName)
            .Select(Person.Fields.Gender, Person.Fields.UserName)
            .Select(Person.Fields.Gender, Person.Fields.UserName)
            .Select(Person.Fields.BestFriend, Person.Fields.Trips)
            .Select(Person.Fields.BestFriend, Person.Fields.BestFriend.Select(Person.Fields.Friends))
            .Select(Person.Fields.Friends, Person.Fields.Friends.Top(2).Skip(1))
            .ToRequest();

        const string expected = "$select=Gender,UserName&$expand=Trips,BestFriend($expand=Friends),Friends($top=2;$skip=1)";
        Assert.Equal(expected, request.QueryString);
    }

    [Fact]
    public void GetByKey_QuoteInKeyIsEscaped()
    {
        var request = Service.GetPeopleByKey("test'user").ToRequest();

        Assert.Equal(ITrippinService.DefaultServicePath + "/People('test''user')", request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void GetByKey_SpecialCharactersInKeyArePercentEncoded()
    {
        var request = Service.GetPeopleByKey("test/? #&user'%$").ToRequest();

        Assert.Equal(
            ITrippinService.DefaultServicePath + "/People('test%2F%3F%20%23&user''%25$')",
            request.GetRelativeUri().OriginalString);
    }

    [Fact]
    public void Filter_EncodesOperatorsAndSpaces()
    {
        var query = Service.GetAllPeople()
            .Filter(
                Person.Fields.UserName.Length().GreaterThanEqual(8).Not()
                    .And(Person.Fields.FirstName.Length().GreaterThanEqual(10))
                    .Or(Person.Fields.LastName.Length().LessThanEqual(7)))
            .ToRequest();

        Assert.Equal(
            "$filter=(((not%20(length(UserName)%20ge%208))%20and%20(length(FirstName)%20ge%2010))%20or%20(length(LastName)%20le%207))",
            query.GetRequestQuery());
    }

    [Fact]
    public void Filter_KeepsUnreservedCharacters()
    {
        var query = Service.GetAllPeople().Filter(Person.Fields.UserName.Contains("_*-:,/'().")).ToRequest();

        Assert.Equal("$filter=contains(UserName,'_*-:,/''().')", query.GetRequestQuery());
    }

    [Fact]
    public void Filter_EncodesSpecialCharacters()
    {
        var query = Service.GetAllPeople().Filter(Person.Fields.UserName.Contains("!@#$%^&=+|\\\"")).ToRequest();

        Assert.Equal("$filter=contains(UserName,'%21%40%23%24%25%5E%26%3D%2B%7C%5C%22')", query.GetRequestQuery());
    }

    [Fact]
    public void Filter_EncodesNonAsciiCharactersAsUtf8()
    {
        var query = Service.GetAllPeople()
            .Filter(
                Person.Fields.UserName.EqualTo("François")
                    .And(Person.Fields.LastName.Contains("Sørina").Or(Person.Fields.FirstName.Matches("维基百科"))))
            .ToRequest();

        Assert.Equal(
            "$filter=((UserName%20eq%20'Fran%C3%A7ois')%20and%20(contains(LastName,'S%C3%B8rina')%20or%20matchesPattern(FirstName,'%E7%BB%B4%E5%9F%BA%E7%99%BE%E7%A7%91')))",
            query.GetRequestQuery());
    }

    [Fact]
    public void Expand_EncodesNestedFiltersWithAllSpecialCharacters()
    {
        var request = Service.GetAllPeople()
            .Filter(Person.Fields.FirstName.Contains("% $&#?\"\\+'"))
            .Select(Person.Fields.BestFriend.Select(Person.Fields.Trips.Filter(Trip.Fields.Name.Contains("% $&#?\"\\+'"))))
            .ToRequest();

        Assert.Equal(
            "$expand=BestFriend($expand=Trips($filter=contains(Name,'%25%20%24%26%23%3F%22%5C%2B''')))&$filter=contains(FirstName,'%25%20%24%26%23%3F%22%5C%2B''')",
            request.GetRequestQuery());
    }

    [Fact]
    public void Search_EncodesQuotesAndBackslashes()
    {
        var request = Service.GetAllPeople().Search("Hash # Quoted \"string\" Escaped \\").ToRequest();

        Assert.Equal("$search=%22Hash%20%23%20Quoted%20%5C%22string%5C%22%20Escaped%20%5C%5C%22", request.GetRequestQuery());
    }

    [Fact]
    public void OrderBy_EncodesSpaces()
    {
        var request = Service.GetAllPeople().OrderBy(Person.Fields.FirstName.Asc(), Person.Fields.LastName.Desc()).ToRequest();

        Assert.Equal("$orderby=FirstName%20asc,LastName%20desc", request.GetRequestQuery());
    }

    [Fact]
    public void CustomQueryParameters_AreEncoded()
    {
        var request = Service.GetAllPeople()
            .WithQueryParameter("foo", "hash#tag")
            .WithQueryParameter("param", "% $&#?\"\\+'bar")
            .ToRequest();

        Assert.Equal("foo=hash%23tag&param=%25%20%24%26%23%3F%22%5C%2B'bar", request.GetRequestQuery());
    }

    [Fact]
    public void CustomQueryParameters_KeepUnreservedCharacters()
    {
        var request = Service.GetAllPeople().WithQueryParameter("foo", "_*-:,/'().").ToRequest();

        Assert.Equal("foo=_*-:,/'().", request.GetRequestQuery());
    }

    [Fact]
    public void Filter_Not()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.UserName.Length().GreaterThanEqual(8).Not()).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(not (length(UserName) ge 8))");
    }

    [Fact]
    public void Filter_And()
    {
        var request = Service.GetAllPeople()
            .Filter(Person.Fields.FirstName.Length().LessThanEqual(8).And(Person.Fields.LastName.Length().LessThanEqual(8)))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "((length(FirstName) le 8) and (length(LastName) le 8))");
    }

    [Fact]
    public void Filter_Or()
    {
        var request = Service.GetAllPeople()
            .Filter(Person.Fields.FirstName.Length().LessThanEqual(8).Or(Person.Fields.LastName.Length().LessThanEqual(8)))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "((length(FirstName) le 8) or (length(LastName) le 8))");
    }

    [Fact]
    public void Filter_SeveralFiltersAreCombinedWithAnd()
    {
        var request = Service.GetAllPeople()
            .Filter(Person.Fields.FirstName.EqualTo("a"))
            .Filter(Person.Fields.LastName.EqualTo("b"))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(FirstName eq 'a') and (LastName eq 'b')");
    }

    [Theory]
    [InlineData("eq", 5)]
    [InlineData("ne", 10)]
    [InlineData("lt", 2)]
    [InlineData("gt", 15)]
    [InlineData("le", 1)]
    [InlineData("ge", 20)]
    public void Filter_Comparisons(string op, int operand)
    {
        var length = Person.Fields.Trips.Length();
        var filter = op switch
        {
            "eq" => length.EqualTo(operand),
            "ne" => length.NotEqualTo(operand),
            "lt" => length.LessThan(operand),
            "gt" => length.GreaterThan(operand),
            "le" => length.LessThanEqual(operand),
            _ => length.GreaterThanEqual(operand),
        };

        var request = Service.GetAllPeople().Filter(filter).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", $"(length(Trips) {op} {operand})");
    }

    [Fact]
    public void Filter_InLiterals()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.UserName.In("scottketchum", "javieralfred")).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(UserName in ('scottketchum','javieralfred'))");
    }

    [Fact]
    public void Filter_InSimpleCollection()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.UserName.In(Person.Fields.Emails)).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(UserName in Emails)");
    }

    [Fact]
    public void Filter_InNavigationPropertyCollection()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.BestFriend.In(Person.Fields.Friends)).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(BestFriend in Friends)");
    }

    [Fact]
    public void Filter_InEnumCollection()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.FavoriteFeature.In(Person.Fields.Features)).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(FavoriteFeature in Features)");
    }

    [Fact]
    public void Filter_InEnumMembers()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.Gender.In(PersonGender.Male, PersonGender.Female)).ToRequest();

        Asserts.HasParameter(
            request.GetRelativeUri(),
            "$filter",
            "(Gender in (Trippin.PersonGender'Male',Trippin.PersonGender'Female'))");
    }

    [Fact]
    public void Filter_Any()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.Friends.Any(Person.Fields.FirstName.EqualTo("Angel"))).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "Friends/any(a:(a/FirstName eq 'Angel'))");
    }

    [Fact]
    public void Filter_All()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.Friends.All(Person.Fields.FirstName.EqualTo("Angel"))).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "Friends/all(a:(a/FirstName eq 'Angel'))");
    }

    [Fact]
    public void Filter_EnumMember()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.Friends.All(Person.Fields.Gender.NotEqualTo(PersonGender.Male))).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "Friends/all(a:(a/Gender ne Trippin.PersonGender'Male'))");
    }

    [Fact]
    public void Filter_EnumNull()
    {
        var request = Service.GetAllPeople().Filter(Person.Fields.Gender.EqualTo((PersonGender?)null)).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(Gender eq null)");
    }

    [Fact]
    public void Filter_ComplexProperty()
    {
        var has = Person.Fields.HomeAddress.Has("Debian");
        var request = Service.GetAllPeople().Filter(has, Person.Fields.HomeAddress.EqualToNull()).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(HomeAddress has 'Debian') and (HomeAddress eq null)");
    }

    [Fact]
    public void Filter_CustomUntypedExpression()
    {
        var untyped = FieldReference.Of("MiddleName").EqualTo("SomeName");

        var request = Service.GetAllPeople().Filter(untyped).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "(MiddleName eq 'SomeName')");
    }

    [Fact]
    public void Filter_CustomUntypedComplexExpression()
    {
        var untyped = FieldReference.Of("MiddleName").EqualTo("SomeName").And(FieldReference.Of("YearsOfExperience").GreaterThan(5));

        var request = Service.GetAllPeople().Filter(untyped).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "((MiddleName eq 'SomeName') and (YearsOfExperience gt 5))");
    }

    [Fact]
    public void Search_Literals()
    {
        Asserts.HasParameter(Service.GetAllPeople().Search("Portland").ToRequest().GetRelativeUri(), "$search", "\"Portland\"");
        Asserts.HasParameter(Service.GetAllPeople().Search("Hash #").ToRequest().GetRelativeUri(), "$search", "\"Hash #\"");
        Asserts.HasParameter(Service.GetAllPeople().Search("United States").ToRequest().GetRelativeUri(), "$search", "\"United States\"");
        Asserts.HasParameter(Service.GetAllPeople().Search("Quoted \"string\"").ToRequest().GetRelativeUri(), "$search", "\"Quoted \\\"string\\\"\"");
        Asserts.HasParameter(Service.GetAllPeople().Search("Escaped \\").ToRequest().GetRelativeUri(), "$search", "\"Escaped \\\\\"");
    }

    [Fact]
    public void Search_AmpersandIsRejected()
    {
        Assert.Throws<ArgumentException>(() => Service.GetAllPeople().Search("a&b"));
    }

    [Fact]
    public void Search_BooleanExpression()
    {
        var request = Service.GetAllPeople().Search(SearchExpression.Of("termA").Or("termB").And("termC")).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$search", "((\"termA\" OR \"termB\") AND \"termC\")");
    }

    [Fact]
    public void Search_Not()
    {
        var request = Service.GetAllPeople()
            .Search(SearchExpression.Of("termA").Or("termB").And(SearchExpression.Of("termC").Not()))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$search", "((\"termA\" OR \"termB\") AND NOT \"termC\")");
    }

    [Fact]
    public void Select_ComplexProperty()
    {
        var request = Service.GetAllPeople().Select(Person.Fields.FirstName, Person.Fields.LastName, Person.Fields.AddressInfo).ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$select", "FirstName,LastName,AddressInfo");
    }

    [Fact]
    public void Select_NestedComplexProperty()
    {
        var request = Service.GetAllPeople()
            .Select(Person.Fields.FirstName, Person.Fields.LastName, Person.Fields.AddressInfo.Select(Location.Fields.Address, Location.Fields.City))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$select", "FirstName,LastName,AddressInfo/Address,AddressInfo/City");
    }

    [Fact]
    public void Select_SimplePropertyInNestedComplexProperty()
    {
        var request = Service.GetAllPeople()
            .Select(
                Person.Fields.FirstName,
                Person.Fields.LastName,
                Person.Fields.AddressInfo.Select(Location.Fields.Address, Location.Fields.City.Select(City.Fields.Name)))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$select", "FirstName,LastName,AddressInfo/Address,AddressInfo/City/Name");
    }

    [Fact]
    public void Select_ComplexPropertyInNavigationProperty()
    {
        var request = Service.GetAllPeople()
            .Select(Person.Fields.BestFriend.Select(Person.Fields.FirstName, Person.Fields.AddressInfo.Select(Location.Fields.Address, Location.Fields.City)))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$expand", "BestFriend($select=FirstName,AddressInfo/Address,AddressInfo/City)");
    }

    [Fact]
    public void Filter_AnyInComplexPropertyCollection()
    {
        var request = Service.GetAllPeople()
            .Select(Person.Fields.FirstName, Person.Fields.LastName)
            .Filter(Person.Fields.AddressInfo.Any(Location.Fields.Address.StartsWith("Diagon Alley")))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$select", "FirstName,LastName");
        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "AddressInfo/any(a:startswith(a/Address,'Diagon Alley'))");
    }

    [Fact]
    public void Filter_AnyInAnyPropertyCollection()
    {
        var request = Service.GetAllPeople()
            .Filter(Person.Fields.Friends.Any(Person.Fields.AddressInfo.Any(Location.Fields.Address.StartsWith("Diagon Alley"))))
            .ToRequest();

        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "Friends/any(a:a/AddressInfo/any(b:startswith(b/Address,'Diagon Alley')))");
    }

    [Fact]
    public void Filter_AnyBesideAll()
    {
        var request = Service.GetAllPeople()
            .Filter(
                Person.Fields.Friends.Any(Person.Fields.FirstName.EqualTo("Adam"))
                    .Or(Person.Fields.Friends.All(Person.Fields.FirstName.EqualTo("Eve"))))
            .ToRequest();

        Asserts.HasParameter(
            request.GetRelativeUri(),
            "$filter",
            "(Friends/any(a:(a/FirstName eq 'Adam')) or Friends/all(a:(a/FirstName eq 'Eve')))");
    }

    [Fact]
    public void Filter_LambdaParameterLevelsForSameEntity()
    {
        var lvl3 = Person.Fields.UserName.EqualTo("c1").Or(Person.Fields.UserName.EqualTo("c2"));
        var lvl2 = Person.Fields.UserName.EqualTo("b1").And(Person.Fields.Friends.Any(lvl3)).And(Person.Fields.UserName.EqualTo("b2"));
        var lvl1 = Person.Fields.UserName.EqualTo("a1").Or(Person.Fields.Friends.All(lvl2)).Or(Person.Fields.UserName.EqualTo("a2"));
        var lvl0 = Person.Fields.UserName.EqualTo("1").And(Person.Fields.Friends.Any(lvl1)).Or(Person.Fields.UserName.EqualTo("2"));

        var request = Service.GetAllPeople().Filter(lvl0).ToRequest();

        Asserts.HasParameter(
            request.GetRelativeUri(),
            "$filter",
            "(((UserName eq '1') and Friends/any(a:(((a/UserName eq 'a1') or a/Friends/all(b:(((b/UserName eq 'b1') and b/Friends/any(c:((c/UserName eq 'c1') or (c/UserName eq 'c2')))) and (b/UserName eq 'b2')))) or (a/UserName eq 'a2')))) or (UserName eq '2'))");
    }

    [Fact]
    public void Filter_LambdaParameterLevelsForDifferentEntities()
    {
        var lvl3 = PlanItem.Fields.ConfirmationCode.EqualTo("c1").Or(PlanItem.Fields.ConfirmationCode.EqualTo("c2"));
        var lvl2 = Trip.Fields.Name.EqualTo("b1").And(Trip.Fields.PlanItems.Any(lvl3)).And(Trip.Fields.Name.EqualTo("b2"));
        var lvl1 = Person.Fields.UserName.EqualTo("a1").Or(Person.Fields.Trips.All(lvl2)).Or(Person.Fields.UserName.EqualTo("a2"));
        var lvl0 = Person.Fields.UserName.EqualTo("1").And(Person.Fields.Friends.Any(lvl1)).And(Person.Fields.UserName.EqualTo("2"));

        var request = Service.GetAllPeople().Filter(lvl0).ToRequest();

        Asserts.HasParameter(
            request.GetRelativeUri(),
            "$filter",
            "(((UserName eq '1') and Friends/any(a:(((a/UserName eq 'a1') or a/Trips/all(b:(((b/Name eq 'b1') and b/PlanItems/any(c:((c/ConfirmationCode eq 'c1') or (c/ConfirmationCode eq 'c2')))) and (b/Name eq 'b2')))) or (a/UserName eq 'a2')))) and (UserName eq '2'))");
    }

    [Fact]
    public void Count_WithFilterAndSearch()
    {
        var request = Service.CountPeople().Search("Foo").Filter(Person.Fields.FirstName.Contains("Bar")).ToRequest();

        Assert.Equal(ITrippinService.DefaultServicePath + "/People/$count", Asserts.Path(request.GetRelativeUri()));
        Asserts.HasParameter(request.GetRelativeUri(), "$filter", "contains(FirstName,'Bar')");
        Asserts.HasParameter(request.GetRelativeUri(), "$search", "\"Foo\"");
    }
}
