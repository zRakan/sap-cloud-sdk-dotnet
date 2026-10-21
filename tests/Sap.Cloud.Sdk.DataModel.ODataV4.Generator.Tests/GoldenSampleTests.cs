using System.Runtime.CompilerServices;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Generator.Tests;

/// <summary>
/// The sample code of the runtime library tests is the generator output for <c>Trippin.edmx</c>. These tests catch
/// drift in either direction. To regenerate the sample after an intended change, run the tests with the environment
/// variable <c>UPDATE_GOLDEN=1</c>.
/// </summary>
public sealed class GoldenSampleTests
{
    private const string SampleRootNamespace = "Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Sample";

    public static ODataV4GeneratorOptions SampleOptions(string outputDirectory) => new()
    {
        InputFiles = [Path.Combine(AppContext.BaseDirectory, "Sample", "Trippin.edmx")],
        OutputDirectory = outputDirectory,
        RootNamespace = SampleRootNamespace,
        DefaultBasePath = "/TripPinRESTierService",
        ServiceMethodsPerEntitySet = true,
        DryRun = true,
    };

    [Fact]
    public async Task Generated_code_is_byte_equal_to_the_checked_in_sample()
    {
        var result = await ODataV4Generator.GenerateAsync(SampleOptions(Path.Combine(Path.GetTempPath(), "unused")));

        var update = Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1";
        var sourceSample = SourceSampleDirectory();
        var sample = Path.Combine(AppContext.BaseDirectory, "Sample");

        var expected = new HashSet<string>(StringComparer.Ordinal);
        var differences = new List<string>();
        foreach (var file in result.Files)
        {
            expected.Add(file.RelativePath);
            if (update)
            {
                var target = Path.Combine(sourceSample, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, file.Content, new System.Text.UTF8Encoding(false));
                continue;
            }

            var checkedIn = Path.Combine(sample, file.RelativePath);
            if (!File.Exists(checkedIn))
            {
                differences.Add($"{file.RelativePath}: missing in the sample");
            }
            else if (!string.Equals(await File.ReadAllTextAsync(checkedIn), file.Content, StringComparison.Ordinal))
            {
                differences.Add($"{file.RelativePath}: differs from the generator output");
            }
        }

        foreach (var existing in Directory.EnumerateFiles(sample, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sample, existing).Replace('\\', '/');
            if (!expected.Contains(relative))
            {
                differences.Add($"{relative}: not produced by the generator");
                if (update)
                {
                    File.Delete(Path.Combine(sourceSample, relative));
                }
            }
        }

        if (!update)
        {
            Assert.True(differences.Count == 0, "The sample is out of date (UPDATE_GOLDEN=1 regenerates it):\n" + string.Join("\n", differences));
        }
    }

    [Fact]
    public async Task Sample_generation_reports_the_skipped_geography_property_only()
    {
        var result = await ODataV4Generator.GenerateAsync(SampleOptions(Path.Combine(Path.GetTempPath(), "unused")));

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("GeographyPoint", warning.Text, StringComparison.Ordinal);
        var summary = Assert.Single(result.Services);
        Assert.True(summary.Generated);
        Assert.Equal("Trippin", summary.ClassName);
        Assert.Equal("/TripPinRESTierServiceTrippin", summary.ServicePath);
        Assert.Equal(5, summary.EntityCount);
        Assert.Equal(3, summary.ComplexTypeCount);
        Assert.Equal(2, summary.EnumTypeCount);
    }

    [Fact]
    public async Task Generation_is_deterministic()
    {
        var first = await ODataV4Generator.GenerateAsync(SampleOptions(Path.Combine(Path.GetTempPath(), "unused")));
        var second = await ODataV4Generator.GenerateAsync(SampleOptions(Path.Combine(Path.GetTempPath(), "unused")));

        Assert.Equal(first.Files.Select(f => f.RelativePath), second.Files.Select(f => f.RelativePath));
        Assert.Equal(first.Files.Select(f => f.Content), second.Files.Select(f => f.Content));
        Assert.All(first.Files, f => Assert.DoesNotContain('\r', f.Content));
    }

    private static string SourceSampleDirectory([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests", "Sample"));
}
