using System.Text.Json;
using System.Text.Json.Serialization;
using RhinoCNCExporter.Core.Profiles;

namespace RhinoCNCExporter.Core.Models;

public enum ToolKind
{
    Router,
    Drill,
    Saw,
    Macro
}

public enum ToolpathPassType
{
    Rapid,
    Feed,
    Roughing,
    Finishing,
    Drill,
    Macro
}

public sealed record ToolDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ToolKind Kind { get; init; }
    public string? TechCode { get; init; }
    public required double NominalDiameter { get; init; }
    public double? CuttingLength { get; init; }
    public double? OverallLength { get; init; }
    public double? SpindleSpeed { get; init; }
    public double? FeedRate { get; init; }
    public double? DefaultStepDown { get; init; }
    public string? Description { get; init; }
}

public sealed record ToolLibrary
{
    public required string Name { get; init; }
    public required string MachineKey { get; init; }
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = Array.Empty<ToolDefinition>();

    public ToolLibrary AddOrUpdate(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var updated = Tools
            .Where(existing => !string.Equals(existing.Id, tool.Id, StringComparison.OrdinalIgnoreCase))
            .Append(tool)
            .OrderBy(static t => t.Kind)
            .ThenBy(static t => t.NominalDiameter)
            .ThenBy(static t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return this with { Tools = updated };
    }

    public ToolLibrary Remove(string toolId)
    {
        EnsureNotBlank(toolId, nameof(toolId));

        return this with
        {
            Tools = Tools
                .Where(tool => !string.Equals(tool.Id, toolId, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
    }

    public ToolDefinition? FindById(string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return null;

        return Tools.FirstOrDefault(tool =>
            string.Equals(tool.Id, toolId, StringComparison.OrdinalIgnoreCase));
    }

    public ToolDefinition? FindByTechCode(string? techCode, ToolKind? expectedKind = null)
    {
        if (string.IsNullOrWhiteSpace(techCode))
            return null;

        return Tools.FirstOrDefault(tool =>
            string.Equals(tool.TechCode, techCode, StringComparison.OrdinalIgnoreCase)
            && (!expectedKind.HasValue || tool.Kind == expectedKind.Value));
    }

    public ToolDefinition? FindClosestDiameter(double diameter, ToolKind kind)
    {
        return Tools
            .Where(tool => tool.Kind == kind)
            .OrderBy(tool => Math.Abs(tool.NominalDiameter - diameter))
            .ThenBy(tool => tool.NominalDiameter)
            .FirstOrDefault();
    }

    public ToolDefinition? SuggestTool(Machining machining, IMachineProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(machining);

        var kind = GetExpectedKind(machining);
        var byTechCode = FindByTechCode(machining.TechCode ?? profile?.DefaultTech, kind);
        if (byTechCode != null)
            return byTechCode;

        var targetDiameter = GetTargetDiameter(machining);
        if (targetDiameter.HasValue)
            return FindClosestDiameter(targetDiameter.Value, kind);

        return Tools.FirstOrDefault(tool => tool.Kind == kind);
    }

    public ToolDefinition? SuggestRoughingTool(Machining machining, ToolDefinition? finishTool)
    {
        ArgumentNullException.ThrowIfNull(machining);

        var kind = GetExpectedKind(machining);
        if (kind != ToolKind.Router)
            return null;

        var targetDiameter = finishTool?.NominalDiameter ?? GetTargetDiameter(machining);
        if (!targetDiameter.HasValue)
            return null;

        return Tools
            .Where(tool => tool.Kind == kind)
            .Where(tool => finishTool == null || !string.Equals(tool.Id, finishTool.Id, StringComparison.OrdinalIgnoreCase))
            .Where(tool => tool.NominalDiameter > targetDiameter.Value + 0.2)
            .Where(tool => tool.NominalDiameter <= targetDiameter.Value * 1.75 + 0.5)
            .OrderBy(tool => tool.NominalDiameter)
            .FirstOrDefault();
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static ToolLibrary FromJson(string json)
    {
        EnsureNotBlank(json, nameof(json));

        var library = JsonSerializer.Deserialize<ToolLibrary>(json, JsonOptions);
        if (library == null)
            throw new InvalidOperationException("Tool library JSON could not be deserialized.");

        return library with
        {
            Tools = library.Tools
                .OrderBy(static t => t.Kind)
                .ThenBy(static t => t.NominalDiameter)
                .ThenBy(static t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public static ToolLibrary LoadFromFile(string filePath)
    {
        EnsureNotBlank(filePath, nameof(filePath));
        return FromJson(File.ReadAllText(filePath));
    }

    public void SaveToFile(string filePath)
    {
        EnsureNotBlank(filePath, nameof(filePath));

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, ToJson());
    }

    public static ToolLibrary CreateDefault(IMachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return CreateDefault(profile.MachineKey);
    }

    public static ToolLibrary CreateDefault(string machineKey)
    {
        var normalized = NormalizeMachineKey(machineKey);
        return normalized switch
        {
            "biesse" => new ToolLibrary
            {
                Name = "Biesse Default Tools",
                MachineKey = normalized,
                Tools = CreateDefaultBiesseTools()
            },
            _ => new ToolLibrary
            {
                Name = "SCM/Xilog Default Tools",
                MachineKey = "xilog",
                Tools = CreateDefaultXilogTools()
            }
        };
    }

    private static ToolKind GetExpectedKind(Machining machining) => machining switch
    {
        DrillMachining => ToolKind.Drill,
        DrillPatternMachining => ToolKind.Drill,
        HorizontalDrillMachining => ToolKind.Drill,
        MacroMachining => ToolKind.Macro,
        _ => ToolKind.Router
    };

    private static double? GetTargetDiameter(Machining machining) => machining switch
    {
        DrillMachining drill => drill.Diameter,
        DrillPatternMachining pattern => pattern.Diameter,
        HorizontalDrillMachining horizontal => horizontal.Diameter,
        RoutingMachining routing => routing.ToolDiameter,
        RoutingWithArcsMachining routing => routing.ToolDiameter,
        PocketMachining pocket => pocket.ToolDiameter,
        GrooveRntMachining groove => groove.Width,
        _ => null
    };

    private static string NormalizeMachineKey(string? machineKey)
    {
        if (string.IsNullOrWhiteSpace(machineKey))
            return "xilog";

        var normalized = machineKey.Trim().ToLowerInvariant();
        return normalized.Contains("biesse", StringComparison.Ordinal) ? "biesse" : "xilog";
    }

    private static ToolDefinition[] CreateDefaultXilogTools()
    {
        return new[]
        {
            CreateTool("scm_router_12", "SCM Router 12mm", ToolKind.Router, 12.0, "E013", 4.0, 18000, 6000),
            CreateTool("scm_router_9_5", "SCM Router 9.5mm", ToolKind.Router, 9.5, "E010", 3.0, 18000, 5000),
            CreateTool("scm_router_6", "SCM Router 6mm", ToolKind.Router, 6.0, "E021", 2.5, 18000, 4200),
            CreateTool("scm_router_5", "SCM Router 5mm", ToolKind.Router, 5.0, "E022", 2.0, 18000, 3600),
            CreateTool("scm_router_4", "SCM Router 4mm", ToolKind.Router, 4.0, "E005", 2.0, 18000, 3200),
            CreateTool("scm_router_3", "SCM Router 3mm", ToolKind.Router, 3.0, "E015", 1.5, 18000, 2500),
            CreateTool("scm_router_3_slot", "SCM Router 3mm Slot", ToolKind.Router, 3.0, "E004", 1.5, 18000, 2200),
            CreateTool("scm_router_6_macro", "SCM Router 6mm Macro", ToolKind.Router, 6.0, "E032", 2.0, 18000, 3200),
            CreateTool("scm_drill_35", "SCM Drill 35mm", ToolKind.Drill, 35.0, "D35", 15.0, 70.0, 3000, 900),
            CreateTool("scm_drill_8", "SCM Drill 8mm", ToolKind.Drill, 8.0, "D8", 25.0, 70.0, 4500, 1800),
            CreateTool("scm_drill_5", "SCM Drill 5mm", ToolKind.Drill, 5.0, "D5", 20.0, 70.0, 5000, 2200),
            CreateTool("scm_drill_3", "SCM Drill 3mm", ToolKind.Drill, 3.0, "D3", 18.0, 70.0, 5500, 2200),
            CreateTool("scm_saw_5_5", "SCM Saw 5.5mm", ToolKind.Saw, 5.5, "RNT066", 8.0, 90.0, 6000, 2500)
        };
    }

    private static ToolDefinition[] CreateDefaultBiesseTools()
    {
        return new[]
        {
            CreateTool("biesse_router_12", "Biesse Router 12mm", ToolKind.Router, 12.0, "T02", 4.0, 18000, 6200),
            CreateTool("biesse_router_10", "Biesse Router 10mm", ToolKind.Router, 10.0, "T01", 3.0, 18000, 5200),
            CreateTool("biesse_router_6", "Biesse Router 6mm", ToolKind.Router, 6.0, "T03", 2.0, 18000, 4200),
            CreateTool("biesse_drill_35", "Biesse Drill 35mm", ToolKind.Drill, 35.0, "BG35", 15.0, 70.0, 3200, 900),
            CreateTool("biesse_drill_8", "Biesse Drill 8mm", ToolKind.Drill, 8.0, "BG8", 25.0, 70.0, 4500, 1800),
            CreateTool("biesse_drill_5", "Biesse Drill 5mm", ToolKind.Drill, 5.0, "BG5", 20.0, 70.0, 5200, 2200)
        };
    }

    private static ToolDefinition CreateTool(
        string id,
        string name,
        ToolKind kind,
        double diameter,
        string techCode,
        double? stepDown,
        double? spindleSpeed,
        double? feedRate)
    {
        return new ToolDefinition
        {
            Id = id,
            Name = name,
            Kind = kind,
            TechCode = techCode,
            NominalDiameter = diameter,
            DefaultStepDown = stepDown,
            SpindleSpeed = spindleSpeed,
            FeedRate = feedRate,
            Description = $"{name} [{techCode}]"
        };
    }

    private static ToolDefinition CreateTool(
        string id,
        string name,
        ToolKind kind,
        double diameter,
        string techCode,
        double? cuttingLength,
        double? overallLength,
        double? spindleSpeed,
        double? feedRate)
    {
        return new ToolDefinition
        {
            Id = id,
            Name = name,
            Kind = kind,
            TechCode = techCode,
            NominalDiameter = diameter,
            CuttingLength = cuttingLength,
            OverallLength = overallLength,
            SpindleSpeed = spindleSpeed,
            FeedRate = feedRate,
            DefaultStepDown = cuttingLength.HasValue ? Math.Max(1.0, Math.Min(cuttingLength.Value / 2.0, 5.0)) : null,
            Description = $"{name} [{techCode}]"
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static void EnsureNotBlank(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
    }
}

public sealed record MachiningStrategy
{
    public ToolDefinition? RoughingTool { get; init; }
    public ToolDefinition? FinishingTool { get; init; }
    public double StockToLeave { get; init; }
    public double? RoughingStepDown { get; init; }
    public double? FinishingStepDown { get; init; }

    public bool HasRoughingPass =>
        RoughingTool != null && FinishingTool != null && StockToLeave > 0;

    public static MachiningStrategy CreateDefault(
        Machining machining,
        ToolLibrary toolLibrary,
        ToolpathPlanningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(machining);
        ArgumentNullException.ThrowIfNull(toolLibrary);

        options ??= new ToolpathPlanningOptions();

        var finishingTool = toolLibrary.SuggestTool(machining);
        var isRoutingLike = machining is RoutingMachining
            or RoutingWithArcsMachining
            or PocketMachining
            or GrooveRntMachining;

        if (!options.EnableRoughingStrategies || !isRoutingLike)
        {
            return new MachiningStrategy
            {
                FinishingTool = finishingTool,
                FinishingStepDown = finishingTool?.DefaultStepDown
            };
        }

        var roughingTool = toolLibrary.SuggestRoughingTool(machining, finishingTool);
        return new MachiningStrategy
        {
            RoughingTool = roughingTool,
            FinishingTool = finishingTool,
            StockToLeave = roughingTool != null ? options.DefaultStockToLeave : 0.0,
            RoughingStepDown = roughingTool?.DefaultStepDown,
            FinishingStepDown = finishingTool?.DefaultStepDown
        };
    }
}

public sealed record ToolpathPlanningOptions
{
    public bool IncludeRapidMoves { get; init; } = true;
    public bool EnableRoughingStrategies { get; init; } = true;
    public double DefaultStockToLeave { get; init; } = 0.3;
}

public sealed record ToolpathPlan
{
    public required Plate Plate { get; init; }
    public IReadOnlyList<ToolpathOperationPlan> Operations { get; init; } = Array.Empty<ToolpathOperationPlan>();

    public int OperationCount => Operations.Count;
    public int PrimitiveCount => Operations.Sum(operation => operation.Primitives.Count);
}

public sealed record ToolpathOperationPlan
{
    public required string Name { get; init; }
    public required MachiningType MachiningType { get; init; }
    public required ToolpathPassType PassType { get; init; }
    public ToolDefinition? Tool { get; init; }
    public double Depth { get; init; }
    public double VisualDiameter { get; init; }
    public double? StockToLeave { get; init; }
    public IReadOnlyList<ToolpathPrimitive> Primitives { get; init; } = Array.Empty<ToolpathPrimitive>();

    public string DisplayLabel
    {
        get
        {
            var toolText = Tool == null
                ? "ohne Werkzeug"
                : $"{Tool.Name} ({Tool.TechCode ?? "n/a"})";
            return $"{PassType}: {Name} [{toolText}]";
        }
    }
}

public abstract record ToolpathPrimitive;

public sealed record ToolpathPolylinePrimitive : ToolpathPrimitive
{
    public required IReadOnlyList<(double X, double Y)> Points { get; init; }
    public bool Closed { get; init; }
}

public sealed record ToolpathLinePrimitive : ToolpathPrimitive
{
    public required double StartX { get; init; }
    public required double StartY { get; init; }
    public required double EndX { get; init; }
    public required double EndY { get; init; }
}

public sealed record ToolpathCirclePrimitive : ToolpathPrimitive
{
    public required double CenterX { get; init; }
    public required double CenterY { get; init; }
    public required double Diameter { get; init; }
}
