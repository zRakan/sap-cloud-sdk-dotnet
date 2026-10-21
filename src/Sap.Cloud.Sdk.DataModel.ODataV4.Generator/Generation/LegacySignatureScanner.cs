using System.Text.RegularExpressions;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Generation;

/// <summary>
/// Reads the signatures of an already generated file so that a regeneration does not break callers when the order of
/// the key or operation parameters in the EDMX changed.
/// </summary>
/// <remarks>
/// Port of <c>LegacyClassScanner</c>. Java parses the previous Java sources with QDox; here the previous C# source
/// is scanned with a regular expression, which is enough because the generator emits one declaration per line.
/// The result for a method is every parameter list the old file had (in the old order, smallest first), plus one
/// list with the old longest list followed by the parameters that are new.
/// </remarks>
internal sealed class LegacySignatureScanner
{
    private readonly IReadOnlyDictionary<string, string>? _files;

    private LegacySignatureScanner()
    {
    }

    /// <summary>Creates a scanner over the previous output.</summary>
    /// <param name="previousFiles">The text of the previously generated files by relative path (<c>/</c> separators).</param>
    public LegacySignatureScanner(IReadOnlyDictionary<string, string> previousFiles)
    {
        _files = previousFiles;
    }

    /// <summary>Reads the previously generated files of a service that the scanner may need.</summary>
    /// <param name="outputDirectory">The output directory.</param>
    /// <param name="relativeEntries">The files, and the folders with a trailing slash, relative to the output directory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The previous text by relative path.</returns>
    public static async Task<Dictionary<string, string>> LoadAsync(string outputDirectory, IEnumerable<string> relativeEntries, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in relativeEntries)
        {
            var path = Path.Combine(outputDirectory, entry.Replace('/', Path.DirectorySeparatorChar));
            IEnumerable<string> files;
            if (entry.EndsWith('/'))
            {
                files = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.cs") : [];
            }
            else
            {
                files = File.Exists(path) ? [path] : [];
            }

            foreach (var file in files)
            {
                try
                {
                    var relative = Path.GetRelativePath(outputDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                    result[relative] = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // an unreadable previous file is treated like a missing one
                }
            }
        }

        return result;
    }

    /// <summary>A scanner that returns the current parameters unchanged.</summary>
    public static LegacySignatureScanner Disabled { get; } = new();

    public IReadOnlyList<IReadOnlyList<T>> DetermineArguments<T>(
        string relativeFilePath,
        string methodName,
        IReadOnlyList<T> currentArguments,
        Func<T, string> nameOf)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(currentArguments);
        ArgumentNullException.ThrowIfNull(nameOf);
        IReadOnlyList<IReadOnlyList<T>> fallback = [currentArguments];
        if (_files is null || !_files.TryGetValue(relativeFilePath, out var text))
        {
            return fallback;
        }

        var legacySets = FindParameterNames(text, methodName);
        if (legacySets.Count == 0)
        {
            return fallback;
        }

        var lookup = currentArguments.ToDictionary(nameOf, StringComparer.Ordinal);
        var resulting = new List<List<T>>();
        foreach (var legacy in legacySets)
        {
            var set = new List<T>();
            foreach (var name in legacy)
            {
                if (!lookup.TryGetValue(name, out var argument))
                {
                    throw new ODataGeneratorWriteException(
                        $"Detected a breaking change: the parameter '{name}' of '{methodName}' in {relativeFilePath} does not exist anymore. Unable to find parameter from previous build.");
                }

                set.Add(argument);
            }

            if (!resulting.Any(r => r.SequenceEqual(set)))
            {
                resulting.Add(set);
            }
        }

        resulting = [.. resulting.OrderBy(s => s.Count)];
        var biggest = resulting.Aggregate((a, b) => b.Count > a.Count ? b : a);
        var additional = currentArguments.Where(a => !biggest.Contains(a)).ToList();
        if (additional.Count > 0)
        {
            resulting.Add([.. biggest, .. additional]);
        }

        return [.. resulting];
    }

    private static List<List<string>> FindParameterNames(string text, string methodName)
    {
        var result = new List<List<string>>();
        var pattern = new Regex(
            @"^[ \t]*(?:public[ \t]+)?(?:static[ \t]+)?[\w<>\[\]?.,]+(?:[ \t]+[\w<>\[\]?.,]+)*?[ \t]+" + Regex.Escape(methodName) + @"[ \t]*\((?<args>[^()]*)\)[ \t]*(?:;|=>|\r?\n)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(2));
        foreach (Match match in pattern.Matches(text))
        {
            var arguments = match.Groups["args"].Value;
            var names = new List<string>();
            foreach (var argument in SplitTopLevel(arguments))
            {
                var trimmed = argument.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var lastSpace = trimmed.LastIndexOfAny([' ', '\t']);
                var name = lastSpace < 0 ? trimmed : trimmed[(lastSpace + 1)..];
                names.Add(name.TrimStart('@'));
            }

            result.Add(names);
        }

        return result;
    }

    private static IEnumerable<string> SplitTopLevel(string arguments)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return arguments[start..i];
                    start = i + 1;
                    break;
                default:
                    break;
            }
        }

        yield return arguments[start..];
    }
}
