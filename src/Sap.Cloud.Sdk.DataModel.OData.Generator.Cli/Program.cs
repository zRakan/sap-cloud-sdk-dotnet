using Sap.Cloud.Sdk.DataModel.OData.Generator;
using Sap.Cloud.Sdk.DataModel.OData.Generator.Cli;
using Sap.Cloud.Sdk.DataModel.ODataV4.Generator;
using V2 = Sap.Cloud.Sdk.DataModel.OData.Generator;
using V4 = Sap.Cloud.Sdk.DataModel.ODataV4.Generator;

return await GeneratorCommand.RunAsync(args, Console.Out, Console.Error, CancellationToken.None).ConfigureAwait(false);

namespace Sap.Cloud.Sdk.DataModel.OData.Generator.Cli
{
    /// <summary>Runs the generator for a parsed command line.</summary>
    internal static class GeneratorCommand
    {
        public const int Success = 0;
        public const int GenerationFailed = 1;
        public const int UsageError = 2;

        public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
        {
            CommandLine commandLine;
            try
            {
                commandLine = CommandLine.Parse(args);
            }
            catch (ArgumentException e)
            {
                await error.WriteLineAsync(e.Message).ConfigureAwait(false);
                await error.WriteLineAsync(CommandLine.Usage).ConfigureAwait(false);
                return UsageError;
            }
            if (commandLine.Help)
            {
                await output.WriteLineAsync(CommandLine.Usage).ConfigureAwait(false);
                return Success;
            }

            return commandLine.Version == "v2"
                ? await RunV2Async(commandLine, output, error, cancellationToken).ConfigureAwait(false)
                : await RunV4Async(commandLine, output, error, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> RunV2Async(CommandLine c, TextWriter output, TextWriter error, CancellationToken cancellationToken)
        {
            var options = new ODataGeneratorOptions
            {
                OutputDirectory = c.Output!,
                ServiceNameMappingsFile = c.ServiceNameMappings,
                NameSource = c.NameSourceIsName ? V2.NameSource.Name : V2.NameSource.Label,
                DeleteTargetDirectory = c.DeleteTarget,
                OverwriteFiles = c.Overwrite,
                GeneratePojosOnly = c.PojosOnly,
                ServiceMethodsPerEntitySet = c.PerEntitySet,
                GenerateLinksToApiBusinessHub = c.LinksToBusinessHub,
                GenerateVersionReference = c.VersionReference,
                ExcludeFilePattern = c.ExcludePattern,
                DefaultBasePath = c.DefaultBasePath,
                FileHeader = c.FileHeader,
                IncludedEntitySets = c.EntitySets,
                IncludedFunctionImports = c.FunctionImports,
                DeprecationNotice = c.DeprecationNotice,
                FailOnWarning = c.FailOnWarning,
            };
            if (c.Namespace is not null)
            {
                options.RootNamespace = c.Namespace;
            }
            if (c.SimpleNaming)
            {
                options.NamingStrategy = new V2.SimpleNamingStrategy();
            }
            foreach (var input in c.Inputs)
            {
                options.InputPaths.Add(input);
            }

            try
            {
                var result = await ODataGenerator.GenerateAsync(options, cancellationToken).ConfigureAwait(false);
                foreach (var warning in result.Warnings)
                {
                    await error.WriteLineAsync("warning: " + warning).ConfigureAwait(false);
                }
                foreach (var service in result.Services)
                {
                    await output.WriteLineAsync($"{service.Status,-9} {service.Name} ({service.SourceFile}){(service.Error is null ? "" : ": " + service.Error)}").ConfigureAwait(false);
                }
                await output.WriteLineAsync($"{result.FilesWritten.Count} files written to {Path.GetFullPath(c.Output!)}.").ConfigureAwait(false);
                var failed = result.Services.Any(s => s.Status == ServiceGenerationStatus.Failed)
                    || (c.FailOnWarning && result.Warnings.Count > 0);
                return failed ? GenerationFailed : Success;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await error.WriteLineAsync("error: " + e.Message).ConfigureAwait(false);
                return GenerationFailed;
            }
        }

        private static async Task<int> RunV4Async(CommandLine c, TextWriter output, TextWriter error, CancellationToken cancellationToken)
        {
            var directories = c.Inputs.Where(Directory.Exists).ToList();
            if (directories.Count > 1)
            {
                await error.WriteLineAsync("error: OData v4 generation accepts at most one input directory; pass further inputs as files.").ConfigureAwait(false);
                return UsageError;
            }
            var options = new ODataV4GeneratorOptions
            {
                InputDirectory = directories.SingleOrDefault(),
                InputFiles = [.. c.Inputs.Where(i => !Directory.Exists(i))],
                OutputDirectory = c.Output!,
                RootNamespace = c.Namespace ?? ODataV4GeneratorOptions.DefaultRootNamespace,
                ServiceNameMappingsFile = c.ServiceNameMappings,
                NamingStrategy = c.SimpleNaming ? new V4.SimpleNamingStrategy() : null,
                NameSource = c.NameSourceIsName ? V4.NameSource.Name : V4.NameSource.Label,
                DeleteTargetDirectory = c.DeleteTarget,
                OverwriteFiles = c.Overwrite,
                GeneratePojosOnly = c.PojosOnly,
                ServiceMethodsPerEntitySet = c.PerEntitySet,
                IncludedEntitySets = c.EntitySets,
                IncludedFunctionImports = c.FunctionImports,
                IncludedActionImports = c.ActionImports,
                ExcludeFilePattern = c.ExcludePattern ?? string.Empty,
                DeprecationNotice = c.DeprecationNotice,
                DefaultBasePath = c.DefaultBasePath,
                GenerateLinksToApiBusinessHub = c.LinksToBusinessHub,
                GenerateVersionReference = c.VersionReference,
                FileHeader = c.FileHeader ?? string.Empty,
                FailOnWarning = c.FailOnWarning,
            };

            try
            {
                var result = await ODataV4Generator.GenerateAsync(options, cancellationToken).ConfigureAwait(false);
                foreach (var message in result.Messages)
                {
                    var writer = message.Level == GeneratorMessageLevel.Warning ? error : output;
                    await writer.WriteLineAsync($"{message.Level.ToString().ToLowerInvariant()}: {message.Text}").ConfigureAwait(false);
                }
                foreach (var service in result.Services)
                {
                    var status = service.Generated ? "Generated" : "Failed";
                    await output.WriteLineAsync($"{status,-9} {service.ServiceName} ({service.SourceFile}){(service.FailureReason is null ? "" : ": " + service.FailureReason)}").ConfigureAwait(false);
                }
                await output.WriteLineAsync($"{result.Files.Count} files written to {Path.GetFullPath(c.Output!)}.").ConfigureAwait(false);
                return result.FailureDueToWarningsNecessary || result.Services.Any(s => !s.Generated) ? GenerationFailed : Success;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await error.WriteLineAsync("error: " + e.Message).ConfigureAwait(false);
                return GenerationFailed;
            }
        }
    }
}
