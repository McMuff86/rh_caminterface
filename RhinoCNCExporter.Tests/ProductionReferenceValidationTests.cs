using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Sprint 5 production validation:
/// - fixtures are derived from real CAD+T DWGs already committed under tests/references/cadt
/// - generated plate programs are compared against production XCS references after
///   normalizing only non-semantic differences (branding comments, generated op names, simple arithmetic)
/// </summary>
public class ProductionReferenceValidationTests
{
    private static readonly string ReferencesPath = Path.GetFullPath(
        Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "tests", "references"));

    [Fact]
    public void Putzschrank_SockelMont_DwgFixture_MatchesProductionReference()
    {
        var fixture = ProductionValidationFixtures.CreatePutzschrankSockelMont();

        Assert.True(File.Exists(fixture.SourceDwgPath), $"Missing DWG fixture source: {fixture.SourceDwgPath}");
        Assert.True(File.Exists(fixture.ReferenceXcsPath), $"Missing XCS reference: {fixture.ReferenceXcsPath}");

        var reference = File.ReadAllText(fixture.ReferenceXcsPath);
        Assert.DoesNotContain("CreateBladeCut", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateSectioningMillingStrategy", reference, StringComparison.Ordinal);

        var generated = GenerateProgram(fixture.Plate);

        AssertNormalizedProgramEquals(reference, generated);
    }

    [Fact]
    public void Legrabox_Fertigauszug_DwgFixture_MatchesProductionReference()
    {
        var fixture = ProductionValidationFixtures.CreateLegraboxFertigauszug();

        Assert.True(File.Exists(fixture.SourceDwgPath), $"Missing DWG fixture source: {fixture.SourceDwgPath}");
        Assert.True(File.Exists(fixture.ReferenceXcsPath), $"Missing XCS reference: {fixture.ReferenceXcsPath}");

        var reference = File.ReadAllText(fixture.ReferenceXcsPath);
        Assert.DoesNotContain("CreateBladeCut", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateSectioningMillingStrategy", reference, StringComparison.Ordinal);

        var generated = GenerateProgram(fixture.Plate);

        AssertNormalizedProgramEquals(reference, generated);
    }

    [Fact]
    public void Legrabox_SchubladenDoppel_Reference_DocumentsCurrentBladeCutGap()
    {
        var referencePath = Path.Combine(ReferencesPath, "NEW_Schubladen_Doppel_1.xcs");
        Assert.True(File.Exists(referencePath), $"Missing XCS reference: {referencePath}");

        var reference = File.ReadAllText(referencePath);

        Assert.Contains("CreateSectioningMillingStrategy", reference, StringComparison.Ordinal);
        Assert.Contains("CreateSegment", reference, StringComparison.Ordinal);
        Assert.Contains("CreateBladeCut", reference, StringComparison.Ordinal);
    }

    private static string GenerateProgram(Plate plate)
    {
        var nameService = new NameService();
        var emitter = new XilogEmitter(nameService);
        var router = new EmitterRouter(emitter, nameService, new MaestroCadTProfile());
        return router.GenerateProgram(plate);
    }

    private static void AssertNormalizedProgramEquals(string expected, string actual)
    {
        var normalizedExpected = NormalizeProgram(expected);
        var normalizedActual = NormalizeProgram(actual);

        Assert.Equal(normalizedExpected, normalizedActual);
    }

    private static string NormalizeProgram(string program)
    {
        var lines = program
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .Select(NormalizeMeaningfulLine);

        return string.Join("\n", lines);
    }

    private static string NormalizeMeaningfulLine(string line)
    {
        if (StartsWithAny(line,
                "CreatePolyline(",
                "CreateRoughFinish(",
                "CreateDrill(",
                "CreateMacro(",
                "CreateWorkplane("))
        {
            line = ReplaceFirstQuotedArgument(line, "NAME");
        }

        return NormalizeOutsideQuotes(line);
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string ReplaceFirstQuotedArgument(string line, string replacement)
    {
        var firstQuote = line.IndexOf('"');
        if (firstQuote < 0)
            return line;

        var secondQuote = line.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
            return line;

        return string.Concat(
            line.AsSpan(0, firstQuote + 1),
            replacement,
            line.AsSpan(secondQuote));
    }

    private static string NormalizeOutsideQuotes(string line)
    {
        var result = new StringBuilder();
        var segment = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                if (inQuotes)
                {
                    result.Append(segment);
                }
                else
                {
                    result.Append(NormalizeOutsideSegment(segment.ToString()));
                }

                segment.Clear();
                result.Append(ch);
                inQuotes = !inQuotes;
                continue;
            }

            segment.Append(ch);
        }

        if (segment.Length > 0)
        {
            result.Append(inQuotes
                ? segment.ToString()
                : NormalizeOutsideSegment(segment.ToString()));
        }

        return result.ToString();
    }

    private static string NormalizeOutsideSegment(string segment)
    {
        segment = Regex.Replace(segment, @"\s+", string.Empty);

        while (true)
        {
            var normalized = ArithmeticRegex.Replace(segment, static match =>
            {
                var left = double.Parse(match.Groups["left"].Value, CultureInfo.InvariantCulture);
                var right = double.Parse(match.Groups["right"].Value, CultureInfo.InvariantCulture);
                var value = match.Groups["op"].Value == "+"
                    ? left + right
                    : left - right;
                return FormatNumber(value);
            });

            if (normalized == segment)
                break;

            segment = normalized;
        }

        return NumberRegex.Replace(segment, static match =>
            FormatNumber(double.Parse(match.Value, CultureInfo.InvariantCulture)));
    }

    private static string FormatNumber(double value)
    {
        if (value == Math.Truncate(value))
            return ((long)value).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static readonly Regex ArithmeticRegex = new(
        @"(?<![A-Za-z_])(?<left>-?\d+(?:\.\d+)?)(?<op>[+-])(?<right>\d+(?:\.\d+)?)(?![A-Za-z_])",
        RegexOptions.Compiled);

    private static readonly Regex NumberRegex = new(
        @"(?<![A-Za-z_])[-+]?\d+(?:\.\d+)?(?![A-Za-z_])",
        RegexOptions.Compiled);

    private sealed record ProductionFixture(
        string SourceDwgPath,
        string ReferenceXcsPath,
        Plate Plate);

    private static class ProductionValidationFixtures
    {
        public static ProductionFixture CreatePutzschrankSockelMont()
        {
            var sourceDwgPath = Path.Combine(ReferencesPath, "cadt", "Putz-Schrank.dwg");
            var referenceXcsPath = Path.Combine(ReferencesPath, "Staub_SockelMont.xcs");

            var plate = new Plate
            {
                Name = "SockelMont",
                LengthX = 285,
                WidthY = 100,
                Thickness = 30,
                LayerPath = @"Putz-Schrank::SockelMont",
                Source = PlateSource.SolidDetection,
                Machinings = new Machining[]
                {
                    new RoutingMachining
                    {
                        Name = "SockelMont_Aussenkontur",
                        Points = new[]
                        {
                            (152.5, 100.0),
                            (0.0, 100.0),
                            (0.0, 0.0),
                            (285.0, 0.0),
                            (285.0, 100.0),
                            (132.5, 100.0)
                        },
                        Depth = 33.0,
                        ToolDiameter = 9.5,
                        TechCode = "E013",
                        IsClosed = true
                    }
                }
            };

            return new ProductionFixture(sourceDwgPath, referenceXcsPath, plate);
        }

        public static ProductionFixture CreateLegraboxFertigauszug()
        {
            var sourceDwgPath = Path.Combine(ReferencesPath, "cadt", "Pult_und_Korpus_Novotny.dwg");
            var referenceXcsPath = Path.Combine(ReferencesPath, "NEW_Fertigauszug_Legrabox.xcs");

            var plate = new Plate
            {
                Name = "Fertigauszug_Legrabox",
                LengthX = 350,
                WidthY = 612,
                Thickness = 1,
                LayerPath = @"Pult_und_Korpus_Novotny::Fertigauszug_Legrabox",
                Source = PlateSource.SolidDetection,
                Machinings = new Machining[]
                {
                    new RoutingMachining
                    {
                        Name = "Fertigauszug_Aussenkontur",
                        Points = new[]
                        {
                            (185.0, 612.0),
                            (0.0, 612.0),
                            (0.0, 0.0),
                            (350.0, 0.0),
                            (350.0, 612.0),
                            (165.0, 612.0)
                        },
                        Depth = 4.0,
                        ToolDiameter = 9.5,
                        TechCode = "E013",
                        IsClosed = true
                    }
                }
            };

            return new ProductionFixture(sourceDwgPath, referenceXcsPath, plate);
        }
    }
}
