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

public enum ToolMaterial
{
    Carbide,
    Hss,
    Diamond,
    Pcd,
    Ceramic,
    Other
}

public enum HolderKind
{
    ColletChuck,
    DrillBlock,
    SawAggregate,
    AngleAggregate,
    MacroAggregate,
    Generic
}

public enum ToolMotionProfile
{
    Freeform2D,
    LinearXyOnly,
    PointOnly,
    MacroDriven
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

public sealed record ToolHolderDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public HolderKind Kind { get; init; } = HolderKind.Generic;
    public double? GaugeLength { get; init; }
    public double? GaugeDiameter { get; init; }
    public double? ProjectionLength { get; init; }
    public string? Description { get; init; }
}

public sealed record ToolDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ToolKind Kind { get; init; }
    public string? HolderId { get; init; }
    public string? TechCode { get; init; }
    public required double NominalDiameter { get; init; }
    public double? ShankDiameter { get; init; }
    public double? CornerRadius { get; init; }
    public double? CuttingLength { get; init; }
    public double? OverallLength { get; init; }
    public int? FluteCount { get; init; }
    public ToolMaterial Material { get; init; } = ToolMaterial.Carbide;
    public double? SpindleSpeed { get; init; }
    public double? FeedRate { get; init; }
    public double? PlungeFeedRate { get; init; }
    public double? DefaultStepDown { get; init; }
    public double? DefaultStepOver { get; init; }
    public ToolMotionProfile MotionProfile { get; init; } = ToolMotionProfile.Freeform2D;
    public bool IsFixedAggregate { get; init; }
    public string? Description { get; init; }
}

public sealed record ToolLibrary
{
    public required string Name { get; init; }
    public required string MachineKey { get; init; }
    public IReadOnlyList<ToolHolderDefinition> Holders { get; init; } = Array.Empty<ToolHolderDefinition>();
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = Array.Empty<ToolDefinition>();

    public ToolLibrary AddOrUpdateHolder(ToolHolderDefinition holder)
    {
        ArgumentNullException.ThrowIfNull(holder);

        var updated = Holders
            .Where(existing => !string.Equals(existing.Id, holder.Id, StringComparison.OrdinalIgnoreCase))
            .Append(holder)
            .OrderBy(static h => h.Kind)
            .ThenBy(static h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return this with { Holders = updated };
    }

    public ToolLibrary MergeDefaults(ToolLibrary defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        var defaultHolderLookup = defaults.Holders.ToDictionary(holder => holder.Id, StringComparer.OrdinalIgnoreCase);
        var defaultToolLookup = defaults.Tools.ToDictionary(tool => tool.Id, StringComparer.OrdinalIgnoreCase);
        var mergedHolders = defaults.Holders
            .Concat(Holders)
            .GroupBy(holder => holder.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var merged = group.Last();
                return defaultHolderLookup.TryGetValue(merged.Id, out var defaultHolder)
                    ? NormalizeLegacyHolderPresentation(merged, defaultHolder)
                    : merged;
            })
            .OrderBy(static h => h.Kind)
            .ThenBy(static h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hydratedTools = Tools
            .Select(tool =>
            {
                var fallback = defaults.FindById(tool.Id)
                    ?? defaults.FindByTechCode(tool.TechCode, tool.Kind)
                    ?? defaults.FindClosestDiameter(tool.NominalDiameter, tool.Kind);

                if (fallback == null)
                    return tool;

                var mergedTool = tool with
                {
                    HolderId = tool.HolderId ?? fallback.HolderId,
                    ShankDiameter = tool.ShankDiameter ?? fallback.ShankDiameter,
                    CornerRadius = tool.CornerRadius ?? fallback.CornerRadius,
                    FluteCount = tool.FluteCount ?? fallback.FluteCount,
                    PlungeFeedRate = tool.PlungeFeedRate ?? fallback.PlungeFeedRate,
                    DefaultStepOver = tool.DefaultStepOver ?? fallback.DefaultStepOver,
                    MotionProfile = tool.MotionProfile == ToolMotionProfile.Freeform2D && fallback.MotionProfile != ToolMotionProfile.Freeform2D
                        ? fallback.MotionProfile
                        : tool.MotionProfile,
                    IsFixedAggregate = tool.IsFixedAggregate || fallback.IsFixedAggregate,
                    Description = tool.Description ?? fallback.Description
                };

                return defaultToolLookup.TryGetValue(mergedTool.Id, out var defaultTool)
                    ? NormalizeLegacyToolPresentation(mergedTool, defaultTool)
                    : mergedTool;
            })
            .OrderBy(static t => t.Kind)
            .ThenBy(static t => t.NominalDiameter)
            .ThenBy(static t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return this with
        {
            MachineKey = NormalizeMachineKey(MachineKey),
            Holders = mergedHolders,
            Tools = hydratedTools
        };
    }

    public ToolLibrary RemoveHolder(string holderId)
    {
        EnsureNotBlank(holderId, nameof(holderId));

        var updatedTools = Tools
            .Select(tool => string.Equals(tool.HolderId, holderId, StringComparison.OrdinalIgnoreCase)
                ? tool with { HolderId = null }
                : tool)
            .ToArray();

        return this with
        {
            Holders = Holders
                .Where(holder => !string.Equals(holder.Id, holderId, StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            Tools = updatedTools
        };
    }

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

    public ToolHolderDefinition? FindHolderById(string? holderId)
    {
        if (string.IsNullOrWhiteSpace(holderId))
            return null;

        return Holders.FirstOrDefault(holder =>
            string.Equals(holder.Id, holderId, StringComparison.OrdinalIgnoreCase));
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

        var compatibleTools = GetCompatibleTools(machining);
        if (compatibleTools.Count == 0)
            return null;

        var hintedTechCode = GetSuggestedTechCode(machining, profile);
        var byTechCode = compatibleTools.FirstOrDefault(tool =>
            string.Equals(tool.TechCode, hintedTechCode, StringComparison.OrdinalIgnoreCase));
        if (byTechCode != null)
            return byTechCode;

        var targetDiameter = GetTargetDiameter(machining);
        if (targetDiameter.HasValue)
        {
            return compatibleTools
                .OrderBy(tool => Math.Abs(tool.NominalDiameter - targetDiameter.Value))
                .ThenBy(tool => tool.NominalDiameter)
                .ThenBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        return compatibleTools[0];
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
            .Where(tool => IsCompatible(machining, tool))
            .Where(tool => finishTool == null || !string.Equals(tool.Id, finishTool.Id, StringComparison.OrdinalIgnoreCase))
            .Where(tool => tool.NominalDiameter > targetDiameter.Value + 0.2)
            .Where(tool => tool.NominalDiameter <= targetDiameter.Value * 1.75 + 0.5)
            .OrderBy(tool => tool.NominalDiameter)
            .FirstOrDefault();
    }

    public IReadOnlyList<ToolDefinition> GetCompatibleTools(Machining machining)
    {
        ArgumentNullException.ThrowIfNull(machining);

        return Tools
            .Where(tool => IsCompatible(machining, tool))
            .ToArray();
    }

    public bool IsCompatible(Machining machining, ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(machining);
        ArgumentNullException.ThrowIfNull(tool);

        if (tool.Kind != GetExpectedKind(machining))
            return false;

        if (tool.MotionProfile != GetRequiredMotionProfile(machining))
            return false;

        return machining switch
        {
            DrillMachining or DrillPatternMachining or HorizontalDrillMachining or GrooveRntMachining => tool.IsFixedAggregate,
            _ => true
        };
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
            Holders = library.Holders
                .OrderBy(static h => h.Kind)
                .ThenBy(static h => h.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
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
                Holders = CreateDefaultBiesseHolders(),
                Tools = CreateDefaultBiesseTools()
            },
            _ => new ToolLibrary
            {
                Name = "SCM/Xilog Default Tools",
                MachineKey = "xilog",
                Holders = CreateDefaultXilogHolders(),
                Tools = CreateDefaultXilogTools()
            }
        };
    }

    private static ToolKind GetExpectedKind(Machining machining) => machining switch
    {
        DrillMachining => ToolKind.Drill,
        DrillPatternMachining => ToolKind.Drill,
        HorizontalDrillMachining => ToolKind.Drill,
        GrooveRntMachining => ToolKind.Saw,
        MacroMachining => ToolKind.Macro,
        _ => ToolKind.Router
    };

    private static ToolMotionProfile GetRequiredMotionProfile(Machining machining) => machining switch
    {
        DrillMachining => ToolMotionProfile.PointOnly,
        DrillPatternMachining => ToolMotionProfile.PointOnly,
        HorizontalDrillMachining => ToolMotionProfile.PointOnly,
        GrooveRntMachining => ToolMotionProfile.LinearXyOnly,
        MacroMachining => ToolMotionProfile.MacroDriven,
        _ => ToolMotionProfile.Freeform2D
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

    private static string? GetSuggestedTechCode(Machining machining, IMachineProfile? profile)
    {
        if (!string.IsNullOrWhiteSpace(machining.TechCode))
            return machining.TechCode;

        if (machining is GrooveRntMachining groove && !string.IsNullOrWhiteSpace(groove.RntCode))
        {
            var normalized = NormalizeTechCodeDigits(groove.RntCode);
            if (!string.IsNullOrWhiteSpace(normalized))
                return $"RNT{normalized}";
        }

        return profile?.DefaultTech;
    }

    private static string NormalizeTechCodeDigits(string code)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return digits.PadLeft(3, '0');
    }

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
            CreateTool("scm_router_12", "SCM Router 12mm", ToolKind.Router, "scm_er32_collet", 12.0, "E013", 4.0, 18000, 6000),
            CreateTool("scm_router_9_5", "SCM Router 9.5mm", ToolKind.Router, "scm_er32_collet", 9.5, "E010", 3.0, 18000, 5000),
            CreateTool("scm_router_6", "SCM Router 6mm", ToolKind.Router, "scm_er32_collet", 6.0, "E021", 2.5, 18000, 4200),
            CreateTool("scm_router_5", "SCM Router 5mm", ToolKind.Router, "scm_er32_collet", 5.0, "E022", 2.0, 18000, 3600),
            CreateTool("scm_router_4", "SCM Router 4mm", ToolKind.Router, "scm_er32_collet", 4.0, "E005", 2.0, 18000, 3200),
            CreateTool("scm_router_3", "SCM Router 3mm", ToolKind.Router, "scm_er32_collet", 3.0, "E015", 1.5, 18000, 2500),
            CreateTool("scm_router_3_slot", "SCM Router 3mm Slot", ToolKind.Router, "scm_er32_collet", 3.0, "E004", 1.5, 18000, 2200),
            CreateTool("scm_router_6_macro", "SCM Router 6mm Macro", ToolKind.Router, "scm_macro_aggregate", 6.0, "E032", 2.0, 18000, 3200),
            CreateTool("scm_drill_35", "SCM Drill 35mm", ToolKind.Drill, "scm_vertical_drill_bank", 35.0, "D35", 15.0, 70.0, 3000, 900),
            CreateTool("scm_drill_8", "SCM Drill 8mm", ToolKind.Drill, "scm_vertical_drill_bank", 8.0, "D8", 25.0, 70.0, 4500, 1800),
            CreateTool("scm_drill_5", "SCM Drill 5mm", ToolKind.Drill, "scm_vertical_drill_bank", 5.0, "D5", 20.0, 70.0, 5000, 2200),
            CreateTool("scm_drill_3", "SCM Drill 3mm", ToolKind.Drill, "scm_vertical_drill_bank", 3.0, "D3", 18.0, 70.0, 5500, 2200),
            CreateTool("scm_saw_5_5", "SCM Rueckwandnuter 5.5mm", ToolKind.Saw, "scm_saw_aggregate", 5.5, "RNT066", 8.0, 90.0, 6000, 2500)
        };
    }

    private static ToolDefinition[] CreateDefaultBiesseTools()
    {
        return new[]
        {
            CreateTool("biesse_router_12", "Biesse Router 12mm", ToolKind.Router, "biesse_hsk_collet", 12.0, "T02", 4.0, 18000, 6200),
            CreateTool("biesse_router_10", "Biesse Router 10mm", ToolKind.Router, "biesse_hsk_collet", 10.0, "T01", 3.0, 18000, 5200),
            CreateTool("biesse_router_6", "Biesse Router 6mm", ToolKind.Router, "biesse_hsk_collet", 6.0, "T03", 2.0, 18000, 4200),
            CreateTool("biesse_drill_35", "Biesse Drill 35mm", ToolKind.Drill, "biesse_vertical_drill_bank", 35.0, "BG35", 15.0, 70.0, 3200, 900),
            CreateTool("biesse_drill_8", "Biesse Drill 8mm", ToolKind.Drill, "biesse_vertical_drill_bank", 8.0, "BG8", 25.0, 70.0, 4500, 1800),
            CreateTool("biesse_drill_5", "Biesse Drill 5mm", ToolKind.Drill, "biesse_vertical_drill_bank", 5.0, "BG5", 20.0, 70.0, 5200, 2200)
        };
    }

    private static ToolHolderDefinition[] CreateDefaultXilogHolders()
    {
        return new[]
        {
            new ToolHolderDefinition
            {
                Id = "scm_er32_collet",
                Name = "SCM HSK63F Standardhalter",
                Kind = HolderKind.ColletChuck,
                GaugeLength = 120,
                GaugeDiameter = 63,
                ProjectionLength = 65,
                Description = "Standard-HSK63F Halter fuer Router-Operationen."
            },
            new ToolHolderDefinition
            {
                Id = "scm_vertical_drill_bank",
                Name = "SCM Bohraggregat",
                Kind = HolderKind.DrillBlock,
                GaugeLength = 90,
                GaugeDiameter = 35,
                ProjectionLength = 45,
                Description = "Fixes Bohraggregat fuer Punktbohrungen und Horizontalbohrungen."
            },
            new ToolHolderDefinition
            {
                Id = "scm_macro_aggregate",
                Name = "SCM Makro-Aggregat",
                Kind = HolderKind.MacroAggregate,
                GaugeLength = 135,
                GaugeDiameter = 62,
                ProjectionLength = 70,
                Description = "Aggregate fuer CLAMEX- und Sonder-Makros."
            },
            new ToolHolderDefinition
            {
                Id = "scm_saw_aggregate",
                Name = "SCM Bohr-/Saegeaggregat",
                Kind = HolderKind.SawAggregate,
                GaugeLength = 145,
                GaugeDiameter = 80,
                ProjectionLength = 40,
                Description = "Fixes Bohr-/Saegeaggregat fuer Rueckwandnuten in X- oder Y-Richtung."
            }
        };
    }

    private static ToolHolderDefinition[] CreateDefaultBiesseHolders()
    {
        return new[]
        {
            new ToolHolderDefinition
            {
                Id = "biesse_hsk_collet",
                Name = "Biesse HSK63F Standardhalter",
                Kind = HolderKind.ColletChuck,
                GaugeLength = 118,
                GaugeDiameter = 63,
                ProjectionLength = 62,
                Description = "Standard-HSK63F Halter fuer Biesse Router-Werkzeuge."
            },
            new ToolHolderDefinition
            {
                Id = "biesse_vertical_drill_bank",
                Name = "Biesse Bohraggregat",
                Kind = HolderKind.DrillBlock,
                GaugeLength = 92,
                GaugeDiameter = 36,
                ProjectionLength = 46,
                Description = "Fixes Bohraggregat fuer Reihen- und Rasterbohrungen."
            }
        };
    }

    private static ToolDefinition CreateTool(
        string id,
        string name,
        ToolKind kind,
        string holderId,
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
            HolderId = holderId,
            TechCode = techCode,
            NominalDiameter = diameter,
            ShankDiameter = diameter,
            FluteCount = kind == ToolKind.Router ? 2 : 1,
            Material = ToolMaterial.Carbide,
            DefaultStepDown = stepDown,
            DefaultStepOver = kind == ToolKind.Router ? Math.Max(0.8, diameter * 0.45) : null,
            SpindleSpeed = spindleSpeed,
            FeedRate = feedRate,
            PlungeFeedRate = feedRate.HasValue ? Math.Max(300, feedRate.Value * 0.35) : null,
            MotionProfile = GetDefaultMotionProfile(kind),
            IsFixedAggregate = kind is ToolKind.Saw or ToolKind.Drill,
            Description = $"{name} [{techCode}]"
        };
    }

    private static ToolDefinition CreateTool(
        string id,
        string name,
        ToolKind kind,
        string holderId,
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
            HolderId = holderId,
            TechCode = techCode,
            NominalDiameter = diameter,
            ShankDiameter = diameter,
            CornerRadius = kind == ToolKind.Router && diameter >= 10 ? 0.5 : null,
            CuttingLength = cuttingLength,
            OverallLength = overallLength,
            FluteCount = kind == ToolKind.Router ? 2 : 1,
            Material = ToolMaterial.Carbide,
            SpindleSpeed = spindleSpeed,
            FeedRate = feedRate,
            PlungeFeedRate = feedRate.HasValue ? Math.Max(300, feedRate.Value * 0.35) : null,
            DefaultStepDown = cuttingLength.HasValue ? Math.Max(1.0, Math.Min(cuttingLength.Value / 2.0, 5.0)) : null,
            DefaultStepOver = kind == ToolKind.Router ? Math.Max(0.8, diameter * 0.45) : null,
            MotionProfile = GetDefaultMotionProfile(kind),
            IsFixedAggregate = kind is ToolKind.Saw or ToolKind.Drill,
            Description = $"{name} [{techCode}]"
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static void EnsureNotBlank(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
    }

    private static ToolMotionProfile GetDefaultMotionProfile(ToolKind kind)
    {
        return kind switch
        {
            ToolKind.Drill => ToolMotionProfile.PointOnly,
            ToolKind.Saw => ToolMotionProfile.LinearXyOnly,
            ToolKind.Macro => ToolMotionProfile.MacroDriven,
            _ => ToolMotionProfile.Freeform2D
        };
    }

    private static ToolHolderDefinition NormalizeLegacyHolderPresentation(
        ToolHolderDefinition holder,
        ToolHolderDefinition defaultHolder)
    {
        if (holder.Id.Equals("scm_er32_collet", StringComparison.OrdinalIgnoreCase)
            && holder.Name.Equals("SCM ER32 Spannzange", StringComparison.OrdinalIgnoreCase))
        {
            return holder with
            {
                Name = defaultHolder.Name,
                GaugeDiameter = defaultHolder.GaugeDiameter,
                Description = defaultHolder.Description
            };
        }

        if (holder.Id.Equals("biesse_hsk_collet", StringComparison.OrdinalIgnoreCase)
            && holder.Name.Equals("Biesse HSK Spannzange", StringComparison.OrdinalIgnoreCase))
        {
            return holder with
            {
                Name = defaultHolder.Name,
                GaugeDiameter = defaultHolder.GaugeDiameter,
                Description = defaultHolder.Description
            };
        }

        if (holder.Id.Equals("scm_vertical_drill_bank", StringComparison.OrdinalIgnoreCase)
            && holder.Name.Equals("SCM Vertikal-Bohrblock", StringComparison.OrdinalIgnoreCase))
        {
            return holder with
            {
                Name = defaultHolder.Name,
                Description = defaultHolder.Description
            };
        }

        if (holder.Id.Equals("biesse_vertical_drill_bank", StringComparison.OrdinalIgnoreCase)
            && holder.Name.Equals("Biesse Vertikal-Bohrblock", StringComparison.OrdinalIgnoreCase))
        {
            return holder with
            {
                Name = defaultHolder.Name,
                Description = defaultHolder.Description
            };
        }

        if (holder.Id.Equals("scm_saw_aggregate", StringComparison.OrdinalIgnoreCase)
            && holder.Name.Equals("SCM Saegeaggregat", StringComparison.OrdinalIgnoreCase))
        {
            return holder with
            {
                Name = defaultHolder.Name,
                Description = defaultHolder.Description
            };
        }

        return holder;
    }

    private static ToolDefinition NormalizeLegacyToolPresentation(
        ToolDefinition tool,
        ToolDefinition defaultTool)
    {
        if (tool.Id.Equals("scm_saw_5_5", StringComparison.OrdinalIgnoreCase)
            && tool.Name.Equals("SCM Saw 5.5mm", StringComparison.OrdinalIgnoreCase))
        {
            return tool with
            {
                Name = defaultTool.Name,
                MotionProfile = defaultTool.MotionProfile,
                IsFixedAggregate = defaultTool.IsFixedAggregate,
                Description = defaultTool.Description
            };
        }

        return tool;
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
        ToolpathPlanningOptions? options = null,
        MachiningToolOverride? strategyOverride = null)
    {
        ArgumentNullException.ThrowIfNull(machining);
        ArgumentNullException.ThrowIfNull(toolLibrary);

        options ??= new ToolpathPlanningOptions();

        var finishingTool = ResolveToolOverride(
                toolLibrary,
                machining,
                strategyOverride?.FinishingToolId)
            ?? toolLibrary.SuggestTool(machining);
        finishingTool = ApplyHolderOverride(toolLibrary, finishingTool, strategyOverride?.FinishingHolderId);

        var isRoutingLike = machining is RoutingMachining
            or RoutingWithArcsMachining
            or PocketMachining;

        if (!options.EnableRoughingStrategies || !isRoutingLike)
        {
            return new MachiningStrategy
            {
                FinishingTool = finishingTool,
                FinishingStepDown = finishingTool?.DefaultStepDown
            };
        }

        var roughingTool = ResolveToolOverride(
                toolLibrary,
                machining,
                strategyOverride?.RoughingToolId)
            ?? toolLibrary.SuggestRoughingTool(machining, finishingTool);
        roughingTool = ApplyHolderOverride(toolLibrary, roughingTool, strategyOverride?.RoughingHolderId);

        return new MachiningStrategy
        {
            RoughingTool = roughingTool,
            FinishingTool = finishingTool,
            StockToLeave = roughingTool != null ? options.DefaultStockToLeave : 0.0,
            RoughingStepDown = roughingTool?.DefaultStepDown,
            FinishingStepDown = finishingTool?.DefaultStepDown
        };
    }

    private static ToolDefinition? ResolveToolOverride(
        ToolLibrary toolLibrary,
        Machining machining,
        string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return null;

        var tool = toolLibrary.FindById(toolId);
        return tool != null && toolLibrary.IsCompatible(machining, tool) ? tool : null;
    }

    private static ToolDefinition? ApplyHolderOverride(
        ToolLibrary toolLibrary,
        ToolDefinition? tool,
        string? holderId)
    {
        if (tool == null || string.IsNullOrWhiteSpace(holderId))
            return tool;

        return toolLibrary.FindHolderById(holderId) != null
            ? tool with { HolderId = holderId }
            : tool;
    }
}

public sealed record MachiningToolOverride
{
    public required string OperationKey { get; init; }
    public string? RoughingToolId { get; init; }
    public string? RoughingHolderId { get; init; }
    public string? FinishingToolId { get; init; }
    public string? FinishingHolderId { get; init; }

    public bool HasOverride =>
        !string.IsNullOrWhiteSpace(RoughingToolId)
        || !string.IsNullOrWhiteSpace(RoughingHolderId)
        || !string.IsNullOrWhiteSpace(FinishingToolId)
        || !string.IsNullOrWhiteSpace(FinishingHolderId);
}

public sealed record ToolpathPlanningOptions
{
    public bool IncludeRapidMoves { get; init; } = true;
    public bool EnableRoughingStrategies { get; init; } = true;
    public double DefaultStockToLeave { get; init; } = 0.3;
    public IReadOnlyList<MachiningToolOverride> StrategyOverrides { get; init; } = Array.Empty<MachiningToolOverride>();

    public MachiningToolOverride? FindOverride(string operationKey)
    {
        return StrategyOverrides.FirstOrDefault(item =>
            string.Equals(item.OperationKey, operationKey, StringComparison.OrdinalIgnoreCase));
    }
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
    public string? OperationKey { get; init; }
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
