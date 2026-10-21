using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Details;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Edm;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Generation;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

/// <summary>
/// Generates C# code for <c>Sap.Cloud.Sdk.DataModel.ODataV4.Core</c> from OData v4 EDMX files.
/// </summary>
/// <remarks>
/// Port of the OData v4 generator of the Java SDK (<c>DataModelGenerator</c>). For every EDMX file (the file name
/// without extension is the service name) the generator produces, in the output directory:
/// <list type="bullet">
/// <item><description><c>Namespaces/&lt;Service&gt;/&lt;Type&gt;.cs</c>: one class per entity type, complex type and enumeration that is
/// reachable from the entity sets and operations;</description></item>
/// <item><description><c>Services/I&lt;Class&gt;Service.cs</c> and <c>Services/Default&lt;Class&gt;Service.cs</c>: the service interface and
/// its implementation.</description></item>
/// </list>
/// The generated code needs implicit usings (or <see cref="ODataV4GeneratorOptions.EmitSystemUsings"/>).
/// Constructs that are not generated: singletons, stream properties, geography/geometry properties, operations with
/// enumeration results or unsupported parameter types, and operations bound to anything but a generated entity type.
/// Every skipped construct is reported as a message.
/// </remarks>
public static class ODataV4Generator
{
    private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Reads the EDMX files and generates the code.</summary>
    /// <param name="options">The configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated files, a summary per service and the messages.</returns>
    /// <exception cref="ODataGeneratorReadException">The input directory or a given input file does not exist.</exception>
    /// <exception cref="ODataGeneratorWriteException">Existing files would be overwritten, or an existing signature cannot be kept.</exception>
    public static async Task<ODataV4GeneratorResult> GenerateAsync(ODataV4GeneratorOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new ArgumentException("The output directory is required.", nameof(options));
        }

        if (!IsValidNamespace(options.RootNamespace))
        {
            throw new ArgumentException($"'{options.RootNamespace}' is not a valid namespace.", nameof(options));
        }

        var run = new Run(options);
        await run.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (!options.DryRun)
        {
            await run.WriteAsync(cancellationToken).ConfigureAwait(false);
        }

        return run.ToResult();
    }

    private static bool IsValidNamespace(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Split('.').All(IsValidIdentifier);

    private static bool IsValidIdentifier(string value) =>
        value.Length > 0 && NamingUtils.IsIdentifierStart(value[0]) && value.All(NamingUtils.IsIdentifierPart) && !NamingUtils.IsCSharpKeyword(value);

    private static bool MatchesAnt(string pattern, string name)
    {
        var regex = "^" + Regex.Escape(pattern.Trim()).Replace(@"\*\*", ".*", StringComparison.Ordinal).Replace(@"\*", "[^/]*", StringComparison.Ordinal).Replace(@"\?", "[^/]", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(name, regex, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    private sealed class LoadedService
    {
        public required string Path { get; init; }

        public required string Name { get; init; }

        public required EdmModel Model { get; init; }

        public required ServiceDetails Details { get; init; }

        public required IReadOnlyDictionary<string, SortedSet<ApiFunction>> Allowed { get; init; }

        public required string Title { get; init; }

        public required string ClassName { get; init; }

        public required string NamespaceSegment { get; init; }
    }

    private sealed class Run
    {
        private readonly ODataV4GeneratorOptions _options;
        private readonly INamingStrategy _naming;
        private readonly List<GeneratorMessage> _messages = [];
        private readonly List<GeneratedFile> _files = [];
        private readonly List<GeneratedServiceSummary> _summaries = [];
        private readonly string _outputDirectory;
        private readonly string? _versionReference;

        public Run(ODataV4GeneratorOptions options)
        {
            _options = options;
            _naming = options.NamingStrategy ?? new DefaultNamingStrategy();
            _naming.NameSource = options.NameSource;
            _outputDirectory = Path.GetFullPath(options.OutputDirectory);
            _versionReference = options.GenerateVersionReference
                ? typeof(ODataV4Generator).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"
                : null;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var inputs = DiscoverInputs();
            if (_options.DeleteTargetDirectory && !_options.DryRun && Directory.Exists(_outputDirectory))
            {
                CleanDirectory(_outputDirectory);
            }

            if (inputs.Count == 0)
            {
                Warn("No OData service definitions found in the input - exiting.", null);
                return;
            }

            var mappings = _options.ServiceNameMappingsFile is { } mappingFile
                ? await ServiceNameMappings.LoadAsync(mappingFile, cancellationToken).ConfigureAwait(false)
                : ServiceNameMappings.CreateInMemory();
            var loaded = await LoadServicesAsync(inputs, mappings, cancellationToken).ConfigureAwait(false);

            foreach (var service in loaded)
            {
                mappings.PutString(service.Name + ServiceNameMappings.ClassSuffix, service.ClassName, service.Title);
                mappings.PutString(service.Name + ServiceNameMappings.PackageSuffix, service.NamespaceSegment);
            }

            if (_options.ServiceNameMappingsFile is not null && !_options.DryRun)
            {
                try
                {
                    await mappings.SaveAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (IOException e)
                {
                    throw new ODataGeneratorWriteException("Could not write the service name mappings file " + _options.ServiceNameMappingsFile, e);
                }
            }

            Info($"Processing {loaded.Count} OData service{(loaded.Count == 1 ? string.Empty : "s")}...", null);
            foreach (var service in loaded)
            {
                await GenerateAsync(service, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task WriteAsync(CancellationToken cancellationToken)
        {
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in _files)
            {
                if (!seen.TryAdd(file.RelativePath, file.RelativePath))
                {
                    throw new ODataGeneratorException(
                        $"More than one service generates the file {file.RelativePath}. Use a service name mappings file to give the services different class names.");
                }

                if (!_options.OverwriteFiles && File.Exists(file.FullPath))
                {
                    throw new ODataGeneratorWriteException("Cannot override existing file: " + file.FullPath);
                }
            }

            try
            {
                Directory.CreateDirectory(_outputDirectory);
                foreach (var file in _files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath)!);
                    await File.WriteAllTextAsync(file.FullPath, file.Content, s_utf8, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new ODataGeneratorWriteException("Could not write the generated files to " + _outputDirectory, e);
            }
        }

        public ODataV4GeneratorResult ToResult() =>
            new(
                _files,
                _summaries,
                _messages,
                _options.FailOnWarning && _messages.Any(m => m.Level == GeneratorMessageLevel.Warning));

        private static void CleanDirectory(string directory)
        {
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, recursive: true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new ODataGeneratorWriteException("Could not clean the output directory " + directory, e);
            }
        }

        private List<string> DiscoverInputs()
        {
            var result = new List<string>();
            if (_options.InputDirectory is { } directory)
            {
                if (!Directory.Exists(directory))
                {
                    throw new ODataGeneratorReadException("The given input directory does not exist: " + Path.GetFullPath(directory));
                }

                result.AddRange(
                    Directory
                        .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".edmx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        .Select(Path.GetFullPath)
                        .Order(StringComparer.Ordinal));
            }

            foreach (var file in _options.InputFiles)
            {
                if (!File.Exists(file))
                {
                    throw new ODataGeneratorReadException("The given input file does not exist: " + Path.GetFullPath(file));
                }

                var full = Path.GetFullPath(file);
                if (!result.Contains(full, StringComparer.Ordinal))
                {
                    result.Add(full);
                }
            }

            if (_options.InputDirectory is null && _options.InputFiles.Count == 0)
            {
                throw new ArgumentException("Either an input directory or input files are required.", nameof(_options));
            }

            return result;
        }

        private async Task<List<LoadedService>> LoadServicesAsync(List<string> inputs, ServiceNameMappings mappings, CancellationToken cancellationToken)
        {
            var loaded = new List<LoadedService>();
            foreach (var path in inputs)
            {
                var fileName = Path.GetFileName(path);
                var serviceName = Path.GetFileNameWithoutExtension(path);

                if (_options.ExcludeFilePattern.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(p => MatchesAnt(p, fileName)))
                {
                    Info($"Skipping EDMX file due to exclusion rule: {path}", path);
                    _summaries.Add(Failed(serviceName, path, "Excluded by the exclusion pattern."));
                    continue;
                }

                XDocument document;
                try
                {
                    document = await CsdlReader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
                }
                catch (ODataGeneratorReadException e)
                {
                    Warn($"Skipping EDMX file that could not be read: {e.Message} {e.InnerException?.Message}".TrimEnd(), path);
                    _summaries.Add(Failed(serviceName, path, e.Message));
                    continue;
                }

                if (!CsdlReader.IsODataV4(document))
                {
                    Info($"Skipping EDMX file due to incompatible OData version: {path}", path);
                    _summaries.Add(Failed(serviceName, path, "Not an OData v4 EDMX file."));
                    continue;
                }

                Info($"Loading OData service {serviceName}", path);
                try
                {
                    var swaggerPath = FindSwaggerFile(path, serviceName);
                    var swagger = swaggerPath is null ? null : await File.ReadAllBytesAsync(swaggerPath, cancellationToken).ConfigureAwait(false);
                    var model = CsdlReader.Read(document);
                    var details = ServiceDetailsResolver.Create(path, document, swagger, _options.DefaultBasePath);
                    var allowed = AllowedFunctionsResolver.Find(model, swagger, text => Warn(text, path));
                    var title = !string.IsNullOrEmpty(details.Info?.Title) ? details.Info.Title : NamingUtils.ApiNameToServiceTitle(serviceName);
                    var className = mappings.GetString(serviceName + ServiceNameMappings.ClassSuffix)
                        ?? NamingUtils.ServiceNameToBaseClassName(title);
                    var segment = mappings.GetString(serviceName + ServiceNameMappings.PackageSuffix) ?? className;
                    if (!IsValidIdentifier(className))
                    {
                        throw new ODataGeneratorReadException(
                            $"'{className}' is not a valid class name for the service '{serviceName}'. Add '{serviceName}{ServiceNameMappings.ClassSuffix}' to the service name mappings.");
                    }

                    if (!IsValidNamespace(segment))
                    {
                        throw new ODataGeneratorReadException(
                            $"'{segment}' is not a valid namespace for the service '{serviceName}'. Add '{serviceName}{ServiceNameMappings.PackageSuffix}' to the service name mappings.");
                    }

                    foreach (var note in model.Notes)
                    {
                        Info(note, path);
                    }

                    Info($"  Title: {title}", path);
                    Info($"  Raw URL: {details.ServiceUrl}", path);
                    Info($"  C# namespace: {segment}", path);
                    Info($"  C# class name: {className}", path);
                    loaded.Add(new LoadedService
                    {
                        Path = path,
                        Name = serviceName,
                        Model = model,
                        Details = details,
                        Allowed = allowed,
                        Title = title,
                        ClassName = className,
                        NamespaceSegment = segment,
                    });
                }
                catch (ODataGeneratorReadException e)
                {
                    Warn($"Error in file {fileName}; unable to generate VDM classes. The file will be skipped and generation continues for the next file. {e.Message}", path);
                    _summaries.Add(Failed(serviceName, path, e.Message));
                }
            }

            return loaded;
        }

        private static string? FindSwaggerFile(string path, string serviceName)
        {
            var directory = Path.GetDirectoryName(path)!;
            return Directory
                .EnumerateFiles(directory)
                .Order(StringComparer.Ordinal)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), serviceName + ".json", StringComparison.OrdinalIgnoreCase));
        }

        private async Task GenerateAsync(LoadedService loaded, CancellationToken cancellationToken)
        {
            var path = loaded.Path;
            var details = loaded.Details;
            var deprecated = _options.DeprecationNotice is not null || details.IsDeprecated;
            var service = new GenService
            {
                Name = loaded.Name,
                Title = loaded.Title,
                ClassName = loaded.ClassName,
                NamespaceSegment = loaded.NamespaceSegment,
                RootNamespace = _options.RootNamespace,
                Details = details,
                SourceFile = path,
                IsDeprecated = deprecated,
                DeprecationMessage = deprecated ? DeprecationMessage(details) : null,
                Description = details.Info?.Description ?? loaded.Model.Container?.Annotations.Description,
            };

            var legacy = LegacySignatureScanner.Disabled;
            if (_options.KeepExistingSignatures)
            {
                var previous = await LegacySignatureScanner
                    .LoadAsync(_outputDirectory, [$"Services/{service.InterfaceName}.cs", service.TypesFolder + "/"], cancellationToken)
                    .ConfigureAwait(false);
                if (previous.Count > 0)
                {
                    legacy = new LegacySignatureScanner(previous);
                }
            }

            var settings = new ProcessorSettings(
                _options.GeneratePojosOnly,
                _options.ServiceMethodsPerEntitySet,
                _options.IncludedEntitySets,
                _options.IncludedFunctionImports,
                _options.IncludedActionImports);
            var processor = new ServiceProcessor(
                loaded.Model,
                service,
                _naming,
                settings,
                loaded.Allowed,
                legacy,
                text => Warn(text, path),
                text => Info(text, path));

            try
            {
                processor.Process();
            }
            catch (Exception e) when (e is ODataGeneratorReadException or InvalidOperationException)
            {
                Warn($"Error in file {Path.GetFileName(path)}; unable to generate all VDM classes. {e.Message}", path);
                _summaries.Add(Failed(loaded.Name, path, e.Message));
                return;
            }

            var emitter = new SourceEmitter(new EmitSettings(
                _options.GeneratePojosOnly,
                _options.FileHeader,
                _versionReference,
                _options.EmitGeneratedCodeAttribute,
                _options.EmitSystemUsings,
                _options.GenerateLinksToApiBusinessHub));

            var typeFolder = service.TypesFolder;
            var generated = new List<(string Name, string Content)>();
            generated.AddRange(service.Enums.Select(e => (e.CsName, emitter.Enum(service, e))));
            generated.AddRange(service.Complexes.Select(c => (c.CsName, emitter.Complex(service, c))));
            generated.AddRange(service.Entities.Select(e => (e.CsName, emitter.Entity(service, e))));
            foreach (var (name, content) in generated.OrderBy(g => g.Name, StringComparer.Ordinal))
            {
                AddFile($"{typeFolder}/{name}.cs", content);
            }

            if (!_options.GeneratePojosOnly)
            {
                AddFile($"Services/{service.InterfaceName}.cs", emitter.ServiceInterface(service));
                AddFile($"Services/{service.ImplementationName}.cs", emitter.ServiceImplementation(service));
            }

            _summaries.Add(new GeneratedServiceSummary(
                loaded.Name,
                path,
                loaded.Title,
                loaded.ClassName,
                $"{_options.RootNamespace}.Namespaces.{service.NamespaceSegment}",
                details.ServiceUrl,
                service.Entities.Count,
                service.Complexes.Count,
                service.Enums.Count,
                service.OperationCount,
                deprecated,
                Generated: !_options.GeneratePojosOnly || generated.Count > 0,
                FailureReason: null));
        }

        private string DeprecationMessage(ServiceDetails details)
        {
            var state = details.StateInfo;
            var release = state?.DeprecationRelease is { Length: > 0 } r ? " as of release " + r : string.Empty;
            var date = state?.DeprecationDate is { Length: > 0 } d ? " (" + d + ")" : string.Empty;
            var notice = string.IsNullOrEmpty(_options.DeprecationNotice) ? "Please use the odata generator to generate the VDM. " : _options.DeprecationNotice;
            var successor = state?.SuccessorApi is { Length: > 0 } s
                ? $" Please use the successor API ({s}) instead."
                : " " + notice;
            return "The service and all its related classes are deprecated" + release + date + "." + successor;
        }

        private void AddFile(string relativePath, string content) =>
            _files.Add(new GeneratedFile(relativePath, Path.GetFullPath(Path.Combine(_outputDirectory, relativePath)), content));

        private static GeneratedServiceSummary Failed(string name, string path, string reason) =>
            new(name, path, null, null, null, null, 0, 0, 0, 0, false, false, reason);

        private void Info(string text, string? source) =>
            _messages.Add(new GeneratorMessage(GeneratorMessageLevel.Info, text, source));

        private void Warn(string text, string? source) =>
            _messages.Add(new GeneratorMessage(GeneratorMessageLevel.Warning, text, source));
    }
}
