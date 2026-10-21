using System.Text;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// The service name mappings: a properties file with entries such as <c>SDK_Grocery_Store.className = SdkGroceryStore</c> that
/// override the names derived from the service title.
/// </summary>
/// <remarks>
/// Port of <c>ServiceNameMappings</c>. Same file format; the keys are <c>{service}.className</c> and
/// <c>{service}.namespaceName</c> (Java: <c>.packageName</c>, a lower-case package name, which is not used for namespaces).
/// </remarks>
internal sealed class ServiceNameMappings
{
    internal const string ClassNameSuffix = ".className";
    internal const string NamespaceNameSuffix = ".namespaceName";

    private readonly string? _file;
    private readonly Dictionary<string, (string Value, string Comment)> _mappings = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    private ServiceNameMappings(string? file)
    {
        _file = file;
    }

    /// <summary>Loads the file if it exists; <see langword="null"/> gives mappings that are not persisted.</summary>
    internal static ServiceNameMappings Load(string? file)
    {
        var mappings = new ServiceNameMappings(file);
        if (file is not null && File.Exists(file))
        {
            mappings.Populate(file);
        }
        return mappings;
    }

    internal string? GetString(string key) => _mappings.TryGetValue(key, out var entry) ? entry.Value : null;

    internal void PutString(string key, string value, string comment = "")
    {
        if (!_mappings.ContainsKey(key))
        {
            _order.Add(key);
        }
        _mappings[key] = (value, comment);
    }

    /// <summary>Writes the file, if there is one. Returns whether the content changed.</summary>
    internal bool Save()
    {
        if (_file is null)
        {
            return false;
        }
        var text = new StringBuilder();
        foreach (var key in _order)
        {
            var (value, comment) = _mappings[key];
            if (comment.Trim().Length > 0)
            {
                text.Append('\n').Append("# ").Append(comment).Append('\n');
            }
            text.Append(key).Append(" = ").Append(value).Append('\n');
        }
        var content = text.ToString();
        if (File.Exists(_file) && File.ReadAllText(_file, Encoding.UTF8).ReplaceLineEndings("\n") == content)
        {
            return false;
        }
        var directory = Path.GetDirectoryName(Path.GetFullPath(_file));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(_file, content, new UTF8Encoding(false));
        return true;
    }

    private void Populate(string file)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(file, Encoding.UTF8);
        }
        catch (IOException e)
        {
            throw new ODataGeneratorReadException("Invalid mapping file: " + file, e);
        }

        var comment = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                comment.Add(line[1..].Trim());
            }
            else if (line.Contains('=', StringComparison.Ordinal))
            {
                var parts = line.Split('=', 2);
                PutString(parts[0].Trim(), parts[1].Trim(), string.Join(' ', comment));
                comment.Clear();
            }
        }
    }
}
