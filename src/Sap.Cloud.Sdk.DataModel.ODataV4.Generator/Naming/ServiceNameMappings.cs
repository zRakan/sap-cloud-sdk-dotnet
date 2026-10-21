using System.Text;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>
/// The service name mappings: a properties file that fixes the class and namespace names generated for a service.
/// </summary>
/// <remarks>
/// Port of <c>ServiceNameMappings</c>. The file has the same format as in Java:
/// <c>&lt;serviceName&gt;.className = Name</c> and <c>&lt;serviceName&gt;.packageName = Name</c>, where the service name
/// is the file name of the EDMX without extension; <c>#</c> lines are comments. The value of <c>packageName</c> is used
/// as the (last) segment of the namespace of the entity classes, for example <c>Trippin</c>. Lines are written with
/// <c>\n</c>.
/// </remarks>
public sealed class ServiceNameMappings
{
    private const string DelimiterShort = "=";
    private const string DelimiterLong = " = ";

    private readonly string _file;
    private readonly Dictionary<string, (string Value, string Comment)> _mappings = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    private ServiceNameMappings(string file)
    {
        _file = file;
    }

    /// <summary>The key suffix of the class name mapping.</summary>
    internal const string ClassSuffix = ".className";

    /// <summary>The key suffix of the namespace mapping.</summary>
    internal const string PackageSuffix = ".packageName";

    /// <summary>Loads the mappings; a missing file yields empty mappings.</summary>
    /// <param name="file">The path of the properties file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mappings.</returns>
    public static async Task<ServiceNameMappings> LoadAsync(string file, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        var mappings = new ServiceNameMappings(file);
        if (File.Exists(file))
        {
            string[] lines;
            try
            {
                lines = await File.ReadAllLinesAsync(file, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException e)
            {
                throw new ArgumentException("Invalid mapping file: " + file, e);
            }

            mappings.Populate(lines);
        }

        return mappings;
    }

    /// <summary>Creates mappings that are not backed by a file.</summary>
    /// <returns>The empty mappings.</returns>
    public static ServiceNameMappings CreateInMemory() => new(string.Empty);

    /// <summary>Gets the value of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? GetString(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _mappings.TryGetValue(key, out var entry) ? entry.Value : null;
    }

    /// <summary>Adds or replaces a mapping.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="comments">Optional comments, written above the mapping.</param>
    public void PutString(string key, string value, params string[] comments)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(comments);
        if (!_mappings.ContainsKey(key))
        {
            _order.Add(key);
        }

        _mappings[key] = (value, string.Join(' ', comments));
    }

    /// <summary>Renders the mappings in the file format.</summary>
    /// <returns>The text.</returns>
    public string Render()
    {
        var text = new StringBuilder();
        foreach (var key in _order)
        {
            var (value, comment) = _mappings[key];
            if (!string.IsNullOrWhiteSpace(comment))
            {
                text.Append('\n').Append("# ").Append(comment).Append('\n');
            }

            text.Append(key).Append(DelimiterLong).Append(value).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Writes the mappings back to the file they were loaded from.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_file.Length == 0)
        {
            throw new InvalidOperationException("These mappings are not backed by a file.");
        }

        await File.WriteAllTextAsync(_file, Render(), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }

    private void Populate(string[] lines)
    {
        var comment = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                comment.Add(line[1..].Trim());
            }
            else if (line.Contains(DelimiterShort, StringComparison.Ordinal))
            {
                var index = line.IndexOf(DelimiterShort, StringComparison.Ordinal);
                PutString(line[..index].Trim(), line[(index + 1)..].Trim(), [.. comment]);
                comment.Clear();
            }
        }
    }
}
