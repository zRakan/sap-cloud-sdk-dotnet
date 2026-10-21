using System.Globalization;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>Whether two names that differ only in case are considered equal.</summary>
internal enum NameEquality
{
    /// <summary><c>abc</c> and <c>ABC</c> are different names.</summary>
    CaseSensitive,

    /// <summary><c>abc</c> and <c>ABC</c> clash; the second one is renamed.</summary>
    CaseInsensitive,
}

/// <summary>
/// Hands out unique names inside one scope (the members of a class, the types of a namespace, the parameters of a
/// method). A clash is resolved by appending <c>_2</c>, <c>_3</c>, ...
/// </summary>
/// <remarks>Port of <c>NamingContext</c>; the reflection based loading of getters and setters is replaced by <see cref="Reserve(IEnumerable{string})"/>.</remarks>
internal sealed class NamingContext
{
    private readonly Dictionary<string, int> _occurrences;
    private readonly Action<string>? _onRenamed;

    public NamingContext(NameEquality equality = NameEquality.CaseSensitive, Action<string>? onRenamed = null)
    {
        _occurrences = new Dictionary<string, int>(
            equality == NameEquality.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        _onRenamed = onRenamed;
    }

    /// <summary>Ensures that the name does not clash in this scope and records it.</summary>
    /// <param name="proposedName">The name you want to use.</param>
    /// <returns>The name you should use.</returns>
    public string EnsureUniqueName(string proposedName)
    {
        if (!_occurrences.TryGetValue(proposedName, out var count))
        {
            _occurrences[proposedName] = 1;
            return proposedName;
        }

        string candidate;
        do
        {
            count++;
            candidate = string.Create(CultureInfo.InvariantCulture, $"{proposedName}_{count}");
        }
        while (_occurrences.ContainsKey(candidate));

        _occurrences[proposedName] = count;
        _occurrences[candidate] = 1;
        _onRenamed?.Invoke($"Found more than one occurrence of the identifier {proposedName}. The new identifier has been renamed to {candidate}.");
        return candidate;
    }

    /// <summary>Checks whether a name is already in use, without recording it.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> if the name is taken.</returns>
    public bool AlreadyUses(string name) => _occurrences.ContainsKey(name);

    /// <summary>Marks names as taken without renaming anything.</summary>
    /// <param name="names">The names.</param>
    public void Reserve(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            _occurrences.TryAdd(name, 1);
        }
    }

    /// <summary>Marks a name as taken without renaming anything.</summary>
    /// <param name="name">The name.</param>
    public void Reserve(string name) => _occurrences.TryAdd(name, 1);
}
