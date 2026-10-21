namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Cli;

/// <summary>Parsed command line of <c>sap-odata-generator</c>.</summary>
internal sealed class CommandLine
{
    public const string Usage = """
        Usage: sap-odata-generator <v2|v4> --input <file-or-directory> [--input ...] --output <directory> [options]

        Generates typed C# clients for OData services from EDMX metadata (port of the SAP Cloud SDK OData generators).

        Options:
          -i, --input <path>                 EDMX file or directory (repeatable). Directories are searched recursively.
          -o, --output <dir>                 Output directory.
          -n, --namespace <name>             Root namespace of the generated code.
              --service-name-mappings <file> Service name mappings (.properties), created/updated as in the Java SDK.
              --name-source <label|name>     Derive names from sap:label/Common.Label (default) or from EDM names.
              --naming-strategy <default|simple>
              --default-base-path <path>     Service path to use when the metadata has none.
              --include-entity-sets <a,b>    Generate only these entity sets.
              --include-function-imports <a,b>
              --include-action-imports <a,b> (v4 only)
              --exclude-pattern <pattern>    Ant-style pattern of input files to skip.
              --deprecation-notice <text>    Notice for deprecated services.
              --file-header <text>           Header comment for every generated file.
              --pojos-only                   Generate entity/complex types only, no service classes.
              --per-entity-set               Generate service methods per entity set.
              --overwrite                    Overwrite existing files.
              --delete-target                Delete the output directory before generating.
              --links-to-business-hub        Add links to the SAP Business Accelerator Hub.
              --version-reference            Add the generator version to generated files.
              --fail-on-warning              Exit with an error if warnings occurred.
          -h, --help                         Show this help.
        """;

    public string Version { get; private set; } = "";

    public List<string> Inputs { get; } = [];

    public string? Output { get; private set; }

    public string? Namespace { get; private set; }

    public string? ServiceNameMappings { get; private set; }

    public bool NameSourceIsName { get; private set; }

    public bool SimpleNaming { get; private set; }

    public string? DefaultBasePath { get; private set; }

    public HashSet<string>? EntitySets { get; private set; }

    public HashSet<string>? FunctionImports { get; private set; }

    public HashSet<string>? ActionImports { get; private set; }

    public string? ExcludePattern { get; private set; }

    public string? DeprecationNotice { get; private set; }

    public string? FileHeader { get; private set; }

    public bool PojosOnly { get; private set; }

    public bool PerEntitySet { get; private set; }

    public bool Overwrite { get; private set; }

    public bool DeleteTarget { get; private set; }

    public bool LinksToBusinessHub { get; private set; }

    public bool VersionReference { get; private set; }

    public bool FailOnWarning { get; private set; }

    public bool Help { get; private set; }

    /// <summary>Parses the arguments.</summary>
    /// <exception cref="ArgumentException">The arguments are invalid; the message explains why.</exception>
    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        var result = new CommandLine();
        if (args.Count == 0 || args[0] is "-h" or "--help")
        {
            result.Help = true;
            return result;
        }

        result.Version = args[0].ToLowerInvariant() switch
        {
            "v2" or "2" => "v2",
            "v4" or "4" => "v4",
            _ => throw new ArgumentException($"Unknown OData version '{args[0]}'. Expected 'v2' or 'v4'."),
        };

        for (var i = 1; i < args.Count; i++)
        {
            var option = args[i];
            string Value()
            {
                if (i + 1 >= args.Count)
                {
                    throw new ArgumentException($"Option '{option}' requires a value.");
                }
                return args[++i];
            }

            static HashSet<string> List(string value) =>
                [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

            switch (option)
            {
                case "-i" or "--input": result.Inputs.Add(Value()); break;
                case "-o" or "--output": result.Output = Value(); break;
                case "-n" or "--namespace": result.Namespace = Value(); break;
                case "--service-name-mappings": result.ServiceNameMappings = Value(); break;
                case "--name-source":
                    result.NameSourceIsName = Value().ToLowerInvariant() switch
                    {
                        "label" => false,
                        "name" => true,
                        var other => throw new ArgumentException($"Unknown name source '{other}'. Expected 'label' or 'name'."),
                    };
                    break;
                case "--naming-strategy":
                    result.SimpleNaming = Value().ToLowerInvariant() switch
                    {
                        "default" => false,
                        "simple" => true,
                        var other => throw new ArgumentException($"Unknown naming strategy '{other}'. Expected 'default' or 'simple'."),
                    };
                    break;
                case "--default-base-path": result.DefaultBasePath = Value(); break;
                case "--include-entity-sets": result.EntitySets = List(Value()); break;
                case "--include-function-imports": result.FunctionImports = List(Value()); break;
                case "--include-action-imports": result.ActionImports = List(Value()); break;
                case "--exclude-pattern": result.ExcludePattern = Value(); break;
                case "--deprecation-notice": result.DeprecationNotice = Value(); break;
                case "--file-header": result.FileHeader = Value(); break;
                case "--pojos-only": result.PojosOnly = true; break;
                case "--per-entity-set": result.PerEntitySet = true; break;
                case "--overwrite": result.Overwrite = true; break;
                case "--delete-target": result.DeleteTarget = true; break;
                case "--links-to-business-hub": result.LinksToBusinessHub = true; break;
                case "--version-reference": result.VersionReference = true; break;
                case "--fail-on-warning": result.FailOnWarning = true; break;
                case "-h" or "--help": result.Help = true; break;
                default: throw new ArgumentException($"Unknown option '{option}'.");
            }
        }

        if (!result.Help)
        {
            if (result.Inputs.Count == 0)
            {
                throw new ArgumentException("At least one --input is required.");
            }
            if (result.Output is null)
            {
                throw new ArgumentException("--output is required.");
            }
            if (result.ActionImports is not null && result.Version == "v2")
            {
                throw new ArgumentException("--include-action-imports is only supported for OData v4.");
            }
        }
        return result;
    }
}
