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
    public void Putzschrank_SeiteLinks_DwgFixture_MatchesProductionReference()
    {
        var fixture = ProductionValidationFixtures.CreatePutzschrankSeiteLinks();

        Assert.True(File.Exists(fixture.SourceDwgPath), $"Missing DWG fixture source: {fixture.SourceDwgPath}");
        Assert.True(File.Exists(fixture.ReferenceXcsPath), $"Missing XCS reference: {fixture.ReferenceXcsPath}");

        var reference = File.ReadAllText(fixture.ReferenceXcsPath);
        Assert.DoesNotContain("CreateBladeCut", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateSectioningMillingStrategy", reference, StringComparison.Ordinal);

        var generated = GenerateProgram(fixture.Plate);

        AssertNormalizedProgramEquals(reference, generated);
    }

    [Fact]
    public void Putzschrank_Boden_DwgFixture_MatchesProductionReference()
    {
        var fixture = ProductionValidationFixtures.CreatePutzschrankBoden();

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
        var dzValue = TryExtractDzValue(program);
        var lines = program
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
            .Select(line => NormalizeMeaningfulLine(line, dzValue));

        return string.Join("\n", lines);
    }

    private static string NormalizeMeaningfulLine(string line, double? dzValue)
    {
        if (line.StartsWith("SetMachiningParameters(", StringComparison.Ordinal))
            line = ReplaceFirstQuotedArgument(line, "MP");

        if (StartsWithAny(line,
                "CreatePolyline(",
                "CreateRoughFinish(",
                "CreateDrill(",
                "CreateMacro(",
                "CreateWorkplane("))
        {
            line = ReplaceFirstQuotedArgument(line, "NAME");
        }

        if (line.StartsWith("SelectWorkplane(", StringComparison.Ordinal)
            && !StartsWithAny(line, "SelectWorkplane(\"Top\")", "SelectWorkplane(\"Bottom\")"))
        {
            line = ReplaceFirstQuotedArgument(line, "WORKPLANE");
        }

        return NormalizeOutsideQuotes(line, dzValue);
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

    private static string NormalizeOutsideQuotes(string line, double? dzValue)
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
                    result.Append(NormalizeOutsideSegment(segment.ToString(), dzValue));
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
                : NormalizeOutsideSegment(segment.ToString(), dzValue));
        }

        return result.ToString();
    }

    private static string NormalizeOutsideSegment(string segment, double? dzValue)
    {
        segment = Regex.Replace(segment, @"\s+", string.Empty);

        if (dzValue.HasValue)
        {
            segment = DzRegex.Replace(segment, FormatNumber(dzValue.Value));
        }

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

    private static double? TryExtractDzValue(string program)
    {
        var match = DzDeclarationRegex.Match(program);
        if (!match.Success)
            return null;

        return double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
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

    private static readonly Regex DzDeclarationRegex = new(
        @"double\s+DZ\s*=\s*(?<value>-?\d+(?:\.\d+)?)\s*;",
        RegexOptions.Compiled);

    private static readonly Regex DzRegex = new(
        @"(?<![A-Za-z_])DZ(?![A-Za-z_])",
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

        /// <summary>
        /// Hand-built <see cref="Plate"/> matching production <c>Staub_Seite_links.xcs</c> (Putz-Schrank cabinet).
        /// Order matches CAD+T: outer contour, RNT groove, hinge/cup drills, System-32 rows, shelf-pin row.
        /// <see cref="Plate.PreserveMachiningOrder"/> is required because type-based sorting merges all drills before patterns.
        /// </summary>
        public static ProductionFixture CreatePutzschrankSeiteLinks()
        {
            var sourceDwgPath = Path.Combine(ReferencesPath, "cadt", "Putz-Schrank.dwg");
            var referenceXcsPath = Path.Combine(ReferencesPath, "Staub_Seite_links.xcs");

            var machinings = new List<Machining>
            {
                new RoutingMachining
                {
                    Name = "Aussenkontur_1",
                    Points = new[]
                    {
                        (1188.0, 380.0),
                        (0.0, 380.0),
                        (0.0, 0.0),
                        (2356.0, 0.0),
                        (2356.0, 380.0),
                        (1168.0, 380.0)
                    },
                    Depth = 9.5,
                    ToolDiameter = 9.5,
                    TechCode = "E010",
                    IsClosed = true
                },
                new GrooveRntMachining
                {
                    Name = "Nut_in_X",
                    Axis = Core.LayerParser.Axis.X,
                    XStart = -70,
                    YStart = 361.5,
                    Length = 2426,
                    Width = 5.5,
                    Depth = 5,
                    RntCode = "066"
                }
            };

            foreach (var (x, y, depth, dia) in StaubSeiteLinksFirstDrills)
            {
                machinings.Add(new DrillMachining
                {
                    Name = "Vertikale_Bohrung",
                    X = x,
                    Y = y,
                    Depth = depth,
                    Diameter = dia
                });
            }

            foreach (var row in StaubSeiteLinksDrillRows)
            {
                machinings.Add(new DrillPatternMachining
                {
                    Name = "Vertikale_Lochreihe",
                    X = row.X,
                    Y = row.Y,
                    Depth = 12,
                    Diameter = 5,
                    CountX = 1,
                    CountY = row.CountY,
                    SpacingX = 0,
                    SpacingY = 32
                });
            }

            foreach (var (x, y, depth, dia) in StaubSeiteLinksLastDrills)
            {
                machinings.Add(new DrillMachining
                {
                    Name = "Vertikale_Bohrung_klein",
                    X = x,
                    Y = y,
                    Depth = depth,
                    Diameter = dia
                });
            }

            var plate = new Plate
            {
                Name = "Seite_links",
                LengthX = 2356,
                WidthY = 380,
                Thickness = 19,
                LayerPath = @"Putz-Schrank::Seite_links",
                Source = PlateSource.SolidDetection,
                PreserveMachiningOrder = true,
                Machinings = machinings
            };

            return new ProductionFixture(sourceDwgPath, referenceXcsPath, plate);
        }

        /// <summary>
        /// Hand-built <see cref="Plate"/> matching production <c>Staub_Boden.xcs</c>.
        /// Covers horizontal drills via <c>CreateWorkplane()</c>, an RNT groove and vertical cup drills.
        /// Order matches CAD+T; the production file emits side drilling before groove + top drills.
        /// </summary>
        public static ProductionFixture CreatePutzschrankBoden()
        {
            var sourceDwgPath = Path.Combine(ReferencesPath, "cadt", "Putz-Schrank.dwg");
            var referenceXcsPath = Path.Combine(ReferencesPath, "Staub_Boden.xcs");

            var machinings = new List<Machining>
            {
                new RoutingMachining
                {
                    Name = "Aussenkontur_1",
                    Points = new[]
                    {
                        (416.75, 380.0),
                        (0.0, 380.0),
                        (0.0, 0.0),
                        (813.5, 0.0),
                        (813.5, 380.0),
                        (396.75, 380.0)
                    },
                    Depth = 9.5,
                    ToolDiameter = 9.5,
                    TechCode = "E010",
                    IsClosed = true
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_1_L",
                    X = 0,
                    Y = 43,
                    Depth = 30,
                    Diameter = 8,
                    DrillSide = 'L',
                    Side = MachiningSide.Left
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_2_L",
                    X = 0,
                    Y = 75,
                    Depth = 20,
                    Diameter = 8,
                    DrillSide = 'L',
                    Side = MachiningSide.Left
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_3_L",
                    X = 0,
                    Y = 305,
                    Depth = 20,
                    Diameter = 8,
                    DrillSide = 'L',
                    Side = MachiningSide.Left
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_4_L",
                    X = 0,
                    Y = 337,
                    Depth = 30,
                    Diameter = 8,
                    DrillSide = 'L',
                    Side = MachiningSide.Left
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_1_R",
                    X = 813.5,
                    Y = 43,
                    Depth = 30,
                    Diameter = 8,
                    DrillSide = 'R',
                    Side = MachiningSide.Right
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_2_R",
                    X = 813.5,
                    Y = 75,
                    Depth = 20,
                    Diameter = 8,
                    DrillSide = 'R',
                    Side = MachiningSide.Right
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_3_R",
                    X = 813.5,
                    Y = 305,
                    Depth = 20,
                    Diameter = 8,
                    DrillSide = 'R',
                    Side = MachiningSide.Right
                },
                new HorizontalDrillMachining
                {
                    Name = "Horizontal freie Bohrung_4_R",
                    X = 813.5,
                    Y = 337,
                    Depth = 30,
                    Diameter = 8,
                    DrillSide = 'R',
                    Side = MachiningSide.Right
                },
                new GrooveRntMachining
                {
                    Name = "Nut_in_X",
                    Axis = Core.LayerParser.Axis.X,
                    XStart = -70,
                    YStart = 361.5,
                    Length = 883.5,
                    Width = 5.5,
                    Depth = 5,
                    RntCode = "066"
                },
                new DrillMachining
                {
                    Name = "Vertikale Bohrung_1",
                    X = 24,
                    Y = 75,
                    Depth = 14,
                    Diameter = 15
                },
                new DrillMachining
                {
                    Name = "Vertikale Bohrung_2",
                    X = 24,
                    Y = 305,
                    Depth = 14,
                    Diameter = 15
                },
                new DrillMachining
                {
                    Name = "Vertikale Bohrung_3",
                    X = 789.5,
                    Y = 305,
                    Depth = 14,
                    Diameter = 15
                },
                new DrillMachining
                {
                    Name = "Vertikale Bohrung_4",
                    X = 789.5,
                    Y = 75,
                    Depth = 14,
                    Diameter = 15
                }
            };

            var plate = new Plate
            {
                Name = "Boden",
                LengthX = 813.5,
                WidthY = 380,
                Thickness = 19,
                LayerPath = @"Putz-Schrank::Boden",
                Source = PlateSource.SolidDetection,
                PreserveMachiningOrder = true,
                Machinings = machinings
            };

            return new ProductionFixture(sourceDwgPath, referenceXcsPath, plate);
        }

        private static readonly (double X, double Y, double Depth, double Dia)[] StaubSeiteLinksFirstDrills =
        {
            (9.5, 43, 10, 8),
            (9.5, 75, 12, 8),
            (9.5, 305, 12, 8),
            (9.5, 337, 10, 8),
            (2346.5, 337, 10, 8),
            (2346.5, 305, 12, 8),
            (2346.5, 75, 12, 8),
            (2346.5, 43, 10, 8),
            (2346.5, 7, 5, 5)
        };

        private static readonly (int CountY, double X, double Y)[] StaubSeiteLinksDrillRows =
        {
            (2, 2236, 36),
            (8, 1962, 50),
            (8, 1706, 50),
            (2, 1699, 36),
            (8, 1450, 50),
            (8, 1194, 50),
            (2, 1162, 36),
            (8, 938, 50),
            (8, 682, 50),
            (2, 625, 36),
            (8, 426, 50),
            (8, 170, 50),
            (2, 88, 36),
            (8, 170, 309),
            (8, 426, 309),
            (8, 682, 309),
            (8, 938, 309),
            (8, 1194, 309),
            (8, 1450, 309),
            (8, 1706, 309),
            (8, 1962, 309)
        };

        private static readonly (double X, double Y, double Depth, double Dia)[] StaubSeiteLinksLastDrills =
        {
            (2218, 34, 17, 3),
            (1757, 34, 17, 3),
            (1220, 34, 17, 3),
            (683, 34, 17, 3),
            (134, 34, 17, 3)
        };

    }

    // --- BladeCut tests (Sprint 6) ---

    [Fact]
    public void BladeCut_Production_Reference_ValidatesFormat()
    {
        // Create a BladeCut machining based on NEW_Schubladen_Doppel_1.xcs reference
        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 19, 354, 19, -187.5),
            new("Cut segment_2", 628, -187.5, 628, 354)
        };

        var bladeCut = new BladeCutMachining
        {
            Name = "Geneigter Schnitt in X/Y_1",
            Angle = 45.0,
            Segments = segments,
            Depth = 15.0,
            TechCode = "E015",
            Side = MachiningSide.Top,
            Source = MachiningSource.BlockDetection
        };

        var plate = new Plate
        {
            Name = "TestBladeCut",
            LengthX = 200,
            WidthY = 100,
            Thickness = 19,
            Machinings = new[] { bladeCut },
            PreserveMachiningOrder = true
        };

        var generated = GenerateProgram(plate);
        var referencePath = Path.Combine(ReferencesPath, "test_bladecut_reference.xcs");
        var reference = File.ReadAllText(referencePath);

        AssertNormalizedProgramEquals(reference, generated);
    }

    [Fact]
    public void BladeCut_EmptySegments_DoesNotCrash()
    {
        var bladeCut = new BladeCutMachining
        {
            Name = "Empty BladeCut",
            Angle = 45.0,
            Segments = Array.Empty<BladeCutSegment>(),
            Depth = 15.0,
            TechCode = "E015"
        };

        var plate = new Plate
        {
            Name = "TestEmpty",
            LengthX = 100,
            WidthY = 100,
            Thickness = 19,
            Machinings = new[] { bladeCut }
        };

        var generated = GenerateProgram(plate);

        Assert.Contains("CreateSectioningMillingStrategy", generated);
        Assert.Contains("CreateBladeCut", generated);
        Assert.DoesNotContain("CreateSegment", generated);
    }

    [Theory]
    [InlineData(30.0, "30.00")]
    [InlineData(45.0, "45.00")]
    [InlineData(60.0, "60.00")]
    [InlineData(90.0, "90.00")]
    public void BladeCut_AngleFormats_ProducesCorrectXCS(double angle, string expectedFormat)
    {
        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 0, 0, 10, 10)
        };

        var bladeCut = new BladeCutMachining
        {
            Name = "Test Angle",
            Angle = angle,
            Segments = segments,
            Depth = 15.0,
            TechCode = "E015"
        };

        var plate = new Plate
        {
            Name = "TestAngle",
            LengthX = 100,
            WidthY = 100,
            Thickness = 19,
            Machinings = new[] { bladeCut }
        };

        var generated = GenerateProgram(plate);

        Assert.Contains($",{expectedFormat},", generated);
    }
}
