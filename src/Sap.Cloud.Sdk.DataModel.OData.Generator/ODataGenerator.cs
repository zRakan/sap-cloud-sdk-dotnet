using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Sap.Cloud.Sdk.DataModel.OData.Generator;

/// <summary>
/// Generates typed C# classes for <c>Sap.Cloud.Sdk.DataModel.OData.Core</c> from OData V2 service metadata (EDMX).
/// </summary>
/// <remarks>
/// Port of Java's <c>DataModelGenerator</c> / <c>ODataToVdmGenerator</c>. See <see cref="ODataGeneratorOptions"/> for the
/// configuration and <see cref="ODataGeneratorResult"/> for the outcome. The output is deterministic: files are generated in
/// a stable order, with <c>\n</c> newlines and without a byte order mark.
/// </remarks>
public static partial class ODataGenerator
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    /// <summary>Reads the configured EDMX files and writes the generated code.</summary>
    /// <param name="options">The configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The files written, the warnings and one summary per input file.</returns>
    /// <exception cref="ODataGeneratorReadException">An input path does not exist.</exception>
    /// <exception cref="ODataGeneratorWriteException">A file cannot be written or may not be overwritten.</exception>
    /// <exception cref="ODataGeneratorException">The generation produced warnings and <see cref="ODataGeneratorOptions.FailOnWarning"/> is set.</exception>
    public static async Task<ODataGeneratorResult> GenerateAsync(ODataGeneratorOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory, nameof(options));
        ArgumentNullException.ThrowIfNull(options.NamingStrategy, nameof(options));

        var warnings = new List<string>();
        var summaries = new List<ServiceGenerationSummary>();
        var inputFiles = FindInputFiles(options);
        options.NamingStrategy.NameSource = options.NameSource;

        // 1. read all services
        var mappings = ServiceNameMappings.Load(options.ServiceNameMappingsFile);
        var parsed = new List<ParsedService>();
        var attempted = 0;
        foreach (var file in inputFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(file);
            if (IsExcluded(options.ExcludeFilePattern, Path.GetFileName(file)))
            {
                summaries.Add(Skipped(name, file, "The file matches the exclusion pattern."));
                continue;
            }
            if (!IsQualifiedForODataV2(file))
            {
                summaries.Add(Skipped(name, file, "The file is not an OData V2 service definition (incompatible OData version or not well-formed)."));
                continue;
            }

            attempted++;
            try
            {
                parsed.Add(ReadService(options, mappings, name, file, warnings));
            }
            catch (Exception e) when (e is ODataGeneratorReadException or InvalidOperationException)
            {
                warnings.Add($"Error in file {Path.GetFileName(file)}; unable to generate classes. The file will be skipped and generation continues for the next file. {e.Message}");
                summaries.Add(Failed(name, file, e.Message));
            }
        }

        if (attempted == 0)
        {
            warnings.Add("No OData service definitions found in the input paths - exiting.");
        }

        // 2. store the (possibly extended) service name mappings
        foreach (var service in parsed)
        {
            mappings.PutString(service.Name + ServiceNameMappings.ClassNameSuffix, service.ClassName, service.Title);
            mappings.PutString(service.Name + ServiceNameMappings.NamespaceNameSuffix, service.NamespaceName);
        }
        mappings.Save();

        // 3. generate the code of every service
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var usedClassNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedNamespaces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in parsed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var serviceFiles = new SortedDictionary<string, string>(StringComparer.Ordinal);
            try
            {
                if (usedClassNames.TryGetValue(service.ClassName, out var otherClass))
                {
                    throw new ODataGeneratorReadException($"The class name '{service.ClassName}' is already used by the service '{otherClass}'. Use the service name mappings to give the services different names.");
                }
                if (usedNamespaces.TryGetValue(service.NamespaceName, out var otherNamespace))
                {
                    throw new ODataGeneratorReadException($"The namespace '{service.NamespaceName}' is already used by the service '{otherNamespace}'. Use the service name mappings to give the services different namespaces.");
                }
                var genService = Generate(options, service, warnings, serviceFiles);
                usedClassNames[service.ClassName] = service.Name;
                usedNamespaces[service.NamespaceName] = service.Name;
                foreach (var (path, content) in serviceFiles)
                {
                    if (!files.TryAdd(path, content))
                    {
                        throw new ODataGeneratorWriteException($"The file '{path}' of the service '{service.Name}' is also generated for another service.");
                    }
                }
                summaries.Add(new ServiceGenerationSummary
                {
                    Name = service.Name,
                    SourceFile = service.File,
                    Status = ServiceGenerationStatus.Generated,
                    Title = service.Title,
                    ClassName = service.ClassName,
                    Namespace = genService.EntityNamespace,
                    ServiceUrl = service.Details.ServiceUrl,
                    IsDeprecated = genService.IsDeprecated,
                    EntityCount = genService.Entities.Count,
                    ComplexTypeCount = genService.ComplexTypes.Count,
                    FunctionImportCount = genService.FunctionImports.Count,
                    Files = [.. serviceFiles.Keys.Select(p => Path.GetFullPath(Path.Combine(options.OutputDirectory, p)))],
                });
            }
            catch (Exception e) when (e is ODataGeneratorReadException or InvalidOperationException)
            {
                warnings.Add($"Error in file {Path.GetFileName(service.File)}; unable to generate all classes. {e.Message}");
                summaries.Add(Failed(service.Name, service.File, e.Message));
            }
        }

        // 4. write
        var written = await WriteFilesAsync(options, inputFiles, files, cancellationToken).ConfigureAwait(false);

        var result = new ODataGeneratorResult
        {
            FilesWritten = written,
            Warnings = warnings,
            Services = summaries,
        };
        if (options.FailOnWarning && warnings.Count > 0)
        {
            throw new ODataGeneratorException(
                "The generation produced warnings and the option to fail on warnings is set:\n" + string.Join('\n', warnings.Select(w => " - " + w)));
        }
        return result;
    }

    private sealed class ParsedService
    {
        internal required string Name { get; init; }

        internal required string File { get; init; }

        internal required string Title { get; init; }

        internal required string ClassName { get; init; }

        internal required string NamespaceName { get; init; }

        internal required EdmModel Model { get; init; }

        internal required ServiceDetails Details { get; init; }

        internal required IReadOnlyDictionary<string, IReadOnlyCollection<ApiFunction>> AllowedFunctions { get; init; }
    }

    private static ParsedService ReadService(
        ODataGeneratorOptions options,
        ServiceNameMappings mappings,
        string name,
        string file,
        List<string> warnings)
    {
        var model = EdmxReader.ReadFile(file);
        var swagger = FindSwaggerFile(file, name);
        var details = new ServiceDetailsResolver(options.DefaultBasePath).CreateServiceDetails(file, model, swagger);
        var allowed = AllowedFunctionsResolver.FindAllowedFunctions(model, swagger, warnings);

        var title = string.IsNullOrEmpty(details.Title) ? NamingUtils.ApiNameToServiceTitle(name) : details.Title;
        var className = mappings.GetString(name + ServiceNameMappings.ClassNameSuffix) ?? NamingUtils.ServiceNameToBaseClassName(title);
        var namespaceName = mappings.GetString(name + ServiceNameMappings.NamespaceNameSuffix) ?? className;
        ValidateIdentifier(className, "class name", name);
        foreach (var segment in namespaceName.Split('.'))
        {
            ValidateIdentifier(segment, "namespace name", name);
        }

        return new ParsedService
        {
            Name = name,
            File = file,
            Title = title,
            ClassName = className,
            NamespaceName = namespaceName,
            Model = model,
            Details = details,
            AllowedFunctions = allowed,
        };
    }

    private static void ValidateIdentifier(string value, string what, string service)
    {
        if (!Identifier().IsMatch(value) || NamingUtils.IsKeyword(value))
        {
            throw new ODataGeneratorReadException(
                $"The {what} '{value}' of the service '{service}' is not a valid C# identifier. Set a valid name in the service name mappings file.");
        }
    }

    private static GenService Generate(ODataGeneratorOptions options, ParsedService parsed, List<string> warnings, SortedDictionary<string, string> files)
    {
        var deprecated = options.DeprecationNotice is not null || parsed.Details.IsDeprecated;
        var service = new GenService
        {
            Name = parsed.Name,
            Title = parsed.Title,
            ClassName = parsed.ClassName,
            NamespaceName = parsed.NamespaceName,
            Details = parsed.Details,
            RootNamespace = options.RootNamespace ?? "",
            EdmNamespace = parsed.Model.FirstNamespace ?? "",
            IsDeprecated = deprecated,
            DeprecationNotice = options.DeprecationNotice,
            LinkToApiBusinessHub = options.GenerateLinksToApiBusinessHub,
            PojosOnly = options.GeneratePojosOnly,
        };

        new ServiceModelBuilder(parsed.Model, service, options.NamingStrategy, options, parsed.AllowedFunctions, warnings).Build();

        var context = new EmitContext(service, options);
        var entityDirectory = "Namespaces/" + service.NamespaceName.Replace('.', '/');
        foreach (var entity in service.Entities)
        {
            files[$"{entityDirectory}/{entity.FileName}.cs"] = EntityEmitter.EmitEntity(context, entity);
            if (!options.GeneratePojosOnly)
            {
                files[$"{entityDirectory}/{entity.HelpersFileName}.cs"] = FluentHelperEmitter.EmitEntityHelpers(context, entity);
            }
        }
        foreach (var complex in service.ComplexTypes)
        {
            files[$"{entityDirectory}/{complex.FileName}.cs"] = EntityEmitter.EmitComplexType(context, complex);
        }

        if (!options.GeneratePojosOnly)
        {
            foreach (var function in service.FunctionImports)
            {
                files[$"{entityDirectory}/{function.FileName}.cs"] = FluentHelperEmitter.EmitFunctionImport(context, function);
            }
            files[$"Services/{service.InterfaceName}.cs"] = ServiceEmitter.EmitInterface(context);
            files[$"Services/{service.ImplementationName}.cs"] = ServiceEmitter.EmitImplementation(context);
            files[$"{entityDirectory}/Batch/{service.BatchInterfaceName}.cs"] = ServiceEmitter.EmitBatchInterfaces(context);
            files[$"{entityDirectory}/Batch/{service.BatchImplementationName}.cs"] = ServiceEmitter.EmitBatchImplementation(context);
            files[$"{entityDirectory}/Batch/{service.ChangeSetImplementationName}.cs"] = ServiceEmitter.EmitBatchChangeSetImplementation(context);
        }
        return service;
    }

    // ----- Input ----------------------------------------------------------------------------------------------------

    private static List<string> FindInputFiles(ODataGeneratorOptions options)
    {
        var files = new List<string>();
        foreach (var path in options.InputPaths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(Directory
                    .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".edmx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFullPath)
                    .Order(StringComparer.Ordinal));
            }
            else if (File.Exists(path))
            {
                files.Add(Path.GetFullPath(path));
            }
            else
            {
                throw new ODataGeneratorReadException("The given input path does not exist: " + Path.GetFullPath(path));
            }
        }
        return [.. files.Distinct(StringComparer.Ordinal)];
    }

    private static string? FindSwaggerFile(string edmxFile, string serviceName)
    {
        var directory = Path.GetDirectoryName(edmxFile)!;
        return Directory
            .EnumerateFiles(directory, "*.json")
            .Where(f => string.Equals(Path.GetFileName(f), serviceName + ".json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Whether the file has a <c>DataServices</c> element with a <c>DataServiceVersion</c> of 1.x or 2.x.</summary>
    private static bool IsQualifiedForODataV2(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && string.Equals(reader.LocalName, "DataServices", StringComparison.OrdinalIgnoreCase))
                {
                    if (!reader.HasAttributes)
                    {
                        continue;
                    }
                    for (var i = 0; i < reader.AttributeCount; i++)
                    {
                        reader.MoveToAttribute(i);
                        if (string.Equals(reader.LocalName, "DataServiceVersion", StringComparison.OrdinalIgnoreCase))
                        {
                            var major = reader.Value.Split('.')[0];
                            return major is "1" or "2";
                        }
                    }
                    reader.MoveToElement();
                }
            }
        }
        catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
        return false;
    }

    private static bool IsExcluded(string? pattern, string fileName)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }
        return pattern.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).Any(p => AntMatch(p, fileName));
    }

    /// <summary>Ant-style matching: <c>?</c> matches one character, <c>*</c> any characters except <c>/</c>, <c>**</c> anything.</summary>
    internal static bool AntMatch(string pattern, string text)
    {
        var regex = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            switch (pattern[i])
            {
                case '*' when i + 1 < pattern.Length && pattern[i + 1] == '*':
                    regex.Append(".*");
                    i++;
                    break;
                case '*':
                    regex.Append("[^/]*");
                    break;
                case '?':
                    regex.Append("[^/]");
                    break;
                default:
                    regex.Append(Regex.Escape(pattern[i].ToString()));
                    break;
            }
        }
        regex.Append('$');
        return Regex.IsMatch(text, regex.ToString(), RegexOptions.CultureInvariant);
    }

    private static ServiceGenerationSummary Skipped(string name, string file, string reason) => new()
    {
        Name = name,
        SourceFile = file,
        Status = ServiceGenerationStatus.Skipped,
        Error = reason,
    };

    private static ServiceGenerationSummary Failed(string name, string file, string reason) => new()
    {
        Name = name,
        SourceFile = file,
        Status = ServiceGenerationStatus.Failed,
        Error = reason,
    };

    // ----- Output ---------------------------------------------------------------------------------------------------

    private static async Task<IReadOnlyList<string>> WriteFilesAsync(
        ODataGeneratorOptions options,
        List<string> inputFiles,
        SortedDictionary<string, string> files,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        if (options.DeleteTargetDirectory && Directory.Exists(outputDirectory))
        {
            if (inputFiles.Any(f => f.StartsWith(outputDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            {
                throw new ODataGeneratorWriteException($"Refusing to delete the output directory '{outputDirectory}': it contains input files.");
            }
            CleanDirectory(outputDirectory);
        }

        var targets = files.Select(f => (Path: Path.GetFullPath(Path.Combine(outputDirectory, f.Key.Replace('/', Path.DirectorySeparatorChar))), Content: f.Value)).ToList();
        if (!options.OverwriteFiles)
        {
            var existing = targets.Where(t => File.Exists(t.Path)).Select(t => t.Path).ToList();
            if (existing.Count > 0)
            {
                throw new ODataGeneratorWriteException(
                    $"The file '{existing[0]}' already exists{(existing.Count > 1 ? $" (and {existing.Count - 1} more)" : "")}. Enable overwriting files or delete the output directory.");
            }
        }

        var utf8 = new UTF8Encoding(false);
        var written = new List<string>();
        foreach (var (path, content) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, content, utf8, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new ODataGeneratorWriteException("The file cannot be written: " + path, e);
            }
            written.Add(path);
        }
        return written;
    }

    private static void CleanDirectory(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                Directory.Delete(subdirectory, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ODataGeneratorWriteException("The output directory cannot be cleaned: " + directory, e);
        }
    }
}
