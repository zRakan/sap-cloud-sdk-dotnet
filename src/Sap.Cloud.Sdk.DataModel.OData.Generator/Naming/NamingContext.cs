using System.Globalization;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>How a <see cref="NamingContext"/> compares names.</summary>
internal enum NameEquality
{
    CaseSensitive,
    CaseInsensitive,
}

/// <summary>
/// Keeps track of the identifiers in one scope (a namespace, a class or a parameter list) and renames duplicates:
/// the second occurrence of <c>Name</c> becomes <c>Name_2</c>, the third <c>Name_3</c>.
/// </summary>
/// <remarks>
/// Port of <c>NamingContext</c> with <c>NameFormattingStrategy</c> folded into <see cref="NameEquality"/>. Unlike Java, a generated
/// replacement is registered as well, so that a later genuine <c>Name_2</c> does not clash with it.
/// </remarks>
internal sealed class NamingContext
{
    private readonly Dictionary<string, int> _occurrences;

    internal NamingContext(NameEquality equality = NameEquality.CaseSensitive)
    {
        _occurrences = new Dictionary<string, int>(equality == NameEquality.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Registers <paramref name="proposedName"/> and returns it, or a renamed variant if it is taken.</summary>
    internal string EnsureUniqueName(string proposedName)
    {
        if (!_occurrences.TryGetValue(proposedName, out var count))
        {
            _occurrences[proposedName] = 1;
            return proposedName;
        }

        string uniqueName;
        do
        {
            count++;
            uniqueName = string.Create(CultureInfo.InvariantCulture, $"{proposedName}_{count}");
        }
        while (_occurrences.ContainsKey(uniqueName));
        _occurrences[proposedName] = count;
        _occurrences[uniqueName] = 1;
        return uniqueName;
    }

    /// <summary>Whether the name is taken.</summary>
    internal bool AlreadyUses(string name) => _occurrences.ContainsKey(name);

    /// <summary>Marks names as taken without renaming anything.</summary>
    internal void Reserve(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            _occurrences.TryAdd(name, 1);
        }
    }
}
