using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Converts machinings into previewable toolpath primitives.
/// This is intentionally approximate for visual validation and ordering checks.
/// It does not replace the machine controller's internal path planning.
/// </summary>
public static class ToolpathPlanner
{
    public static ToolpathPlan PlanPlate(
        Plate plate,
        ToolLibrary toolLibrary,
        ToolpathPlanningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(plate);
        ArgumentNullException.ThrowIfNull(toolLibrary);

        options ??= new ToolpathPlanningOptions();

        var sequence = plate.PreserveMachiningOrder
            ? plate.Machinings
            : EmitterRouter.OrderMachinings(plate.Machinings).ToArray();

        var operations = new List<ToolpathOperationPlan>();
        (double X, double Y)? currentPosition = null;

        foreach (var machining in sequence)
        {
            var planned = PlanMachining(machining, toolLibrary, options);
            if (planned.Count == 0)
                continue;

            if (options.IncludeRapidMoves)
            {
                var start = GetOperationStart(planned[0]);
                if (currentPosition.HasValue
                    && start.HasValue
                    && Distance(currentPosition.Value, start.Value) > 0.001)
                {
                    operations.Add(new ToolpathOperationPlan
                    {
                        Name = $"{machining.Name}_rapid",
                        MachiningType = GetMachiningType(machining),
                        PassType = ToolpathPassType.Rapid,
                        Depth = 0,
                        VisualDiameter = 0,
                        Primitives = new ToolpathPrimitive[]
                        {
                            new ToolpathLinePrimitive
                            {
                                StartX = currentPosition.Value.X,
                                StartY = currentPosition.Value.Y,
                                EndX = start.Value.X,
                                EndY = start.Value.Y
                            }
                        }
                    });
                }
            }

            operations.AddRange(planned);

            var end = GetOperationEnd(planned[^1]);
            if (end.HasValue)
                currentPosition = end;
        }

        return new ToolpathPlan
        {
            Plate = plate,
            Operations = operations
        };
    }

    private static IReadOnlyList<ToolpathOperationPlan> PlanMachining(
        Machining machining,
        ToolLibrary toolLibrary,
        ToolpathPlanningOptions options)
    {
        var strategy = MachiningStrategy.CreateDefault(machining, toolLibrary, options);
        return machining switch
        {
            DrillMachining drill => new[] { PlanDrill(drill, strategy.FinishingTool) },
            DrillPatternMachining pattern => new[] { PlanDrillPattern(pattern, strategy.FinishingTool) },
            HorizontalDrillMachining horizontal => new[] { PlanHorizontalDrill(horizontal, strategy.FinishingTool) },
            RoutingMachining routing => PlanRouting(routing, strategy),
            RoutingWithArcsMachining routingWithArcs => PlanRoutingWithArcs(routingWithArcs, strategy),
            PocketMachining pocket => PlanPocket(pocket, strategy),
            GrooveRntMachining groove => PlanGroove(groove, strategy),
            MacroMachining macro => new[] { PlanMacro(macro, strategy.FinishingTool) },
            _ => Array.Empty<ToolpathOperationPlan>()
        };
    }

    private static IReadOnlyList<ToolpathOperationPlan> PlanRouting(RoutingMachining routing, MachiningStrategy strategy)
    {
        var primitives = new ToolpathPrimitive[]
        {
            new ToolpathPolylinePrimitive
            {
                Points = routing.Points,
                Closed = routing.IsClosed
            }
        };

        return BuildRoutingPasses(
            routing.Name,
            MachiningType.Routing,
            routing.Depth,
            routing.ToolDiameter,
            primitives,
            strategy);
    }

    private static IReadOnlyList<ToolpathOperationPlan> PlanRoutingWithArcs(
        RoutingWithArcsMachining routing,
        MachiningStrategy strategy)
    {
        var polyline = ApproximatePolyline(routing);
        var primitives = new ToolpathPrimitive[]
        {
            new ToolpathPolylinePrimitive
            {
                Points = polyline,
                Closed = routing.IsClosed
            }
        };

        return BuildRoutingPasses(
            routing.Name,
            MachiningType.RoutingWithArcs,
            routing.Depth,
            routing.ToolDiameter,
            primitives,
            strategy);
    }

    private static IReadOnlyList<ToolpathOperationPlan> PlanPocket(PocketMachining pocket, MachiningStrategy strategy)
    {
        var primitives = pocket.Loops
            .Where(loop => loop.Count > 1)
            .Select(loop => (ToolpathPrimitive)new ToolpathPolylinePrimitive
            {
                Points = loop,
                Closed = true
            })
            .ToArray();

        return BuildRoutingPasses(
            pocket.Name,
            MachiningType.Pocket,
            pocket.Depth,
            pocket.ToolDiameter,
            primitives,
            strategy);
    }

    private static IReadOnlyList<ToolpathOperationPlan> PlanGroove(GrooveRntMachining groove, MachiningStrategy strategy)
    {
        var (endX, endY) = groove.Axis == Axis.X
            ? (groove.XStart + groove.Length, groove.YStart)
            : (groove.XStart, groove.YStart + groove.Length);

        var primitives = new ToolpathPrimitive[]
        {
            new ToolpathLinePrimitive
            {
                StartX = groove.XStart,
                StartY = groove.YStart,
                EndX = endX,
                EndY = endY
            }
        };

        return BuildRoutingPasses(
            groove.Name,
            MachiningType.GrooveRnt,
            groove.Depth,
            groove.Width,
            primitives,
            strategy);
    }

    private static IReadOnlyList<ToolpathOperationPlan> BuildRoutingPasses(
        string name,
        MachiningType machiningType,
        double depth,
        double fallbackDiameter,
        IReadOnlyList<ToolpathPrimitive> primitives,
        MachiningStrategy strategy)
    {
        var operations = new List<ToolpathOperationPlan>();

        if (strategy.HasRoughingPass)
        {
            operations.Add(new ToolpathOperationPlan
            {
                Name = $"{name}_rough",
                MachiningType = machiningType,
                PassType = ToolpathPassType.Roughing,
                Tool = strategy.RoughingTool,
                Depth = depth,
                VisualDiameter = strategy.RoughingTool?.NominalDiameter ?? fallbackDiameter,
                StockToLeave = strategy.StockToLeave,
                Primitives = primitives
            });
        }

        operations.Add(new ToolpathOperationPlan
        {
            Name = strategy.HasRoughingPass ? $"{name}_finish" : name,
            MachiningType = machiningType,
            PassType = strategy.HasRoughingPass ? ToolpathPassType.Finishing : ToolpathPassType.Feed,
            Tool = strategy.FinishingTool ?? strategy.RoughingTool,
            Depth = depth,
            VisualDiameter = strategy.FinishingTool?.NominalDiameter
                ?? strategy.RoughingTool?.NominalDiameter
                ?? fallbackDiameter,
            StockToLeave = strategy.HasRoughingPass ? 0.0 : null,
            Primitives = primitives
        });

        return operations;
    }

    private static ToolpathOperationPlan PlanDrill(DrillMachining drill, ToolDefinition? tool)
    {
        return new ToolpathOperationPlan
        {
            Name = drill.Name,
            MachiningType = MachiningType.Drill,
            PassType = ToolpathPassType.Drill,
            Tool = tool,
            Depth = drill.Depth,
            VisualDiameter = tool?.NominalDiameter ?? drill.Diameter,
            Primitives = new ToolpathPrimitive[]
            {
                new ToolpathCirclePrimitive
                {
                    CenterX = drill.X,
                    CenterY = drill.Y,
                    Diameter = drill.Diameter
                }
            }
        };
    }

    private static ToolpathOperationPlan PlanDrillPattern(DrillPatternMachining pattern, ToolDefinition? tool)
    {
        var circles = new List<ToolpathPrimitive>();
        for (var x = 0; x < pattern.CountX; x++)
        {
            for (var y = 0; y < pattern.CountY; y++)
            {
                circles.Add(new ToolpathCirclePrimitive
                {
                    CenterX = pattern.X + x * pattern.SpacingX,
                    CenterY = pattern.Y + y * pattern.SpacingY,
                    Diameter = pattern.Diameter
                });
            }
        }

        return new ToolpathOperationPlan
        {
            Name = pattern.Name,
            MachiningType = MachiningType.DrillPattern,
            PassType = ToolpathPassType.Drill,
            Tool = tool,
            Depth = pattern.Depth,
            VisualDiameter = tool?.NominalDiameter ?? pattern.Diameter,
            Primitives = circles
        };
    }

    private static ToolpathOperationPlan PlanHorizontalDrill(HorizontalDrillMachining horizontal, ToolDefinition? tool)
    {
        var (offsetX, offsetY) = horizontal.DrillSide switch
        {
            'L' => (-20.0, 0.0),
            'R' => (20.0, 0.0),
            'V' => (0.0, -20.0),
            'H' => (0.0, 20.0),
            _ => (0.0, 0.0)
        };

        return new ToolpathOperationPlan
        {
            Name = horizontal.Name,
            MachiningType = MachiningType.HorizontalDrill,
            PassType = ToolpathPassType.Drill,
            Tool = tool,
            Depth = horizontal.Depth,
            VisualDiameter = tool?.NominalDiameter ?? horizontal.Diameter,
            Primitives = new ToolpathPrimitive[]
            {
                new ToolpathCirclePrimitive
                {
                    CenterX = horizontal.X,
                    CenterY = horizontal.Y,
                    Diameter = horizontal.Diameter
                },
                new ToolpathLinePrimitive
                {
                    StartX = horizontal.X,
                    StartY = horizontal.Y,
                    EndX = horizontal.X + offsetX,
                    EndY = horizontal.Y + offsetY
                }
            }
        };
    }

    private static ToolpathOperationPlan PlanMacro(MacroMachining macro, ToolDefinition? tool)
    {
        var point = ExtractMacroPosition(macro.Parameters);
        var angleDeg = ExtractMacroAngle(macro.Parameters);
        var angleRad = angleDeg * Math.PI / 180.0;
        const double halfLength = 20.0;

        var startX = point.X - Math.Cos(angleRad) * halfLength;
        var startY = point.Y - Math.Sin(angleRad) * halfLength;
        var endX = point.X + Math.Cos(angleRad) * halfLength;
        var endY = point.Y + Math.Sin(angleRad) * halfLength;

        return new ToolpathOperationPlan
        {
            Name = macro.Name,
            MachiningType = MachiningType.Macro,
            PassType = ToolpathPassType.Macro,
            Tool = tool,
            Depth = 0,
            VisualDiameter = tool?.NominalDiameter ?? 6.0,
            Primitives = new ToolpathPrimitive[]
            {
                new ToolpathCirclePrimitive
                {
                    CenterX = point.X,
                    CenterY = point.Y,
                    Diameter = 8.0
                },
                new ToolpathLinePrimitive
                {
                    StartX = startX,
                    StartY = startY,
                    EndX = endX,
                    EndY = endY
                }
            }
        };
    }

    private static MachiningType GetMachiningType(Machining machining) => machining switch
    {
        DrillMachining => MachiningType.Drill,
        DrillPatternMachining => MachiningType.DrillPattern,
        RoutingMachining => MachiningType.Routing,
        RoutingWithArcsMachining => MachiningType.RoutingWithArcs,
        PocketMachining => MachiningType.Pocket,
        GrooveRntMachining => MachiningType.GrooveRnt,
        MacroMachining => MachiningType.Macro,
        HorizontalDrillMachining => MachiningType.HorizontalDrill,
        _ => throw new NotSupportedException($"Unsupported machining type: {machining.GetType().Name}")
    };

    private static IReadOnlyList<(double X, double Y)> ApproximatePolyline(RoutingWithArcsMachining routing)
    {
        var points = new List<(double X, double Y)> { (routing.StartX, routing.StartY) };
        var currentX = routing.StartX;
        var currentY = routing.StartY;

        foreach (var segment in routing.Segments)
        {
            if (!segment.IsArc)
            {
                points.Add((segment.EndX, segment.EndY));
                currentX = segment.EndX;
                currentY = segment.EndY;
                continue;
            }

            var radius = Math.Sqrt(
                Math.Pow(currentX - segment.CenterX, 2) +
                Math.Pow(currentY - segment.CenterY, 2));
            var startAngle = Math.Atan2(currentY - segment.CenterY, currentX - segment.CenterX);
            var endAngle = Math.Atan2(segment.EndY - segment.CenterY, segment.EndX - segment.CenterX);
            var sweep = NormalizeSweep(startAngle, endAngle, segment.Clockwise);
            var steps = Math.Max(4, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12.0)));

            for (var index = 1; index <= steps; index++)
            {
                var angle = startAngle + sweep * index / steps;
                points.Add((
                    segment.CenterX + Math.Cos(angle) * radius,
                    segment.CenterY + Math.Sin(angle) * radius));
            }

            currentX = segment.EndX;
            currentY = segment.EndY;
        }

        return points;
    }

    private static double NormalizeSweep(double startAngle, double endAngle, bool clockwise)
    {
        var sweep = endAngle - startAngle;
        if (clockwise)
        {
            while (sweep >= 0)
                sweep -= 2 * Math.PI;
        }
        else
        {
            while (sweep <= 0)
                sweep += 2 * Math.PI;
        }

        return sweep;
    }

    private static (double X, double Y)? GetOperationStart(ToolpathOperationPlan operation)
    {
        foreach (var primitive in operation.Primitives)
        {
            switch (primitive)
            {
                case ToolpathPolylinePrimitive polyline when polyline.Points.Count > 0:
                    return polyline.Points[0];
                case ToolpathLinePrimitive line:
                    return (line.StartX, line.StartY);
                case ToolpathCirclePrimitive circle:
                    return (circle.CenterX, circle.CenterY);
            }
        }

        return null;
    }

    private static (double X, double Y)? GetOperationEnd(ToolpathOperationPlan operation)
    {
        for (var index = operation.Primitives.Count - 1; index >= 0; index--)
        {
            switch (operation.Primitives[index])
            {
                case ToolpathPolylinePrimitive polyline when polyline.Points.Count > 0:
                    return polyline.Closed ? polyline.Points[0] : polyline.Points[^1];
                case ToolpathLinePrimitive line:
                    return (line.EndX, line.EndY);
                case ToolpathCirclePrimitive circle:
                    return (circle.CenterX, circle.CenterY);
            }
        }

        return null;
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }

    private static (double X, double Y) ExtractMacroPosition(IReadOnlyList<string?> parameters)
    {
        var x = TryParse(parameters, 0);
        var y = TryParse(parameters, 1);
        return (x ?? 0.0, y ?? 0.0);
    }

    private static double ExtractMacroAngle(IReadOnlyList<string?> parameters)
    {
        return TryParse(parameters, 46)
            ?? TryParse(parameters, 4)
            ?? 0.0;
    }

    private static double? TryParse(IReadOnlyList<string?> parameters, int index)
    {
        if (index < 0 || index >= parameters.Count)
            return null;

        var value = parameters[index];
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.StartsWith("DZ", StringComparison.OrdinalIgnoreCase))
            return null;

        return double.TryParse(value, out var parsed) ? parsed : null;
    }
}
